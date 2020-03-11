
namespace Dreamteck.Forever.Editor
{
    using UnityEngine;
    using System.Collections;
    using UnityEditor;
    using System.Collections.Generic;
    using Splines;
#if UNITY_2018_3_OR_NEWER
    using UnityEditor.Experimental.SceneManagement;
#endif
#if DREAMTECK_SPLINES
    using Splines.Editor;
#endif


    [CustomEditor(typeof(LevelSegment), true)]
    [CanEditMultipleObjects]
    public class LevelSegmentEditor : Editor
    {
        public class PropertyEditWindow : EditorWindow
        {
            public List<int> selectedProperties = null;
            public LevelSegment segment = null;


            public void Init(Vector2 pos)
            {
                Rect newPos = position;
                newPos.x = pos.x - position.width;
                if (newPos.y < pos.y) newPos.y = pos.y;
                position = newPos;
            }

            private void Awake()
            {
                ExtrusionSettingsEditor.onWillChange += RecordUndo;
            }

            private void OnDestroy()
            {
                selectedProperties.Clear();
                ExtrusionSettingsEditor.onWillChange -= RecordUndo;
            }

            void RecordUndo()
            {
                Undo.RecordObject(segment, "Change Properties of " + segment.name);
            }

            private void OnGUI()
            {
                if (selectedProperties.Count == 0) return;
                if (selectedProperties.Count == 1) titleContent = new GUIContent(segment.objectProperties[selectedProperties[0]].transform.name + " - Extrusion Settings");
                else titleContent = new GUIContent("Multiple Objects - Extrusion Settings");
                GUILayout.BeginVertical();
                int overrideCount = 0;
                int settingsComponentCount = 0;
                ExtrusionSettings[] settings = new ExtrusionSettings[selectedProperties.Count];
                for (int i = 0; i < settings.Length; i++)
                {
                    settings[i] = segment.objectProperties[selectedProperties[i]].extrusionSettings;
                    if (segment.objectProperties[selectedProperties[i]].overrideSettingsComponent) overrideCount++;
                    if (segment.objectProperties[selectedProperties[i]].hasSettingsComponent) settingsComponentCount++;
                }

                if (selectedProperties.Count > 1)
                {
                    bool averageOverride = overrideCount == selectedProperties.Count;
                    bool lastOverride = averageOverride;
                    if (settingsComponentCount > 0)
                    {
                        EditorGUILayout.HelpBox("One or more of the objects have a Settings Component attached. The components' extrusion settings will be used.", MessageType.Info);
                        averageOverride = EditorGUILayout.Toggle("Override Settings Component", averageOverride);
                        if (lastOverride != averageOverride)
                        {
                            for (int i = 0; i < selectedProperties.Count; i++)
                            {
                                segment.objectProperties[selectedProperties[i]].overrideSettingsComponent = averageOverride;
                                segment.UpdateReferences();
                            }
                        }
                    }
                    if (averageOverride || settingsComponentCount == 0)
                    {
                        EditorGUILayout.Space();
                        if (ExtrusionSettingsEditor.Draw(settings)) segment.UpdateReferences();
                    }
                }
                else if (selectedProperties.Count == 1)
                {
                    if (settingsComponentCount > 0)
                    {
                        EditorGUILayout.HelpBox("Object has a Settings Component attached. The component's extrusion settings will be used.", MessageType.Info);
                        segment.objectProperties[selectedProperties[0]].overrideSettingsComponent = EditorGUILayout.Toggle("Override Settings Component", segment.objectProperties[selectedProperties[0]].overrideSettingsComponent);
                    }
                    if (segment.objectProperties[selectedProperties[0]].overrideSettingsComponent || !segment.objectProperties[selectedProperties[0]].hasSettingsComponent)
                    {
                        EditorGUILayout.Space();
                        if (ExtrusionSettingsEditor.Draw(settings[0])) segment.UpdateReferences();
                    }
                    if (GUILayout.Button("Select Object"))
                    {
                        Selection.activeGameObject = segment.objectProperties[selectedProperties[0]].transform.gameObject;
                        Close();
                    }

                    GUILayout.EndVertical();
                    Rect rect = GUILayoutUtility.GetLastRect();
                    if (rect.x + rect.height > position.height)
                    {
                        position = new Rect(position.x, position.y, position.width, rect.height + 10);
                    }
                }
            }
        }

