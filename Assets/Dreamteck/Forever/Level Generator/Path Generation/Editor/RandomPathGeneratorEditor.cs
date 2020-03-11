namespace Dreamteck.Forever.Editor
{
    using UnityEditor;
    using UnityEngine;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(RandomPathGenerator))]
    public class RandomPathGeneratorEditor : HighLevelPathGeneratorEditor
    {
        bool orientation = false;
        bool offset = false;
        bool colors = false;
        bool sizes = false;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            RandomPathGenerator gen = (RandomPathGenerator)target;
            Panel("Orientation", ref orientation, OrientationGUI);
            Panel("Colors", ref colors, ColorGUI);
            Panel("Sizes", ref sizes, SizeGUI);
            Panel("Offset", ref offset, OffsetGUI);
            if (GUI.changed) EditorUtility.SetDirty(gen);
        }

        protected virtual void OrientationGUI()
        {
            RandomPathGenerator generator = (RandomPathGenerator)target;
            EditorGUILayout.BeginHorizontal();
            generator.usePitch = EditorGUILayout.Toggle(generator.usePitch, GUILayout.Width(20));
            GUILayout.Label("Pitch", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            if (generator.usePitch)
            {
                generator.restrictPitch = EditorGUILayout.Toggle("Restrict", generator.restrictPitch);
                if (generator.restrictPitch)
                {
                    generator.minOrientation.x = EditorGUILayout.FloatField("Restrict Min.", generator.minOrientation.x);
                    generator.maxOrientation.x = EditorGUILayout.FloatField("Restrict Max.", generator.maxOrientation.x);
                }
                generator.minRandomStep.x = EditorGUILayout.FloatField("Min. Target Step", generator.minRandomStep.x);
                if (generator.minRandomStep.x < 0f) generator.minRandomStep.x = 0f;
                generator.maxRandomStep.x = EditorGUILayout.FloatField("Max. Target Step", generator.maxRandomStep.x);
                if (generator.maxRandomStep.x < 0f) generator.maxRandomStep.x = 0f;
                generator.minTurnRate.x = EditorGUILayout.FloatField("Min. Turn Rate", generator.minTurnRate.x);
                generator.maxTurnRate.x = EditorGUILayout.FloatField("Max. Turn Rate", generator.maxTurnRate.x);
                EditorGUILayout.BeginHorizontal();
                generator.useStartPitchTarget = EditorGUILayout.Toggle("Level Start Target", generator.useStartPitchTarget);
                if (generator.useStartPitchTarget) generator.startTargetOrientation.x = EditorGUILayout.FloatField("", generator.startTargetOrientation.x);
                EditorGUILayout.EndHorizontal();
            }
            

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            generator.useYaw = EditorGUILayout.Toggle(generator.useYaw, GUILayout.Width(20));
            GUILayout.Label("Yaw", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            if (generator.useYaw)
            {
                generator.restrictYaw = EditorGUILayout.Toggle("Restrict", generator.restrictYaw);
                if (generator.restrictYaw)
                {
                    generator.minOrientation.y = EditorGUILayout.FloatField("Restrict Min.", generator.minOrientation.y);
                    generator.maxOrientation.y = EditorGUILayout.FloatField("Restrict Max.", generator.maxOrientation.y);
                }
                generator.minRandomStep.y = EditorGUILayout.FloatField("Min. Target Step", generator.minRandomStep.y);
                if (generator.minRandomStep.y < 0f) generator.minRandomStep.y = 0f;
                generator.maxRandomStep.y = EditorGUILayout.FloatField("Max. Target Step", generator.maxRandomStep.y);
                if (generator.maxRandomStep.y < 0f) generator.maxRandomStep.y = 0f;
                generator.minTurnRate.y = EditorGUILayout.FloatField("Min. Turn Rate", generator.minTurnRate.y);
                generator.maxTurnRate.y = EditorGUILayout.FloatField("Max. Turn Rate", generator.maxTurnRate.y);
                EditorGUILayout.BeginHorizontal();
                generator.useStartYawTarget = EditorGUILayout.Toggle("Level Start Target", generator.useStartYawTarget);
                if (generator.useStartYawTarget) generator.startTargetOrientation.y = EditorGUILayout.FloatField("", generator.startTargetOrientation.y);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            generator.useRoll = EditorGUILayout.Toggle(generator.useRoll, GUILayout.Width(20));
            GUILayout.Label("Roll", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            if (generator.useRoll)
            {
                generator.restrictRoll = EditorGUILayout.Toggle("Restrict", generator.restrictRoll);
                if (generator.restrictRoll)
                {
                    generator.minOrientation.z = EditorGUILayout.FloatField("Restrict Min.", generator.minOrientation.z);
                    generator.maxOrientation.z = EditorGUILayout.FloatField("Restrict Max.", generator.maxOrientation.z);
                }
                generator.minRandomStep.z = EditorGUILayout.FloatField("Min. Target Step", generator.minRandomStep.z);
                if (generator.minRandomStep.z < 0f) generator.minRandomStep.z = 0f;
                generator.maxRandomStep.z = EditorGUILayout.FloatField("Max. Target Step", generator.maxRandomStep.z);
                if (generator.maxRandomStep.z < 0f) generator.maxRandomStep.z = 0f;
                generator.minTurnRate.z = EditorGUILayout.FloatField("Min. Turn Rate", generator.minTurnRate.z);
                generator.maxTurnRate.z = EditorGUILayout.FloatField("Max. Turn Rate", generator.maxTurnRate.z);
                EditorGUILayout.BeginHorizontal();
                generator.useStartRollTarget = EditorGUILayout.Toggle("Level Start Target", generator.useStartRollTarget);
                if (generator.useStartRollTarget) generator.startTargetOrientation.z = EditorGUILayout.FloatField("", generator.startTargetOrientation.z);
                EditorGUILayout.EndHorizontal();
            }
        }

        protected virtual void ColorGUI()
        {
            RandomPathGenerator generator = (RandomPathGenerator)target;
            EditorGUILayout.BeginHorizontal();
            generator.useColors = EditorGUILayout.Toggle(generator.useColors, GUILayout.Width(20));
            GUILayout.Label("Use Colors", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            if (generator.useColors)
            {
                generator.minColor = DreamteckEditorGUI.GradientField("Min.", generator.minColor);
                generator.maxColor = DreamteckEditorGUI.GradientField("Max.", generator.maxColor);
            }
        }

        protected virtual void SizeGUI()
        {
            RandomPathGenerator generator = (RandomPathGenerator)target;
            EditorGUILayout.BeginHorizontal();
            generator.useSizes = EditorGUILayout.Toggle(generator.useSizes, GUILayout.Width(20));
            GUILayout.Label("Use Sizes", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            if (generator.useSizes)
            {
                generator.minSize = EditorGUILayout.CurveField("Min.", generator.minSize);
                generator.maxSize = EditorGUILayout.CurveField("Max.", generator.maxSize);
            }
        }

        protected virtual void OffsetGUI()
        {
            RandomPathGenerator generator = (RandomPathGenerator)target;
            generator.minSegmentOffset = EditorGUILayout.Vector3Field("Min. Segment Offset", generator.minSegmentOffset);
            generator.maxSegmentOffset = EditorGUILayout.Vector3Field("Max. Segment Offset", generator.maxSegmentOffset);
            generator.segmentOffsetSpace = (Space)EditorGUILayout.EnumPopup("Segment Offset Space", generator.segmentOffsetSpace);
            EditorGUILayout.Space();
            generator.newLevelMinOffset = EditorGUILayout.Vector3Field("Min. New Level Offset", generator.newLevelMinOffset);
            generator.newLevelMaxOffset = EditorGUILayout.Vector3Field("Max. New Level Offset", generator.newLevelMaxOffset);
            generator.levelOffsetSpace = (Space)EditorGUILayout.EnumPopup("Level Offset Space", generator.levelOffsetSpace);
        }
    }
}
