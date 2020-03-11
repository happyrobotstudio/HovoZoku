using UnityEngine;
using UnityEditor;

namespace Dreamteck.Forever
{
    public static class ExtrusionSettingsEditor
    {
        public delegate void EmptyHandler();
        public static event EmptyHandler onWillChange;

        public static bool Draw(ExtrusionSettings settings)
        {
            EditorGUIUtility.labelWidth = 105;
            ExtrusionSettings.Indexing indexing = (ExtrusionSettings.Indexing)EditorGUILayout.EnumPopup("Indexing", settings.indexing);
            EditorGUILayout.Space();
#if UNITY_2017_4_OR_NEWER
            ExtrusionSettings.BoundsInclusion boundsInclusion = (ExtrusionSettings.BoundsInclusion)EditorGUILayout.EnumFlagsField("Include in bounds", settings.boundsInclusion);
#else
            ExtrusionSettings.BoundsInclusion boundsInclusion = (ExtrusionSettings.BoundsInclusion)EditorGUILayout.EnumMaskField("Include in bounds", settings.boundsInclusion);
#endif 


            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            bool applyRotation = EditorGUILayout.Toggle("Rotation", settings.applyRotation);
            Vector3 upVector = Vector3.up;
            bool keepUpright = false;
            if (applyRotation)
            {
                EditorGUI.indentLevel++;
                keepUpright = EditorGUILayout.Toggle("Keep Upright", settings.keepUpright);
                if (keepUpright) upVector = EditorGUILayout.Vector3Field("Up Vector", settings.upVector);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            bool applyScale = EditorGUILayout.Toggle("Scale", settings.applyScale);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Extrusion", EditorStyles.boldLabel);

            bool bendMesh = false;
            bool applyMeshColors = false;
            bool bendSprite = false;
            if (!settings.bendSprite) bendMesh = EditorGUILayout.Toggle("Bend Mesh", settings.bendMesh);
            if (bendMesh) applyMeshColors = EditorGUILayout.Toggle("Apply Vertex Colors", settings.applyMeshColors);
            if (!bendMesh) bendSprite = EditorGUILayout.Toggle("Bend Sprite", settings.bendSprite);

            ExtrusionSettings.MeshColliderHandling meshColliderHandling = (ExtrusionSettings.MeshColliderHandling)EditorGUILayout.EnumPopup("Mesh Collider", settings.meshColliderHandling);

#if DREAMTECK_SPLINES
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Splines", EditorStyles.boldLabel);
            bool bendSpline = EditorGUILayout.Toggle("Bend Spline", settings.bendSpline);
#endif
            bool change = false;
            if (indexing != settings.indexing) change = true;
            else if (boundsInclusion != settings.boundsInclusion) change = true;
            else if (applyRotation != settings.applyRotation) change = true;
            else if (applyScale != settings.applyScale) change = true;
            else if (bendMesh != settings.bendMesh) change = true;
            else if (bendSprite != settings.bendSprite) change = true;
            else if (applyMeshColors != settings.applyMeshColors) change = true;
            else if (meshColliderHandling != settings.meshColliderHandling) change = true;
            else if (keepUpright != settings.keepUpright) change = true;
            else if (upVector != settings.upVector) change = true;
#if DREAMTECK_SPLINES
            else if (bendSpline != settings.bendSpline) change = true;
#endif
            if (change) {
                if (onWillChange != null) onWillChange();
                settings.indexing = indexing;
                settings.boundsInclusion = boundsInclusion;
                settings.applyRotation = applyRotation;
                settings.applyScale = applyScale;
                settings.bendMesh = bendMesh;
                settings.bendSprite = bendSprite;
                settings.applyMeshColors = applyMeshColors;
                settings.meshColliderHandling = meshColliderHandling;
                settings.keepUpright = keepUpright;
                settings.upVector = upVector;
#if DREAMTECK_SPLINES
                settings.bendSpline = bendSpline;
#endif
            }
            GUI.color = Color.white;
            return change;
        }

        public static bool Draw(ExtrusionSettings[] settings)
        {
            ExtrusionSettings average = new ExtrusionSettings();
            int applyRotationCount = 0;
            int applyScaleCount = 0;
            int bendMeshCount = 0;
            int bendSpriteCount = 0;
            int applyColorCount = 0;
#if DREAMTECK_SPLINES
            int bendSplineCount = 0;
#endif
            int uprightCount = 0;
            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i].applyRotation) applyRotationCount++;
                if (settings[i].applyScale) applyScaleCount++;
                if (settings[i].bendMesh) bendMeshCount++;
                if (settings[i].bendSprite) bendSpriteCount++;
                if (settings[i].applyMeshColors) applyColorCount++;
#if DREAMTECK_SPLINES
                if (settings[i].bendSpline) bendSplineCount++;
#endif
                if (settings[i].keepUpright) uprightCount++;
                average.upVector += settings[i].upVector;
                average.meshColliderHandling = settings[i].meshColliderHandling;
                average.boundsInclusion = settings[i].boundsInclusion;
                average.indexing = settings[i].indexing;
            }
            average.upVector.Normalize();
            average.applyRotation = applyRotationCount == settings.Length;
            average.applyScale = applyScaleCount == settings.Length;
            average.bendMesh = bendMeshCount == settings.Length;
            average.bendSprite = bendSpriteCount == settings.Length;
#if DREAMTECK_SPLINES
            average.bendSpline = bendSplineCount == settings.Length;
#endif
            average.keepUpright = uprightCount == settings.Length;
            average.applyMeshColors = applyColorCount == settings.Length;
            bool lastAR = average.applyRotation, lastAS = average.applyScale, lastBM = average.bendMesh, lastBSpr = average.bendSprite, lastAC = average.applyMeshColors, lastUpright = average.keepUpright;
#if DREAMTECK_SPLINES
            bool lastBS = average.bendSpline;
#endif
            ExtrusionSettings.Indexing lastIndexing = average.indexing;
            ExtrusionSettings.BoundsInclusion lastBoundsInclusion = average.boundsInclusion;
            ExtrusionSettings.MeshColliderHandling lastColliderHandling = average.meshColliderHandling;

            Vector3 lastUpVector = average.upVector;
            if (Draw(average))
            {
                for (int i = 0; i < settings.Length; i++)
                {
                    if (lastAR != average.applyRotation) settings[i].applyRotation = average.applyRotation;
                    if (lastAS != average.applyScale) settings[i].applyScale = average.applyScale;
                    if (lastBM != average.bendMesh) settings[i].bendMesh = average.bendMesh;
                    if (lastBSpr != average.bendSprite) settings[i].bendSprite = average.bendSprite;
                    if (lastAC != average.applyMeshColors) settings[i].applyMeshColors = average.applyMeshColors;
#if DREAMTECK_SPLINES
                    if (lastBS != average.bendSpline) settings[i].bendSpline = average.bendSpline;
#endif
                    if (lastUpright != average.keepUpright) settings[i].keepUpright = average.keepUpright;
                    if (lastUpVector != average.upVector) settings[i].upVector = average.upVector;
                    if (lastIndexing != average.indexing) settings[i].indexing = average.indexing;
                    if (lastBoundsInclusion != average.boundsInclusion) settings[i].boundsInclusion = average.boundsInclusion;
                    if (lastColliderHandling != average.meshColliderHandling) settings[i].meshColliderHandling = average.meshColliderHandling;
                }
                SceneView.RepaintAll();
                return true;
            }
            return false;
        }
    }
}