        [InitializeOnLoad]
        internal static class PrefabStageCheck
        {
            internal static bool open = false;
            static PrefabStageCheck()
            {
#if UNITY_2018_3_OR_NEWER
                PrefabStage.prefabStageOpened -= OnStageOpen;
                PrefabStage.prefabStageOpened += OnStageOpen;
                PrefabStage.prefabStageClosing -= OnStageClose;
                PrefabStage.prefabStageClosing += OnStageClose;
#endif
            }

#if UNITY_2018_3_OR_NEWER
            static void OnStageOpen(PrefabStage stage)
            {
                open = true;
            }

            static void OnStageClose(PrefabStage stage)
            {
                open = false;
            }
#endif
        }

        internal class PropertyBinder
        {
            internal int index = 0;
            internal string name = "";

            internal PropertyBinder(int index, string name)
            {
                this.index = index;
                this.name = name;
            }
        }

        private bool showProperties = false;
        private bool showCustomPaths = false;
        UnityEditor.IMGUI.Controls.SearchField searchField = null;
        PropertyBinder[] properties = new PropertyBinder[0];
        private string propertyFilter = "";
        private PropertyEditWindow propertyWindow = null;
        List<int> selectedProperties = new List<int>();
        private string relativePath = "";
#if DREAMTECK_SPLINES
        private SplineComputer[] splines = new SplineComputer[0];
#endif

        private LevelSegment[] allSegments = new LevelSegment[0];
        private LevelSegment[] sceneSegments = new LevelSegment[0];
        private GUIStyle boxStyle = null;

        int selectedPath = -1;
        int renameCustomPath = -1;
        LevelSegmentCustomPathEditor pathEditor = null;
        int[] laneIndices = new int[0];
        string[] laneNames = new string[0];

        private bool debugFoldout = false;
        EditorGUIEvents input = new EditorGUIEvents();

        public static EditorWindow GetWindowByName(string pName)
        {
            Object[] objectList = Resources.FindObjectsOfTypeAll(typeof(EditorWindow));
            foreach (Object obj in objectList)
            {
                if (obj.GetType().ToString() == pName)
                    return ((EditorWindow)obj);
            }
            return null;
        }

        private void Awake()
        {
            GetSegments();
            if (Application.isPlaying) return;
            for (int i = 0; i < allSegments.Length; i++) allSegments[i].UpdateReferences();
            Undo.undoRedoPerformed += OnUndoRedo;
#if UNITY_2018_3_OR_NEWER
            PrefabStage.prefabStageClosing -= OnSavingPrefab;
            PrefabStage.prefabStageClosing += OnSavingPrefab;
#endif
        }

        private void OnDestroy()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            selectedPath = -1;
            pathEditor = null;
            LevelSegment segment = (LevelSegment)target;
        }

#if UNITY_2018_3_OR_NEWER
        void OnSavingPrefab(PrefabStage stage)
        {
            LevelSegment segment = stage.prefabContentsRoot.GetComponent<LevelSegment>();
            if (segment != null)
            {
                segment.EditorPack();
                PrefabUtility.SaveAsPrefabAsset(segment.gameObject, stage.prefabAssetPath);
            }
            PrefabStage.prefabStageClosing -= OnSavingPrefab;
        }
#endif

