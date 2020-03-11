namespace Dreamteck.Forever.Editor
{
    using UnityEngine;
    using System.Collections;
    using UnityEditor;
    using System.Collections.Generic;
    using UnityEngine.UI;

    [CustomEditor(typeof(LevelGenerator))]
    public class LevelGeneratorEditor : Editor
    {
        private int levelIndex = -1;
        private GUIStyle boxStyle = null;
        private bool levelFoldout = false;
        LevelWindow levelWindow = null;


        private void OnEnable()
        {
            levelFoldout = EditorPrefs.GetBool("Dreamteck.Forever.LevelEditor.levelFoldout", levelFoldout);
        }

        private void OnDisable()
        {
             EditorPrefs.SetBool("Dreamteck.Forever.LevelEditor.levelFoldout", levelFoldout);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            LevelGenerator gen = (LevelGenerator)target;
            Undo.RecordObject(gen, "Edit Level Generator");
            gen.type = (LevelGenerator.Type)EditorGUILayout.EnumPopup("Type", gen.type);
            if (gen.type == LevelGenerator.Type.Finite)
            {
                gen.finiteSegmentsCount = EditorGUILayout.IntField("Segments Count", gen.finiteSegmentsCount);
                gen.finiteLoop = EditorGUILayout.Toggle("Loop", gen.finiteLoop);
            }
            else
            {
                gen.maxSegments = EditorGUILayout.IntField("Max. Segments", gen.maxSegments);
                if (gen.maxSegments < 1) gen.maxSegments = 1;
                gen.generateSegmentsAhead = EditorGUILayout.IntField("Generate Segments Ahead", gen.generateSegmentsAhead);
                if(gen.generateSegmentsAhead < 1) gen.generateSegmentsAhead = 1;
                if (gen.generateSegmentsAhead > gen.maxSegments) gen.generateSegmentsAhead = gen.maxSegments;
                gen.activateSegmentsAhead = EditorGUILayout.IntField("Activate Segments Ahead", gen.activateSegmentsAhead);
                if (gen.activateSegmentsAhead < 0) gen.activateSegmentsAhead = 0;
                if (gen.activateSegmentsAhead > gen.generateSegmentsAhead) gen.activateSegmentsAhead = gen.generateSegmentsAhead;
            }

            EditorGUILayout.Space();

            gen.pathGenerator = (LevelPathGenerator)EditorGUILayout.ObjectField("Path Generator", gen.pathGenerator, typeof(LevelPathGenerator), false);
            if (gen.pathGenerator != null)
            {
                EditorGUI.indentLevel++;
                gen.usePathGeneratorInstance = EditorGUILayout.Toggle("Create Instance", gen.usePathGeneratorInstance);
                EditorGUI.indentLevel--;
            } else
            {
                EditorGUILayout.HelpBox("A Path Generator needs to be assigned to the Level Generator.", MessageType.Error);
            }

            if (Event.current.type == EventType.KeyDown && gen.levels.Length > 0)
            {
                if(Event.current.keyCode == KeyCode.DownArrow)
                {
                    levelIndex++;
                    if (levelIndex >= gen.levels.Length) levelIndex = -1;
                    levelWindow = EditorWindow.GetWindow<LevelWindow>(true);
                    levelWindow.Init(gen, levelIndex);
                    Repaint();
                    Event.current.Use();
                }
                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    levelIndex--;
                    if (levelIndex < -1) levelIndex = gen.levels.Length-1;
                    levelWindow = EditorWindow.GetWindow<LevelWindow>(true);
                    levelWindow.Init(gen, levelIndex);
                    Repaint();
                    Event.current.Use();
                }
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.GetStyle("box"));
                boxStyle.normal.background = DreamteckEditorGUI.blankImage;
                boxStyle.margin = new RectOffset(0, 0, 0, 2);
            }
            EditorGUILayout.Space();
            levelFoldout = EditorGUILayout.Foldout(levelFoldout, "Levels");
            if (levelFoldout)
            {
                gen.levelIteration = (LevelGenerator.LevelIteration)EditorGUILayout.EnumPopup("Level Iteration", gen.levelIteration);
                gen.startLevel = EditorGUILayout.IntField("Start Level", gen.startLevel);
                ListLevels();
            }

            if (gen.levels.Length == 0) EditorGUILayout.HelpBox("No defined levels. Define at least one level.", MessageType.Error);

            EditorGUILayout.Space();
            gen.testMode = EditorGUILayout.Toggle("Test Mode", gen.testMode);
            if (gen.testMode)
            {
                for (int i = 0; i < gen.debugSegments.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    gen.debugSegments[i] = (GameObject)EditorGUILayout.ObjectField("Segment " + (i + 1), gen.debugSegments[i], typeof(GameObject), false);
                    if (GUILayout.Button("x", GUILayout.Width(30f)) || gen.debugSegments[i] == null)
                    {
                        GameObject[] newSegments = new GameObject[gen.debugSegments.Length - 1];
                        for (int n = 0; n < gen.debugSegments.Length; n++)
                        {
                            if (n < i) newSegments[n] = gen.debugSegments[n];
                            else if (n > i) newSegments[n - 1] = gen.debugSegments[n];
                        }
                        gen.debugSegments = newSegments;
                        i--;
                        EditorGUILayout.EndHorizontal();
                        continue;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                GameObject newSegment = null;
                newSegment = (GameObject)EditorGUILayout.ObjectField("Add debug segment", newSegment, typeof(GameObject), false);
                if (newSegment != null)
                {
                    if (newSegment.GetComponent<LevelSegment>() != null)
                    {
                        GameObject[] newSegments = new GameObject[gen.debugSegments.Length + 1];
                        gen.debugSegments.CopyTo(newSegments, 0);
                        newSegments[newSegments.Length - 1] = newSegment;
                        gen.debugSegments = newSegments;
                    }
                }
            }
        }

        void MoveDown(int index)
        {
            LevelGenerator generator = (LevelGenerator)target;
            Level temp = generator.levels[index + 1];
            generator.levels[index + 1] = generator.levels[index];
            generator.levels[index] = temp;
            levelIndex++;
            EditorUtility.SetDirty(generator);
            Repaint();
        }

        void MoveUp(int index)
        {
            LevelGenerator generator = (LevelGenerator)target;
            Level temp = generator.levels[index - 1];
            generator.levels[index - 1] = generator.levels[index];
            generator.levels[index] = temp;
            levelIndex--;
            EditorUtility.SetDirty(generator);
            Repaint();
        }

        void Duplicate(int index)
        {
            LevelGenerator generator = (LevelGenerator)target;
            Level[] newLevels = new Level[generator.levels.Length + 1];
            for (int i = 0; i < generator.levels.Length; i++)
            {
                if (i < index) newLevels[i] = generator.levels[i];
                else if (i == index)
                {
                    newLevels[i] = generator.levels[i];
                    newLevels[i + 1] = generator.levels[i].Duplicate();
                }
                else
                {
                    newLevels[i + 1] = generator.levels[i];
                }
            }
            generator.levels = newLevels;
            EditorUtility.SetDirty(generator);
            Repaint();
        }

        void Delete(int index)
        {
            LevelGenerator generator = (LevelGenerator)target;
            if (EditorUtility.DisplayDialog("Delete level", "Do you want to delete this level?", "Yes", "No"))
            {
                Level[] levels = new Level[generator.levels.Length - 1];
                for (int n = 0; n < levels.Length; n++)
                {
                    if (n < index) levels[n] = generator.levels[n];
                    else levels[n] = generator.levels[n + 1];
                }
                generator.levels = levels;
                levelIndex = -1;
                Repaint();
                EditorUtility.SetDirty(generator);
                Repaint();
            }
        }

        void ListLevels()
        {
            LevelGenerator generator = (LevelGenerator)target;
            for (int i = 0; i < generator.levels.Length; i++)
            {
                if (i == levelIndex)
                {
                    GUI.backgroundColor = ForeverPrefs.highlightColor * DreamteckEditorGUI.lightColor;
                    GUI.contentColor = ForeverPrefs.highlightContentColor;
                } else
                {
                    GUI.backgroundColor = DreamteckEditorGUI.lightColor;
                    GUI.contentColor = new Color(1f, 1f, 1f, 0.8f);
                }
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField(i + "  " + generator.levels[i].title);
                GUI.contentColor = Color.white;
                Rect rect = GUILayoutUtility.GetLastRect();
                if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 1)
                    {
                        int current = i;
                        GenericMenu menu = new GenericMenu();
                        if (i > 0) menu.AddItem(new GUIContent("Move Up"), false, delegate { MoveUp(current); });
                        else menu.AddDisabledItem(new GUIContent("Move Up"));
                        if (i < generator.levels.Length - 1) menu.AddItem(new GUIContent("Move Down"), false, delegate { MoveDown(current); });
                        else menu.AddDisabledItem(new GUIContent("Move Down"));
                        menu.AddItem(new GUIContent("Duplicate"), false, delegate { Duplicate(current); });
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Delete"), false, delegate { Delete(current); });
                        menu.ShowAsContext();
                    } else if(Event.current.button == 0)
                    {
                        levelWindow = EditorWindow.GetWindow<LevelWindow>(true);
                        levelWindow.Init(generator, i); 
                        levelIndex = i;
                        Repaint();
                    }
                }
                EditorGUILayout.EndVertical();
            }
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            if (GUILayout.Button("New Level"))
            {
                Level[] levels = new Level[generator.levels.Length + 1];
                generator.levels.CopyTo(levels, 0);
                levels[levels.Length - 1] = new Level();
                levels[levels.Length - 1].title = "Level " + levels.Length;
                generator.levels = levels;
                EditorUtility.SetDirty(generator);
                Repaint();
            }

        }
    }
}
