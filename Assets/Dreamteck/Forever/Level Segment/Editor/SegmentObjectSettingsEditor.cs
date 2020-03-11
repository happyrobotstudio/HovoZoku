namespace Dreamteck.Forever.Editor
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(SegmentObjectSettings))]
    public class SegmentObjectSettingsEditor : Editor
    {
        private void OnEnable()
        {
            ExtrusionSettingsEditor.onWillChange += RecordUndo;
        }

        private void OnDisable()
        {
            ExtrusionSettingsEditor.onWillChange -= RecordUndo;
        }

        void RecordUndo()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                Undo.RecordObject(((SegmentObjectSettings)targets[i]), "Edit Settings");
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Extrusion Settings", EditorStyles.boldLabel);
            ExtrusionSettings[] settings = new ExtrusionSettings[targets.Length];
            for (int i = 0; i < settings.Length; i++) settings[i] = ((SegmentObjectSettings)targets[i]).settings;
            ExtrusionSettingsEditor.Draw(settings);
        }
    }
}
