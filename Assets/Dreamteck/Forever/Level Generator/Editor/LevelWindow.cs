using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace Dreamteck.Forever {
    public class LevelWindow : EditorWindow
    {
        internal class WindowPanel
        {
            internal float width = 0f;
            internal float height = 0f;
            internal string title = "";
            private static float currentY = 0f;
            private LevelWindow window = null;
            private static GUIStyle defaultBoxStyle = null;
            public delegate void EmptyHandler();
            private EmptyHandler uiFunction;

            internal static void Reset()
            {
                currentY = 0f;
            }

            internal WindowPanel(float w, float h, string t, LevelWindow windowRef, EmptyHandler uiFunction)
            {
                width = w;
                height = h;
                title = t;
                window = windowRef;
                defaultBoxStyle = new GUIStyle(GUI.skin.box);
                defaultBoxStyle.normal.background = DreamteckEditorGUI.blankImage;
                defaultBoxStyle.margin = new RectOffset(0, 0, 0, 0);
                this.uiFunction = uiFunction;
            }

            internal static Rect DrawPanel(WindowPanel panel)
            {
                Rect drawRect = new Rect(panel.window.position.width - panel.width - 5f, currentY, panel.width, panel.height);
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = DreamteckEditorGUI.lightColor;
                GUI.Box(drawRect, panel.title, defaultBoxStyle);
                currentY += drawRect.height;
                drawRect.y += 22;
                drawRect.height -= 22;
                drawRect.x += 5;
                drawRect.width -= 10;
                GUILayout.BeginArea(drawRect);
                panel.uiFunction();
                GUILayout.EndArea();
                GUI.backgroundColor = prevColor;
                return drawRect;
            }
        }

        private LevelGenerator generator;
        private int levelIndex;


        private Texture2D white;

        WindowPanel settingsPanel;

        SegmentSequenceEditor sequenceEditor = null;
        private string[] sceneNames = new string[0];

        public void Init(LevelGenerator gen, int index)
        {
            generator = gen;
            levelIndex = index;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            settingsPanel = new WindowPanel(200, 9000f, "Level Settings", this, SettingsUI);
            sequenceEditor = new SegmentSequenceEditor(generator, generator.levels[levelIndex].sequenceCollection, position);
            sequenceEditor.onWillChange -= RecordUndo;
            sequenceEditor.onChanged -= OnChanged;
            sequenceEditor.onApplySequences -= OnApplySequences;
            sequenceEditor.onWillChange += RecordUndo;
            sequenceEditor.onChanged += OnChanged;
            sequenceEditor.onApplySequences += OnApplySequences;
            titleContent = new GUIContent("Level Window - " + generator.levels[levelIndex].title);
        }

        void OnChanged()
        {
            Repaint();
        }

        void RecordUndo()
        {
            Undo.RecordObject(generator, "Edit Segment Collection");
        }

        void OnApplySequences(SegmentSequence[] sequences)
        {
            generator.levels[levelIndex].sequenceCollection.sequences = sequences;
        }

        void OnGUI()
        {
            if (generator == null)
            {
                Close();
                return;
            }
            if(sequenceEditor == null)
            {
                Init(generator, levelIndex);
                return;
            }
            EditorGUI.BeginChangeCheck();

            if (levelIndex >= generator.levels.Length)
            {
                Close();
                return;
            }

            if (generator.levels[levelIndex].remoteSequence)
            {
                minSize = new Vector2(210, 300);
                maxSize = new Vector2(210, 9000);
            }
            else
            {
                minSize = new Vector2(600, 600);
                maxSize = new Vector2(9000, 9000);
                sequenceEditor.viewRect = new Rect(5, 5, position.width - 208, position.height - 20);
                sequenceEditor.windowPosition = new Vector2(position.x, position.y);
                sequenceEditor.sequences = generator.levels[levelIndex].sequenceCollection.sequences;
                sequenceEditor.DrawEditor();
                generator.levels[levelIndex].sequenceCollection.sequences = sequenceEditor.sequences;
            }

            WindowPanel.Reset();
            WindowPanel.DrawPanel(settingsPanel);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Application.isPlaying)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                }
            }
        }

        void SettingsUI()
        {
            EditorGUI.BeginChangeCheck();
            generator.levels[levelIndex].enabled = EditorGUILayout.Toggle("Enabled", generator.levels[levelIndex].enabled);
            EditorGUIUtility.labelWidth = 50;
            generator.levels[levelIndex].title = EditorGUILayout.TextField("Title", generator.levels[levelIndex].title);
            EditorGUIUtility.labelWidth = 120;

            GUILayout.Label("Resources");
            generator.levels[levelIndex].remoteSequence = EditorGUILayout.Toggle("Remote Sequence", generator.levels[levelIndex].remoteSequence);
            if (generator.levels[levelIndex].remoteSequence)
            {
                EditorGUIUtility.labelWidth = 70;
                if (sceneNames.Length != EditorBuildSettings.scenes.Length + 1) sceneNames = new string[EditorBuildSettings.scenes.Length + 1];
                int sceneIndex = 0;
                sceneNames[0] = "NONE";
                for (int i = 1; i < sceneNames.Length; i++)
                {
                    sceneNames[i] = System.IO.Path.GetFileNameWithoutExtension(EditorBuildSettings.scenes[i - 1].path);
                    if (generator.levels[levelIndex].remoteSceneName == sceneNames[i]) sceneIndex = i;
                }
                EditorGUIUtility.labelWidth = 80;
                sceneIndex = EditorGUILayout.Popup("Scene", sceneIndex, sceneNames);
                if (sceneIndex == 0) generator.levels[levelIndex].remoteSceneName = "";
                else generator.levels[levelIndex].remoteSceneName = sceneNames[sceneIndex];
                if (sceneIndex > 0)
                {
                    generator.levels[levelIndex].loadingPriority = (ThreadPriority)EditorGUILayout.EnumPopup("Loading Priority", generator.levels[levelIndex].loadingPriority);
                    if (GUILayout.Button("Go To Scene"))
                    {
                        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                        {
                            if (System.IO.Path.GetFileNameWithoutExtension(EditorBuildSettings.scenes[i].path) == generator.levels[levelIndex].remoteSceneName)
                            {
                                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes[i].path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                                break;
                            }
                        }
                    }
                }
                EditorGUIUtility.labelWidth = 0;
            }
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(generator);
        }

        void ReorderSegments(ref SegmentDefinition[] segments, int index, int targetIndex)
        {
            SegmentDefinition[] newSegments = new SegmentDefinition[segments.Length];
            if (index == targetIndex) return;
            if (targetIndex < index)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    if (i == targetIndex) newSegments[i] = segments[index];
                    else if (i < targetIndex) newSegments[i] = segments[i];
                    else if (i <= index) newSegments[i] = segments[i - 1];
                    else newSegments[i] = segments[i];
                }
            }
            else
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    if (i == targetIndex - 1) newSegments[i] = segments[index];
                    else if (i < index) newSegments[i] = segments[i];
                    else if (i < targetIndex) newSegments[i] = segments[i + 1];
                    else newSegments[i] = segments[i];

                }
            }
            segments = newSegments;
            EditorUtility.SetDirty(generator);
        }

        void InsertSegment(ref SegmentDefinition[] segments, int index, SegmentDefinition insert)
        {
            SegmentDefinition[] newSegments = new SegmentDefinition[segments.Length + 1];
            for (int i = 0; i < newSegments.Length; i++)
            {
                if (i < index) newSegments[i] = segments[i];
                else if (i == index) newSegments[i] = insert;
                else newSegments[i] = segments[i - 1];
            }
            segments = newSegments;
            EditorUtility.SetDirty(generator);
        }

        void RemoveSegment(ref SegmentDefinition[] segments, int index)
        {
            SegmentDefinition[] newSegments = new SegmentDefinition[segments.Length - 1];
            for (int i = 0; i < newSegments.Length; i++)
            {
                if (i < index) newSegments[i] = segments[i];
                else newSegments[i] = segments[i + 1];
            }
            segments = newSegments;
            EditorUtility.SetDirty(generator);
        }

        void OnDestroy()
        {
            //if (objWindow != null) objWindow.Close();
        }
    }
}