        void GetSegments()
        {
            if (allSegments.Length != targets.Length) allSegments = new LevelSegment[targets.Length];
            for (int i = 0; i < targets.Length; i++) allSegments[i] = (LevelSegment)targets[i];
            List<LevelSegment> sceneSegmentsList = new List<LevelSegment>();
            for (int i = 0; i < allSegments.Length; i++)
            {
#if UNITY_2018_3_OR_NEWER
                sceneSegmentsList.Add(allSegments[i]);
#else
                //In older versions of Unity, only use the objects in the scene
                if (IsSceneObject(allSegments[i].gameObject)){
                    sceneSegmentsList.Add(allSegments[i]);
                }
#endif
            }
            sceneSegments = sceneSegmentsList.ToArray();
            //Unpack the scene segments only
            if (!Application.isPlaying)
            {
                for (int i = 0; i < sceneSegments.Length; i++)
                {
                    if (!sceneSegments[i].unpacked) sceneSegments[i].EditorUnpack();
                }
            }

#if DREAMTECK_SPLINES
            List<SplineComputer> comps = new List<SplineComputer>();
            for (int i = 0; i < sceneSegments.Length; i++)
            {
                List<Transform> children = new List<Transform>();
                SceneUtility.GetChildrenRecursively(sceneSegments[i].transform, ref children);
                for (int j = 1; j < children.Count; j++)
                {
                    SplineComputer comp = children[j].GetComponent<SplineComputer>();
                    if (comp != null)
                    {
                        comps.Add(comp);
                        SplineDrawer.RegisterComputer(comp);
                    }
                }
            }
            splines = comps.ToArray();
#endif
        }

        bool IsSceneObject(GameObject obj)
        {
#if UNITY_2018_3_OR_NEWER
            Object parentObject = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            PrefabAssetType type = PrefabUtility.GetPrefabAssetType(obj);
            if(type == PrefabAssetType.Regular && type == PrefabAssetType.Variant) return false;
            if (parentObject == null) return true;
            return !AssetDatabase.Contains(parentObject);
#else
            PrefabType type = PrefabUtility.GetPrefabType(obj);
            if (type == PrefabType.Prefab) return false;
            return !AssetDatabase.Contains(obj);
#endif

        }

