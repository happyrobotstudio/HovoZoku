namespace Dreamteck.Forever.Editor
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(RemoteLevel))]
    public class RemoteLevelEditor : Editor
    {
        public class SequenceEditWindow : EditorWindow
        {
            private RemoteLevel target = null;
            SegmentSequenceEditor sequenceEditor = null;
            public void Init(RemoteLevel t)
            {
                target = t;
                titleContent = new GUIContent("Sequence Editor");
                sequenceEditor = new SegmentSequenceEditor(target, t.sequenceCollection, new Rect(100, 100, 600, 600));
                sequenceEditor.onWillChange += RecordUndo;
                sequenceEditor.onChanged += OnChanged;
                sequenceEditor.onApplySequences += OnApplySequences;
            }

            void OnChanged()
            {
                Repaint();
            }

            void RecordUndo()
            {
                Undo.RecordObject(target, "Edit Segment Collection");
            }

            void OnApplySequences(SegmentSequence[] sequences)
            {
                target.sequenceCollection.sequences = sequences;
            }

            private void OnGUI()
            {
                sequenceEditor.viewRect = new Rect(5, 5, position.width, position.height);
                sequenceEditor.windowPosition = new Vector2(position.x, position.y);
                sequenceEditor.DrawEditor();
                target.sequenceCollection.sequences = sequenceEditor.sequences;
            }
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Edit Sequence", GUILayout.Height(50)))
            {
                SequenceEditWindow window = EditorWindow.GetWindow<SequenceEditWindow>(true);
                window.Init((RemoteLevel)target);
            }
        }

        private void OnDisable()
        {
            ExtractEssentialResources();
        }

        private void OnEnable()
        {
            ExtractEssentialResources();
        }

        void UnpackRecursively(SegmentSequence sequence)
        {
            if (sequence.isCustom && sequence.customSequence != null)
            {
                GameObject[] customSequencePrefabs = sequence.customSequence.GetAllSegments();
                for (int j = 0; j < customSequencePrefabs.Length; j++)
                {
                    GameObject go = Instantiate(customSequencePrefabs[j]);
                    LevelSegment ls = go.GetComponent<LevelSegment>();
                    if (ls != null) UnpackSegment(ls);
                }
                return;
            }
            for (int i = 0; i < sequence.segments.Length; i++)
            {
                if (sequence.segments[i].nested) UnpackRecursively(sequence.segments[i].nestedSequence);
                else
                {
                    LevelSegment ls = sequence.segments[i].Instantiate();
                    if (ls != null) UnpackSegment(ls);
                }
            }
        }

        void UnpackSegment(LevelSegment input)
        {
            RemoteLevel collection = (RemoteLevel)target;
            input.transform.position = Vector3.zero;
            input.transform.rotation = Quaternion.identity;
            input.transform.parent = collection.transform;
            input.EditorUnpack();
            if (input != null) DestroyImmediate(input);
        }

        void ExtractEssentialResources()
        {
            if (Application.isPlaying) return;
            RemoteLevel collection = (RemoteLevel)target;
            foreach (Transform child in collection.transform) DestroyImmediate(child.gameObject);
            for (int i = 0; i < collection.sequenceCollection.sequences.Length; i++) UnpackRecursively(collection.sequenceCollection.sequences[i]);
            List<Transform> children = new List<Transform>();
            SceneUtility.GetChildrenRecursively(collection.transform, ref children);
            List<Object> packedAssets = new List<Object>();

            for (int i = children.Count - 1; i >= 1; i--)
            {
                MeshFilter f = children[i].GetComponent<MeshFilter>();
                MeshRenderer r = children[i].GetComponent<MeshRenderer>();
                AudioSource a = children[i].GetComponent<AudioSource>();
                bool unique = false;
                if (f != null && f.sharedMesh != null)
                {
                    if (IsUnique(f.sharedMesh, ref packedAssets)) unique = true;
                }

                if (a != null)
                {
                    if (IsUnique(a.clip, ref packedAssets)) unique = true;
                }

                if (r != null)
                {
                    r.enabled = true;
                    for (int j = 0; j < r.sharedMaterials.Length; j++)
                    {
                        if (r.sharedMaterials[j] == null) continue;
                        if (AssetDatabase.Contains(r.sharedMaterials[j]))
                        {
                            if (IsUnique(r.sharedMaterials[j], ref packedAssets)) unique = true;

                            Shader shader = r.sharedMaterials[j].shader;
                            for (int k = 0; k < ShaderUtil.GetPropertyCount(shader); k++)
                            {
                                if (ShaderUtil.GetPropertyType(shader, k) == ShaderUtil.ShaderPropertyType.TexEnv)
                                {
                                    Texture texture = r.sharedMaterials[j].GetTexture(ShaderUtil.GetPropertyName(shader, k));
                                    if (texture != null)
                                    {
                                        if (IsUnique(texture, ref packedAssets)) unique = true;
                                    }
                                }
                            }
                        }
                    }
                }
                if (!unique) DestroyImmediate(children[i].gameObject);
                else
                {
                    children[i].parent = collection.transform;
                    children[i].localPosition = Vector3.zero;
                    children[i].localRotation = Quaternion.identity;
                    children[i].localScale = Vector3.one;
                    children[i].gameObject.SetActive(true);
                    Component[] components = children[i].GetComponents<Component>();
                    for (int j = 0; j < components.Length; j++)
                    {
                        if (components[j] is Transform) continue;
                        if (components[j] is MeshFilter || components[j] is Renderer || components[j] is AudioSource) continue;
                        DestroyImmediate(components[j]);
                    }
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        bool IsUnique(Object obj, ref List<Object> list)
        {
            if (list.Contains(obj)) return false;
            if (obj == null) return false;
            if (!AssetDatabase.Contains(obj)) return false;
            list.Add(obj);
            return true;
        }
    }
}
