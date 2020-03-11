namespace Dreamteck.Forever.Editor
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(LevelPathGenerator), true)]
    public class PathGeneratorEditor : Editor
    {
        bool path = false;
        protected delegate void UIHandler();

        public override void OnInspectorGUI()
        {
            LevelPathGenerator generator = (LevelPathGenerator)target;
            Undo.RecordObject(generator, generator.name + " - Edit Properties");
            base.OnInspectorGUI();
            OnInspector();
        }

        protected virtual void OnInspector()
        {
            LevelPathGenerator generator = (LevelPathGenerator)target;
            Panel("Path", ref path, PathGUI);
            if (GUI.changed) EditorUtility.SetDirty(generator);
        }

        protected virtual void PathGUI()
        {
            LevelPathGenerator generator = (LevelPathGenerator)target;
            generator.pathType = (LevelPathGenerator.PathType)EditorGUILayout.EnumPopup("Type", generator.pathType);
            generator.controlPointsPerSegment = EditorGUILayout.IntField("Points Per Segment", generator.controlPointsPerSegment);
            if (generator.controlPointsPerSegment < 2) generator.controlPointsPerSegment = 2;
            generator.sampleRate = EditorGUILayout.IntField("Sample Rate", generator.sampleRate);
            if (generator.sampleRate < 1) generator.sampleRate = 1;
            EditorGUILayout.BeginHorizontal();
            generator.customNormalInterpolation = EditorGUILayout.Toggle("Normal Interpolation", generator.customNormalInterpolation);
            if (generator.customNormalInterpolation) generator.normalInterpolation = EditorGUILayout.CurveField(generator.normalInterpolation);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            generator.customValueInterpolation = EditorGUILayout.Toggle("Value Interpolation", generator.customValueInterpolation);
            if (generator.customValueInterpolation) generator.valueInterpolation = EditorGUILayout.CurveField(generator.valueInterpolation);
            EditorGUILayout.EndHorizontal();
        }

        protected void Panel(string name, ref bool toggle, UIHandler handler)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;
            toggle = EditorGUILayout.Foldout(toggle, name);
            EditorGUI.indentLevel--;
            if (toggle)
            {
                EditorGUILayout.Space();
                handler();
            }
            EditorGUILayout.EndVertical();
        }

        protected void OnEnable()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += DrawScene;
#else
            SceneView.onSceneGUIDelegate += DrawScene;
#endif
        }

        protected void OnDisable()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= DrawScene;
#else
            SceneView.onSceneGUIDelegate -= DrawScene;
#endif
            AssetDatabase.SaveAssets();
        }

        public virtual void DrawScene(SceneView current)
        {

        }
    }
}