        int GetPropertyIndex(LevelSegment.ObjectProperty[] properties, LevelSegment.ObjectProperty property)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                if (property == properties[i]) return i;
            }
            return 0;
        }

        void ObjectPropertiesUI(PropertyBinder[] binders, LevelSegment.ObjectProperty[] properties)
        {
            input.Update();
            for (int i = 0; i < binders.Length; i++)
            {
                LevelSegment.ObjectProperty property = properties[binders[i].index];
                if (selectedProperties.Contains(binders[i].index))
                {
                    GUI.backgroundColor = ForeverPrefs.highlightColor;
                    GUI.contentColor = ForeverPrefs.highlightContentColor;
                }
                else
                {
                    if (property.extrusionSettings.ignore) GUI.backgroundColor = Color.gray;
                    else GUI.backgroundColor = DreamteckEditorGUI.lightColor;
                    GUI.contentColor = new Color(1f, 1f, 1f, 0.8f);
                }
                GUILayout.BeginVertical(boxStyle);
                EditorGUILayout.LabelField(i + "   " + binders[i].name);
                GUILayout.EndVertical();
                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.width -= 30;
                if (lastRect.Contains(Event.current.mousePosition) && input.mouseLeft)
                {
                    if (Event.current.shift)
                    {
                        if (selectedProperties.Count == 0) selectedProperties.Add(binders[i].index);
                        else
                        {
                            if (i < selectedProperties[0])
                            {
                                for (int n = selectedProperties[0] - 1; n >= i; n--)
                                {
                                    if (!selectedProperties.Contains(binders[n].index)) selectedProperties.Add(binders[n].index);
                                }
                            }
                            else
                            {
                                for (int n = selectedProperties[0] + 1; n <= i; n++)
                                {
                                    if (!selectedProperties.Contains(binders[n].index)) selectedProperties.Add(binders[n].index);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Event.current.control)
                        {
                            if (!selectedProperties.Contains(binders[i].index)) selectedProperties.Add(binders[i].index);
                        }
                        else
                        {
                            selectedProperties.Clear();
                            selectedProperties.Add(binders[i].index);
                        }

                    }
                    Repaint();
                    if (propertyWindow != null) propertyWindow.Repaint();
                    SceneView.RepaintAll();
                }
                lastRect.width += 30;
            }
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
#if DREAMTECK_SPLINES
            for (int i = 0; i < splines.Length; i++)
            {
                if (splines[i] != null) SplineDrawer.UnregisterComputer(splines[i]);
            }
#endif
            if (propertyWindow != null) propertyWindow.Close();
        }

        private bool IsObjectPrefabInstance(GameObject obj)
        {
#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.GetPrefabInstanceStatus(obj) == PrefabInstanceStatus.Connected;
#else
            return PrefabUtility.GetPrefabType(obj) == PrefabType.PrefabInstance;
#endif
        }

        private void WritePrefabs(bool forceCopy = false)
        {
            for (int i = 0; i < allSegments.Length; i++) Write(allSegments[i], forceCopy);
        }

        void Write(LevelSegment segment, bool forceCopy)
        {
            //Check to see if we are currently editing the prefab and if yes (2018.3), just pack everything without rewriting
            bool isPrefabInstance = false;
            Object prefabParent = null;
#if UNITY_2018_3_OR_NEWER
            PrefabInstanceStatus instanceStatus = PrefabUtility.GetPrefabInstanceStatus(segment.gameObject);
            isPrefabInstance = instanceStatus == PrefabInstanceStatus.Connected;
             if (isPrefabInstance) prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(segment.gameObject);
#else

            PrefabType prefabType = PrefabUtility.GetPrefabType(segment.gameObject);
            isPrefabInstance = prefabType == PrefabType.PrefabInstance;
            if (isPrefabInstance) prefabParent = PrefabUtility.GetPrefabParent(segment.gameObject);
#endif

            if (!forceCopy && prefabParent != null)
            {
                segment.EditorPack();
#if DREAMTECK_SPLINES
                for (int i = 0; i < splines.Length; i++)
                {
                    if (splines[i] != null)
                    {
                        SplineDrawer.UnregisterComputer(splines[i]);
                    }
                }
#endif
#if UNITY_2018_3_OR_NEWER
                Selection.activeGameObject = PrefabUtility.SaveAsPrefabAsset(segment.gameObject, AssetDatabase.GetAssetPath(prefabParent));
#else
                PrefabUtility.ReplacePrefab(segment.gameObject, prefabParent, ReplacePrefabOptions.ConnectToPrefab);
#endif
                Undo.DestroyObjectImmediate(segment.gameObject);
            }
            else
            {
                relativePath = EditorPrefs.GetString("LevelSegmentEditor.relativePath", "/");
                if (prefabParent != null)
                {
                    relativePath = AssetDatabase.GetAssetPath(prefabParent);
                    if(relativePath.StartsWith("Assets")) relativePath = relativePath.Substring("Assets".Length);
                    relativePath = System.IO.Path.GetDirectoryName(relativePath);
                }
                string path = EditorUtility.SaveFilePanel("Save Prefab", Application.dataPath + relativePath, segment.name, "prefab");
                if (path.StartsWith(Application.dataPath) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                {
                    relativePath = path.Substring(Application.dataPath.Length);
                    segment.EditorPack();
#if DREAMTECK_SPLINES
                    for (int i = 0; i < splines.Length; i++)
                    {
                        if (splines[i] != null)
                        {
                            SplineDrawer.UnregisterComputer(splines[i]);
                        }
                    }
#endif
#if UNITY_2018_3_OR_NEWER
                    if (isPrefabInstance) PrefabUtility.UnpackPrefabInstance(segment.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    PrefabUtility.SaveAsPrefabAsset(segment.gameObject, "Assets" + relativePath);
#else
                    if (isPrefabInstance) PrefabUtility.DisconnectPrefabInstance(segment.gameObject);
                    PrefabUtility.CreatePrefab("Assets" + relativePath, segment.gameObject);
#endif
                    Undo.DestroyObjectImmediate(segment.gameObject);
                    EditorPrefs.SetString("LevelSegmentEditor.relativePath", System.IO.Path.GetDirectoryName(relativePath));
                } else
                {
                    if (path != "" && !path.StartsWith(Application.dataPath)) EditorUtility.DisplayDialog("Path Error", "Please select a path inside this project's Assets folder", "OK");
                }
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.GetStyle("box"));
                boxStyle.normal.background = DreamteckEditorGUI.blankImage;
                boxStyle.margin = new RectOffset(0, 0, 0, 2);
            }

            string saveText = "Save";
            string saveAsText = "Save As";
            if (allSegments.Length > 1)
            {
                saveText = "Save All";
                saveAsText = "Save All As";
            }
            if (sceneSegments.Length > 0 && !Application.isPlaying && !PrefabStageCheck.open)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(saveText, GUILayout.Height(40)))
                {
                    WritePrefabs();
                    return;
                }
                if (GUILayout.Button(saveAsText, GUILayout.Height(40), GUILayout.Width(70)))
                {
                    WritePrefabs(true);
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (allSegments.Length > 1)
            {
                EditorGUILayout.HelpBox("Property editing unavailable with multiple selection", MessageType.Info);
                return;
            }

            LevelSegment segment = (LevelSegment)target;

            segment.type = (LevelSegment.Type)EditorGUILayout.EnumPopup("Type", segment.type);
            if (segment.type == LevelSegment.Type.Extruded)
            {
                segment.axis = (LevelSegment.Axis)EditorGUILayout.EnumPopup("Extrude Axis", segment.axis);
                ExtrusionUI();
            } else
            {
                EditorGUILayout.BeginHorizontal();
                segment.customEntrance = (Transform)EditorGUILayout.ObjectField("Entrance", segment.customEntrance, typeof(Transform), true);
                if (segment.customEntrance == null)
                {
                    if (GUILayout.Button("Create", GUILayout.Width(50)))
                    {
                        GameObject go = new GameObject("Entrance");
                        go.transform.parent = segment.transform;
                        segment.customEntrance = go.transform;
                    }
                }
                else if (!IsChildOrSubchildOf(segment.customEntrance, segment.transform))
                {
                    Debug.LogError(segment.customEntrance.name + " must be a child of " + segment.name);
                    segment.customEntrance = null;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                segment.customExit = (Transform)EditorGUILayout.ObjectField("Exit", segment.customExit, typeof(Transform), true);
                if (segment.customExit == null)
                {
                    if (GUILayout.Button("Create", GUILayout.Width(50)))
                    {
                        GameObject go = new GameObject("Exit");
                        go.transform.parent = segment.transform;
                        segment.customExit = go.transform;
                    }
                }
                else if (!IsChildOrSubchildOf(segment.customExit, segment.transform))
                {
                    Debug.LogError(segment.customExit.name + " must be a child of " + segment.name);
                    segment.customExit = null;
                }
                EditorGUILayout.EndHorizontal();
                segment.customKeepUpright = EditorGUILayout.Toggle("Keep Upright", segment.customKeepUpright);
            }
            EditorGUILayout.Space();

            int childCount = 0;
            TransformUtility.GetChildCount(segment.transform, ref childCount);
            if (segment.editorChildCount != childCount && !Application.isPlaying)
            {
                segment.UpdateReferences();
                selectedProperties.Clear();
            }

            CustomPathUI();
            
            EditorGUILayout.Space();
            debugFoldout = EditorGUILayout.Foldout(debugFoldout, "Debug");
            if (debugFoldout)
            {
                if (!Application.isPlaying) segment.drawBounds = EditorGUILayout.Toggle("Draw Bounds", segment.drawBounds);
                if (segment.type == LevelSegment.Type.Custom) segment.drawEntranceAndExit = EditorGUILayout.Toggle("Draw Entrance / Exit", segment.drawEntranceAndExit);
                segment.drawGeneratedSpline = EditorGUILayout.Toggle("Draw Generated Points", segment.drawGeneratedSpline);
                segment.drawGeneratedSamples = EditorGUILayout.Toggle("Draw Generated Samples", segment.drawGeneratedSamples);
                if (segment.drawGeneratedSamples)
                {
                    EditorGUI.indentLevel++;
                    segment.drawSampleScale = EditorGUILayout.FloatField("Sample Scale", segment.drawSampleScale);
                    EditorGUI.indentLevel--;
                }
                segment.drawCustomPaths = EditorGUILayout.Toggle("Draw Custom Paths", segment.drawCustomPaths);
                EditorGUILayout.HelpBox(segment.GetBounds().size.ToString(), MessageType.Info);
            }
        }

        void CustomPathUI()
        {
            LevelSegment segment = (LevelSegment)target;
            showCustomPaths = EditorGUILayout.Foldout(showCustomPaths, "Custom Paths (" + segment.customPaths.Length + ")");
            if (showCustomPaths)
            {
                Undo.RecordObject(segment, "Edit Custom Paths");
                if (segment.type == LevelSegment.Type.Custom)
                {
                    if(laneIndices.Length != segment.customPaths.Length + 1)
                    {
                        laneIndices = new int[segment.customPaths.Length + 1];
                        laneNames = new string[segment.customPaths.Length + 1];
                    }
                    laneIndices[0] = -1;
                    laneNames[0] = "None";
                    for (int i = 0; i < segment.customPaths.Length; i++)
                    {
                        laneNames[i + 1] = (i + 1) + " - " + segment.customPaths[i].name;
                        laneIndices[i + 1] = i;
                    }
                    segment.customMainPath = EditorGUILayout.IntPopup("Main Path", segment.customMainPath, laneNames, laneIndices);
                }

                input.Update();
                GUI.backgroundColor = DreamteckEditorGUI.lightColor;
                for (int i = 0; i < segment.customPaths.Length; i++)
                {
                    GUILayout.BeginVertical(boxStyle);
                    EditorGUILayout.BeginHorizontal();
                    segment.customPaths[i].color = EditorGUILayout.ColorField(segment.customPaths[i].color, GUILayout.Width(40));
                    if (renameCustomPath == i)
                    {
                        if (input.enterDown)
                        {
                            input.Use();
                            renameCustomPath = -1;
                        }
                        segment.customPaths[i].name = EditorGUILayout.TextField(segment.customPaths[i].name);
                    }
                    else
                    {
                        GUIStyle style = i == segment.customMainPath ? EditorStyles.boldLabel : EditorStyles.label;
                        EditorGUILayout.LabelField(segment.customPaths[i].name, style);
                    }
                    EditorGUILayout.EndHorizontal();
                    Rect lastRect = GUILayoutUtility.GetLastRect();

                    if (input.mouseRightDown)
                    {
                        if (lastRect.Contains(Event.current.mousePosition))
                        {
                            int index = i;
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Close"), false, delegate { selectedPath = -1; pathEditor = null; Repaint(); SceneView.RepaintAll(); });
                            menu.AddItem(new GUIContent("Rename"), false, delegate { renameCustomPath = index; Repaint(); SceneView.RepaintAll(); });
                            menu.AddItem(new GUIContent("Duplicate"), false, delegate { ArrayUtility.Insert(ref segment.customPaths, index + 1, segment.customPaths[index].Copy()); Repaint(); SceneView.RepaintAll(); });
                            menu.AddSeparator("");
                            if (i == 0) menu.AddDisabledItem(new GUIContent("Move Up"));
                            else menu.AddItem(new GUIContent("Move Up"), false, delegate {
                                LevelSegment.LevelSegmentPath temp = segment.customPaths[index];
                                segment.customPaths[index] = segment.customPaths[index - 1];
                                segment.customPaths[index - 1] = temp;
                                if (selectedPath == index) selectedPath--;
                                Repaint();
                                SceneView.RepaintAll();
                            });
                            if (i == segment.customPaths.Length - 1) menu.AddDisabledItem(new GUIContent("Move Down"));
                            else menu.AddItem(new GUIContent("Move Down"), false, delegate {
                                LevelSegment.LevelSegmentPath temp = segment.customPaths[index];
                                segment.customPaths[index] = segment.customPaths[index + 1];
                                segment.customPaths[index + 1] = temp;
                                if (selectedPath == index) selectedPath++;
                                Repaint();
                                SceneView.RepaintAll();
                            });
                            menu.AddSeparator("");
                            menu.AddItem(new GUIContent("Delete"), false, delegate { segment.RemoveCustomPath(index); selectedPath = -1; pathEditor = null; Repaint(); SceneView.RepaintAll(); });
                            menu.ShowAsContext();
                        }
                    }
                    if (selectedPath == i && pathEditor != null)
                    {
                        EditorGUILayout.Space();
                        pathEditor.DrawInspector();
                    }
                    GUILayout.EndVertical();
                    lastRect = GUILayoutUtility.GetLastRect();

                    if (input.mouseLeftDown)
                    {
                        if (lastRect.Contains(Event.current.mousePosition))
                        {
                            selectedPath = i;
                            pathEditor = new LevelSegmentCustomPathEditor(this, segment, segment.customPaths[i]);
                            Repaint();
                            SceneView.RepaintAll();
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
                if(GUILayout.Button("Add Path"))
                {
                    segment.AddCustomPath("Lane " + (segment.customPaths.Length + 1));
                    Repaint();
                    SceneView.RepaintAll();
                }
            } else
            {
                renameCustomPath = -1;
                selectedPath = -1;
                if (pathEditor != null) pathEditor.Close();
                pathEditor = null;
            }
        }

        void ExtrusionUI()
        {
            LevelSegment segment = (LevelSegment)target;
            showProperties = EditorGUILayout.Foldout(showProperties, "Objects (" + segment.objectProperties.Length + ")");
            if (showProperties)
            {
                GUI.color = Color.clear;
                GUILayout.Box("", GUILayout.Width(Screen.width - 50));
                GUI.color = Color.white;
                if (searchField == null) searchField = new UnityEditor.IMGUI.Controls.SearchField();
                string lastFilter = propertyFilter;
                propertyFilter = searchField.OnGUI(GUILayoutUtility.GetLastRect(), propertyFilter);
                if (lastFilter != propertyFilter)
                {
                    List<PropertyBinder> found = new List<PropertyBinder>();
                    for (int i = 0; i < segment.objectProperties.Length; i++)
                    {
                        if (segment.objectProperties[i].transform.name.ToLower().Contains(propertyFilter.ToLower())) found.Add(new PropertyBinder(i, segment.objectProperties[i].transform.name));
                    }
                    properties = found.ToArray();
                }
                else if (propertyFilter == "")
                {
                    if (properties.Length != segment.objectProperties.Length) properties = new PropertyBinder[segment.objectProperties.Length];
                    for (int i = 0; i < segment.objectProperties.Length; i++)
                    {
                        if(properties[i] == null) properties[i] = new PropertyBinder(i, segment.objectProperties[i].transform.name);
                        else
                        {
                            properties[i].name = segment.objectProperties[i].transform.name;
                            properties[i].index = i;
                        }
                    }
                }

                if (selectedProperties.Count > 0)
                {
                    if (propertyWindow == null)
                    {
                        propertyWindow = EditorWindow.GetWindow<PropertyEditWindow>(true);
                        propertyWindow.segment = segment;
                        propertyWindow.selectedProperties = selectedProperties;
                        EditorWindow inspectorWindow = GetWindowByName("UnityEditor.InspectorWindow");
                        if (inspectorWindow != null) propertyWindow.Init(new Vector2(inspectorWindow.position.x, inspectorWindow.position.y + 250));
                        else propertyWindow.Init(new Vector2(2560 - Screen.width, 1080 / 2));
                    }
                }
                ObjectPropertiesUI(properties, segment.objectProperties);
                if (selectedProperties.Count > 0)
                {
                    if (Event.current.type == EventType.KeyDown)
                    {
                        if (Event.current.keyCode == KeyCode.DownArrow)
                        {
                            if (selectedProperties.Count > 1)
                            {
                                int temp = selectedProperties[selectedProperties.Count - 1];
                                selectedProperties.Clear();
                                selectedProperties.Add(temp);
                            }
                            selectedProperties[0]++;
                        }
                        if (Event.current.keyCode == KeyCode.UpArrow)
                        {
                            if (selectedProperties.Count > 1)
                            {
                                int temp = selectedProperties[0];
                                selectedProperties.Clear();
                                selectedProperties.Add(temp);
                            }
                            selectedProperties[0]--;
                        }
                        if (selectedProperties[0] < 0) selectedProperties[0] = 0;
                        if (selectedProperties[0] >= segment.objectProperties.Length) selectedProperties[0] = segment.objectProperties.Length - 1;
                        Repaint();
                        if (propertyWindow != null) propertyWindow.Repaint();
                        SceneView.RepaintAll();
                        Event.current.Use();
                    }
                }
                else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
                {
                    selectedProperties.Clear();
                    selectedProperties.Add(0);
                }
                GUI.color = Color.white;
            }
        }

        bool IsChildOrSubchildOf(Transform child, Transform parent)
        {
            Transform current = child;
            while (current.parent != null)
            {
                if (current.parent == parent) return true;
                current = current.parent;
            }
            return false;
        }

        void OnSceneGUI()
        {
#if DREAMTECK_SPLINES
            for (int i = 0; i < splines.Length; i++)
            {
                if (splines[i] != null) SplineDrawer.DrawSplineComputer(splines[i]);
            }
#endif

            if (Application.isPlaying)
            {
                for (int i = 0; i < sceneSegments.Length; i++)
                {
                    if (sceneSegments[i].drawCustomPaths) LevelSegmentDebug.DrawCustomPaths(sceneSegments[i]);
                    if (sceneSegments[i].drawGeneratedSpline) LevelSegmentDebug.DrawGeneratedSpline(sceneSegments[i]);
                    if (sceneSegments[i].drawGeneratedSamples) LevelSegmentDebug.DrawGeneratedSamples(sceneSegments[i]);
                }
            }
            else
            {
                for (int i = 0; i < sceneSegments.Length; i++)
                {
                    if (sceneSegments[i].drawCustomPaths) LevelSegmentDebug.DrawCustomPaths(sceneSegments[i]);
                    if (sceneSegments[i].type == LevelSegment.Type.Custom) continue;
                    if (sceneSegments[i].drawBounds) LevelSegmentDebug.DrawBounds(sceneSegments[i]);
                }

                if (sceneSegments.Length == 1 && selectedProperties.Count > 0)
                {
                    Handles.BeginGUI();
                    for (int i = 0; i < selectedProperties.Count; i++)
                    {
                        Vector2 screenPosition = HandleUtility.WorldToGUIPoint(sceneSegments[0].objectProperties[selectedProperties[i]].transform.transform.position);
                        DreamteckEditorGUI.Label(new Rect(screenPosition.x - 120 + sceneSegments[0].objectProperties[selectedProperties[i]].transform.transform.name.Length * 4, screenPosition.y, 120, 25), sceneSegments[0].objectProperties[selectedProperties[i]].transform.transform.name);
                    }
                    Handles.EndGUI();
                }
            }
            if (pathEditor != null) pathEditor.DrawScene();

            for (int i = 0; i < sceneSegments.Length; i++)
            {
                if (!sceneSegments[i].drawEntranceAndExit) continue;
                if(sceneSegments[i].type == LevelSegment.Type.Custom)
                {
                    if (sceneSegments[i].customEntrance != null)
                    {
                        float handleSize = HandleUtility.GetHandleSize(sceneSegments[i].customEntrance.position);
                        Handles.color = ForeverPrefs.entranceColor;
                        Handles.DrawSolidDisc(sceneSegments[i].customEntrance.position, Camera.current.transform.position - sceneSegments[i].customEntrance.position, handleSize * 0.1f);
                        Handles.ArrowHandleCap(0, sceneSegments[i].customEntrance.position, sceneSegments[i].customEntrance.rotation, handleSize * 0.5f, EventType.Repaint);
                        Handles.Label(sceneSegments[i].customEntrance.position + Camera.current.transform.up * handleSize * 0.3f, "Entrance");
                    }
                    if (sceneSegments[i].customExit != null)
                    {
                        Handles.color = ForeverPrefs.exitColor;
                        float handleSize = HandleUtility.GetHandleSize(sceneSegments[i].customExit.position);
                        Handles.DrawSolidDisc(sceneSegments[i].customExit.position, Camera.current.transform.position - sceneSegments[i].customExit.position, handleSize * 0.1f);
                        Handles.ArrowHandleCap(0, sceneSegments[i].customExit.position, sceneSegments[i].customExit.rotation, handleSize * 0.5f, EventType.Repaint);
                        Handles.Label(sceneSegments[i].customExit.position + Camera.current.transform.up * HandleUtility.GetHandleSize(sceneSegments[i].customExit.position) * 0.3f, "Exit");
                    } 
                }
            }
        }

    }
}
