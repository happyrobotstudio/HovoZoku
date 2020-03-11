namespace Dreamteck.Forever.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Splines;
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CustomPathGenerator))]
    public class CustomPathGeneratorEditor : PathGeneratorEditor
    {
        bool pointsPanel = false;
        int selectedPoint = -1;
        Spline visualization;
        SplineSample drawResult = new SplineSample();

        protected override void OnInspector()
        {
            base.OnInspector();
            CustomPathGenerator gen = (CustomPathGenerator)target;
            Undo.RecordObject(gen, gen.name + " - Edit Properties");
            Panel("Custom Path", ref pointsPanel, PointsGUI);
            if (GUI.changed) EditorUtility.SetDirty(gen);
        }

        void PointsGUI()
        {
            CustomPathGenerator generator = (CustomPathGenerator)target;
            generator.customPathType = (Spline.Type)EditorGUILayout.EnumPopup("Custom Path Type", generator.customPathType);
            if (generator.points.Length >= 4) generator.loop = EditorGUILayout.Toggle("Loop", generator.loop);
            else generator.loop = false;

            string[] options = new string[(generator.loop ? generator.points.Length - 1 : generator.points.Length) + 1];
            for (int i = 0; i < options.Length - 1; i++) options[i + 1] = "Point " + (i + 1);
            options[0] = "- None -";
            EditorGUILayout.BeginHorizontal();
            selectedPoint = EditorGUILayout.Popup("Select Point", selectedPoint + 1, options) - 1;
            if((!generator.loop && generator.points.Length >= 1) || (generator.loop && generator.points.Length > 4))
            {
                if(GUILayout.Button("X", GUILayout.Width(20)))
                {
                    ArrayUtility.RemoveAt(ref generator.points, selectedPoint);
                    selectedPoint--;
                }
            }
            EditorGUILayout.EndHorizontal();
            if (selectedPoint >= 0 && selectedPoint < generator.points.Length)
            {
                SplinePoint avgPoint = generator.points[selectedPoint];

                avgPoint.SetPosition(EditorGUILayout.Vector3Field("Position", avgPoint.position));
                if (generator.customPathType == Splines.Spline.Type.Bezier)
                {
                    EditorGUILayout.Space();
                    avgPoint.type = (SplinePoint.Type)EditorGUILayout.EnumPopup("Tangents Type", avgPoint.type);
                    avgPoint.SetTangent2Position(EditorGUILayout.Vector3Field("Front Tangent", avgPoint.tangent2));
                    avgPoint.SetTangentPosition(EditorGUILayout.Vector3Field("Back Tangent", avgPoint.tangent));
                    EditorGUILayout.Space();
                } else
                {
                    avgPoint.tangent = avgPoint.position;
                    avgPoint.tangent2 = avgPoint.position;
                }
                avgPoint.normal = EditorGUILayout.Vector3Field("Normal", avgPoint.normal);
                avgPoint.normal.Normalize();

                EditorGUIUtility.labelWidth = 0f;
                avgPoint.size = EditorGUILayout.FloatField("Size", avgPoint.size);
                avgPoint.color = EditorGUILayout.ColorField("Color", avgPoint.color);

                if (generator.loop) generator.points[generator.points.Length - 1] = generator.points[0];
                generator.points[selectedPoint] = avgPoint;
            }

            if(GUILayout.Button("Create Point"))
            {
                SplinePoint newPoint = new SplinePoint();
                newPoint.color = Color.white;
                newPoint.size = 1f;
                newPoint.normal = Vector3.up;
                ArrayUtility.Add(ref generator.points, newPoint);
                selectedPoint = generator.points.Length - 1;
            }
        }

        public override void DrawScene(SceneView current)
        {
            CustomPathGenerator generator = (CustomPathGenerator)target;
            if (visualization == null) visualization = new Spline(generator.customPathType, generator.customPathSampleRate);
            visualization.sampleRate = generator.customPathSampleRate;
            visualization.type = generator.customPathType;
            visualization.points = generator.points;
            if (generator.loop && generator.points.Length > 3) visualization.Close();
            else if(visualization.isClosed) visualization.Break();
            DrawSpline(visualization);
            Vector3 cameraPos = SceneView.currentDrawingSceneView.camera.transform.position;
            for (int i = 0; i < generator.points.Length; i++)
            {
                if (visualization.isClosed && i == generator.points.Length - 1) continue;
                Color col = generator.points[i].color;
                col.a = 0.7f;
                Handles.color = col;
                if (i == selectedPoint)
                {
                    generator.points[i].SetPosition(Handles.PositionHandle(generator.points[i].position, Quaternion.identity));
                    if (visualization.type == Spline.Type.Bezier)
                    {
                        generator.points[i].SetTangent2Position(Handles.PositionHandle(generator.points[i].tangent2, Quaternion.identity));
                        generator.points[i].SetTangentPosition(Handles.PositionHandle(generator.points[i].tangent, Quaternion.identity));
                        Handles.DrawLine(generator.points[i].position, generator.points[i].tangent);
                        Handles.DrawLine(generator.points[i].position, generator.points[i].tangent2);
                    }
                }
                else
                {
                    Vector3 pos = generator.points[i].position;
                    float handleSize = HandleUtility.GetHandleSize(generator.points[i].position);
                    float pointSize = handleSize * 0.1f * Mathf.Max(generator.points[i].size, 0.2f);
                    Handles.DrawSolidDisc(pos, cameraPos - pos, pointSize);
                    Handles.DrawLine(pos, pos + generator.points[i].normal * 5f * handleSize * 0.2f);
                    if (generator.customPathType == Spline.Type.Bezier)
                    {
                        Handles.DrawDottedLine(generator.points[i].position, generator.points[i].tangent, 10f);
                        Handles.DrawDottedLine(generator.points[i].position, generator.points[i].tangent2, 10f);
                        Handles.DrawWireDisc(generator.points[i].tangent, cameraPos - generator.points[i].tangent, handleSize * 0.075f);
                        Handles.DrawWireDisc(generator.points[i].tangent2, cameraPos - generator.points[i].tangent2, handleSize * 0.075f);
                    }
                    Handles.color = Color.clear;
                    if (Handles.Button(pos, Quaternion.LookRotation(cameraPos - pos), pointSize, pointSize, Handles.CircleHandleCap))
                    {
                        selectedPoint = i;
                        pointsPanel = true;
                        Repaint();
                    }
                }
            }
            if (generator.loop) generator.points[generator.points.Length - 1] = generator.points[0];
            Handles.color = Color.white;
        }

        public void DrawSpline(Spline spline)
        {
            double add = spline.moveStep;
            if (add < 0.0025) add = 0.0025;
            Color prevColor = Handles.color;
            int iterations = spline.iterations;

            Vector3 prevPos = spline.EvaluatePosition(0.0);
            for (int i = 1; i < iterations; i++)
            {
                double p = (double)i / (iterations - 1);
                spline.Evaluate(drawResult, p);
                Vector3 newPos = drawResult.position;
                Color col = drawResult.color;
                col.a = 1f;
                Handles.color = col;
                Handles.DrawLine(newPos, prevPos);
                prevPos = newPos;
            }
            Handles.color = prevColor;
        }
    }
}
