namespace Dreamteck.Forever.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Dreamteck.Splines;

    public class LevelSegmentCustomPathEditor
    {
        internal LevelSegment segment;
        internal LevelSegment.LevelSegmentPath path;
        internal LevelSegmentEditor editor;
        internal enum PointEditingSpace { Local, World, Spline }
        internal PointEditingSpace pointEditingSpace = PointEditingSpace.Local;
        internal enum PathTool { Move, Surface, Normal }
        internal PathTool tool = PathTool.Move;
        internal LayerMask surfaceLayermask = ~0;
        EditorGUIEvents input = new EditorGUIEvents();
        private bool isSurface = false;
        Matrix4x4 matrix = new Matrix4x4();


        int selectedPoint = -1;
        SplineSample evalResult = new SplineSample();

        ModifyWindow modifyWindow = null;

        public LevelSegmentCustomPathEditor(LevelSegmentEditor e, LevelSegment s, LevelSegment.LevelSegmentPath p)
        {
            editor = e;
            segment = s;
            path = p;
            tool = (PathTool)EditorPrefs.GetInt("Dreamteck.Forever.LevelSegmentCustompathEditor.tool", 0);
            surfaceLayermask = EditorPrefs.GetInt("Dreamteck.Forever.surfaceLayermask.tool", ~0);
        }

        public void Close()
        {
            if (modifyWindow != null) modifyWindow.Close();
            EditorPrefs.SetInt("Dreamteck.Forever.LevelSegmentCustompathEditor.tool", (int)tool);
            EditorPrefs.SetInt("Dreamteck.Forever.LevelSegmentCustompathEditor.surfaceLayermask", surfaceLayermask);
        }

        public void DrawInspector()
        {
            Spline spline = path.spline;
            if (spline == null) return;

            path.Transform();
            EditorGUILayout.BeginVertical();
            bool bezier = path.spline.type == Spline.Type.Bezier;
            bezier = EditorGUILayout.Toggle("Bezier", bezier);
            path.spline.type = bezier ? Spline.Type.Bezier : Spline.Type.Linear;
            path.seamlessEnds = EditorGUILayout.Toggle("Seamless Ends", path.seamlessEnds);
            path.spline.sampleRate = EditorGUILayout.IntField("Precision", path.spline.sampleRate);
            string[] options = new string[spline.points.Length + 1];
            for (int i = 0; i < options.Length - 1; i++) options[i + 1] = "Point " + (i + 1);
            options[0] = "- None -";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            selectedPoint = EditorGUILayout.Popup("Select Point", selectedPoint + 1, options) - 1;
            if (selectedPoint > 0 && spline.points.Length > 2 &&  modifyWindow == null)
            {
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    ArrayUtility.Remove(ref spline.points, spline.points[selectedPoint]);
                    selectedPoint--;
                }
            }
            EditorGUILayout.EndHorizontal();
            if (selectedPoint >= 0 && selectedPoint < spline.points.Length)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (tool == PathTool.Move) GUI.backgroundColor = ForeverPrefs.highlightColor;
                else GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Move", EditorStyles.miniButtonLeft)) tool = PathTool.Move;
                
                if (tool == PathTool.Surface) GUI.backgroundColor = ForeverPrefs.highlightColor;
                else GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Surface", EditorStyles.miniButtonMid)) tool = PathTool.Surface;
                
                if (tool == PathTool.Normal) GUI.backgroundColor = ForeverPrefs.highlightColor;
                else GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Normals", EditorStyles.miniButtonRight)) tool = PathTool.Normal;
                
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                if (tool == PathTool.Surface) surfaceLayermask = DreamteckEditorGUI.LayermaskField("Layer Mask", surfaceLayermask);
                EditorGUILayout.Space();

                SplinePoint avgPoint = spline.points[selectedPoint];

                avgPoint.SetPosition(EditorGUILayout.Vector3Field("Position", avgPoint.position));
                if (spline.type == Spline.Type.Bezier)
                {
                    EditorGUILayout.Space();
                    avgPoint.type = (SplinePoint.Type)EditorGUILayout.EnumPopup("Tangents Type", avgPoint.type);
                    avgPoint.SetTangent2Position(EditorGUILayout.Vector3Field("Front Tangent", avgPoint.tangent2));
                    avgPoint.SetTangentPosition(EditorGUILayout.Vector3Field("Back Tangent", avgPoint.tangent));
                    EditorGUILayout.Space();
                }
                else
                {
                    avgPoint.tangent = avgPoint.position;
                    avgPoint.tangent2 = avgPoint.position;
                }
                avgPoint.normal = EditorGUILayout.Vector3Field("Normal", avgPoint.normal);
                avgPoint.normal.Normalize();

                EditorGUIUtility.labelWidth = 0f;
                avgPoint.size = EditorGUILayout.FloatField("Size", avgPoint.size);
                avgPoint.color = EditorGUILayout.ColorField("Color", avgPoint.color);


                Undo.RecordObject(segment, "Edit Path " + path.name);
                spline.points[selectedPoint] = avgPoint;
                if (GUI.changed) path.InverseTransform();
            }
            
            if (modifyWindow == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Insert Point", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("At Start"))
                {
                    SplineSample result = spline.Evaluate(DMath.Lerp(0.0, 1.0 / (spline.points.Length - 1), 0.5));
                    float tangentLength = Mathf.Lerp(Vector3.Distance(spline.points[0].position, spline.points[0].tangent), Vector3.Distance(spline.points[1].position, spline.points[1].tangent), 0.5f);
                    ArrayUtility.Insert(ref spline.points, 1, new SplinePoint(result.position, result.position - result.forward * tangentLength, result.up, result.size, result.color));
                    path.InverseTransform();
                    selectedPoint = 1;
                }
                if (GUILayout.Button("At End"))
                {
                    SplineSample result = spline.Evaluate(DMath.Lerp((double)(spline.points.Length-2)/ (spline.points.Length - 1), 1.0, 0.5));
                    float tangentLength = Mathf.Lerp(Vector3.Distance(spline.points[spline.points.Length - 2].position, spline.points[spline.points.Length - 2].tangent), Vector3.Distance(spline.points[spline.points.Length - 1].position, spline.points[spline.points.Length - 1].tangent), 0.5f);
                    ArrayUtility.Insert(ref spline.points, spline.points.Length - 2, new SplinePoint(result.position, result.position - result.forward * tangentLength, result.up, result.size, result.color));
                    path.InverseTransform();
                    selectedPoint = spline.points.Length - 2;
                }

                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Modify Path"))
                {
                    modifyWindow = EditorWindow.GetWindow<ModifyWindow>(true);
                    modifyWindow.Init(this);
                }
            }
            EditorGUILayout.EndVertical();
        }

        public void DrawScene()
        {
            if (path.spline == null) return;
            path.Transform();
            ForeverSplineDrawer.DrawSpline(path.spline, path.color);
            Vector3 cameraPos = SceneView.currentDrawingSceneView.camera.transform.position;
            Quaternion handleRotation = Quaternion.identity;
            if (pointEditingSpace == PointEditingSpace.Local) handleRotation = segment.transform.rotation;
            for (int i = 0; i < path.spline.points.Length; i++)
            {
                if(pointEditingSpace == PointEditingSpace.Spline)
                {
                    path.spline.Evaluate(evalResult, (double)i / (path.spline.points.Length - 1));
                    handleRotation = evalResult.rotation;
                }
                if(path.spline.type == Spline.Type.Bezier)
                {

                }
                if (i == selectedPoint)
                {
                    input.Update();
                    if(tool == PathTool.Move) path.spline.points[i].SetPosition(Handles.PositionHandle(path.spline.points[i].position, handleRotation));
                    else if(tool == PathTool.Surface)
                    {
                        Handles.color = Color.white;
                        Handles.RectangleHandleCap(0, path.spline.points[i].position, Quaternion.LookRotation(-SceneView.currentDrawingSceneView.camera.transform.forward), HandleUtility.GetHandleSize(path.spline.points[i].position) * 0.1f, EventType.Repaint);
                        Handles.color = Color.black;
                        Handles.RectangleHandleCap(0, path.spline.points[i].position, Quaternion.LookRotation(-SceneView.currentDrawingSceneView.camera.transform.forward), HandleUtility.GetHandleSize(path.spline.points[i].position) * 0.12f, EventType.Repaint);
                        if (!input.mouseLeft) isSurface = false;
                        Vector3 guiPoint = HandleUtility.WorldToGUIPoint(path.spline.points[i].position);
                        Rect containerRect = new Rect(guiPoint.x - 15, guiPoint.y - 15, 30, 30);
                        if(guiPoint.z >= 0f && containerRect.Contains(Event.current.mousePosition) && input.mouseLeftDown)
                        {
                            isSurface = true;
                            Event.current.Use();
                        }

                        if (isSurface)
                        {
                            RaycastHit hit;
                            if(Physics.Raycast(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition), out hit, Mathf.Infinity, surfaceLayermask))
                            {
                                path.spline.points[i].SetPosition(hit.point);
                            }
                        }
                    } else if(tool == PathTool.Normal)
                    {
                        path.spline.Evaluate(evalResult, (double)i / (path.spline.points.Length - 1));
                        Handles.color = evalResult.color;
                        Handles.DrawWireDisc(evalResult.position, evalResult.forward, evalResult.up.magnitude * evalResult.size);
                        matrix.SetTRS(evalResult.position, evalResult.rotation, Vector3.one * evalResult.size);
                        Vector3 pos = path.spline.points[i].position + path.spline.points[i].normal;
                        Handles.DrawLine(evalResult.position, pos);
                        Vector3 lastPos = pos;
                        pos =  Handles.FreeMoveHandle(pos, Quaternion.identity, HandleUtility.GetHandleSize(pos) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);
                        if (pos != lastPos)
                        {
                            pos = matrix.inverse.MultiplyPoint(pos);
                            pos.z = 0f;
                            pos = matrix.MultiplyPoint(pos);
                            path.spline.points[i].normal = pos - path.spline.points[i].position;
                        }
                    }

                    Color col = path.spline.points[i].color;
                    col.a = 0.7f;
                    Handles.color = col;
                    if (path.spline.type == Spline.Type.Bezier)
                    {
                        path.spline.points[i].SetTangent2Position(Handles.PositionHandle(path.spline.points[i].tangent2, handleRotation));
                        path.spline.points[i].SetTangentPosition(Handles.PositionHandle(path.spline.points[i].tangent, handleRotation));
                        Handles.DrawDottedLine(path.spline.points[i].position, path.spline.points[i].tangent, 10f);
                        Handles.DrawDottedLine(path.spline.points[i].position, path.spline.points[i].tangent2, 10f);
                        Handles.DrawWireDisc(path.spline.points[i].tangent, cameraPos - path.spline.points[i].tangent, HandleUtility.GetHandleSize(path.spline.points[i].tangent) * 0.075f);
                        Handles.DrawWireDisc(path.spline.points[i].tangent2, cameraPos - path.spline.points[i].tangent2, HandleUtility.GetHandleSize(path.spline.points[i].tangent2) * 0.075f);
                    }
                }
                else
                {
                    Vector3 pos = path.spline.points[i].position;
                    float handleSize = HandleUtility.GetHandleSize(path.spline.points[i].position);
                    float pointSize = handleSize * 0.1f * Mathf.Max(path.spline.points[i].size, 0.2f);
                    Handles.DrawSolidDisc(pos, cameraPos - pos, pointSize);
                    Handles.DrawLine(pos, pos + path.spline.points[i].normal * 5f * handleSize * 0.2f);
                    Color inv = Color.white - Handles.color;
                    inv.a = Handles.color.a;
                    Handles.color = inv;
                    Handles.DrawWireDisc(pos, cameraPos - pos, pointSize);
                    if (path.spline.type == Spline.Type.Bezier)
                    {
                        Handles.color = Color.clear;
                        if (Handles.Button(pos, Quaternion.LookRotation(cameraPos - pos), pointSize, pointSize, Handles.CircleHandleCap))
                        {
                            selectedPoint = i;
                            editor.Repaint();
                        }
                    }
                }
                Handles.color = Color.white;
            }

            for (int i = 0; i < path.spline.points.Length; i++)
            {
                double percent = (double)i / (path.spline.points.Length - 1);
                SplineSample result = path.spline.Evaluate(percent);
                Vector3 normal = Vector3.Cross(result.forward, result.right).normalized;
                if (normal != Vector3.zero) path.spline.points[i].normal = normal;
            }
            path.InverseTransform();
        }

        internal class ModifyWindow : EditorWindow
        {
            LevelSegmentCustomPathEditor editor = null;
            internal enum OffsetSpace { World, Segment, Spline };
            internal OffsetSpace space = OffsetSpace.Segment;
            internal Vector3 offset = Vector3.zero;
            SplinePoint[] originalPoints = new SplinePoint[0];
            SplineSample result = new SplineSample();
            bool saved = true;

            internal void Init(LevelSegmentCustomPathEditor e)
            {
                editor = e;
                originalPoints = new SplinePoint[editor.path.localPoints.Length];
                editor.path.localPoints.CopyTo(originalPoints, 0);
                titleContent = new GUIContent("Modify " + editor.path.name);
            }

            private void OnGUI()
            {
                if(editor == null)
                {
                    Close();
                    return;
                }

                OffsetGUI();
                EditorGUILayout.Space();
                if(GUILayout.Button("Auto Tangents")) AutoTangents(ref editor.path.localPoints);
                
                if (GUI.changed)
                {
                    saved = false;
                    OffsetLogic();
                    editor.path.Transform();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("Save"))
                {
                    saved = true;
                    Close();
                }
            }

            void OffsetGUI()
            {
                offset = EditorGUILayout.Vector3Field("Offset", offset);
                space = (OffsetSpace)EditorGUILayout.EnumPopup("Space", space);
            }

            void OffsetLogic()
            {
                for (int i = 0; i < editor.path.localPoints.Length; i++)
                {
                    switch (space)
                    {
                        case OffsetSpace.Segment: editor.path.localPoints[i].SetPosition(originalPoints[i].position + offset); break;
                        case OffsetSpace.World: editor.path.localPoints[i].SetPosition(originalPoints[i].position + editor.segment.transform.InverseTransformDirection(offset)); break;
                        case OffsetSpace.Spline:
                            editor.path.spline.Evaluate(result, (double)i / (editor.path.spline.points.Length - 1));
                            Matrix4x4 matrix = Matrix4x4.TRS(result.position, result.rotation, Vector3.one);
                            editor.path.localPoints[i].SetPosition(originalPoints[i].position + editor.segment.transform.InverseTransformDirection(matrix.MultiplyVector(offset))); break;
                    }
                }
            }

            private void OnDestroy()
            {
                if (editor == null) return;
                if (!saved)
                {
                    if (EditorUtility.DisplayDialog("Unsaved Changes", "You haven't saved the changes made. Would you like to save them?", "Yes", "No")) saved = true;
                }
                if(!saved) editor.path.localPoints = originalPoints;
            }

            protected void AutoTangents(ref SplinePoint[] points)
            {
                for (int i = 1; i < points.Length-1; i++)
                {
                    Vector3 prevPos = points[i - 1].position, forwardPos = points[i + 1].position;
                    Vector3 delta = (forwardPos - prevPos) / 2f;
                    points[i].tangent = points[i].position - delta / 3f;
                    points[i].tangent2 = points[i].position + delta / 3f;
                }
                float firstPointDistance = Vector3.Distance(points[0].position, points[1].position);
                float lastPointDistance = Vector3.Distance(points[points.Length-1].position, points[points.Length - 2].position);
                if(editor.segment.type == LevelSegment.Type.Extruded)
                {
                    switch (editor.segment.axis)
                    {
                        case LevelSegment.Axis.X:
                            points[0].tangent = points[0].position + Vector3.left * firstPointDistance / 3f;
                            points[0].tangent2 = points[0].position + Vector3.right * firstPointDistance / 3f;
                            points[points.Length-1].tangent = points[points.Length - 1].position + Vector3.left * lastPointDistance / 3f;
                            points[points.Length-1].tangent2 = points[points.Length - 1].position + Vector3.right * lastPointDistance / 3f;
                            break;
                        case LevelSegment.Axis.Y:
                            points[0].tangent = points[0].position + Vector3.down * firstPointDistance;
                            points[0].tangent2 = points[0].position + Vector3.up * firstPointDistance;
                            points[points.Length - 1].tangent = points[points.Length - 1].position + Vector3.down * lastPointDistance / 3f;
                            points[points.Length - 1].tangent2 = points[points.Length - 1].position + Vector3.up * lastPointDistance / 3f;
                            break;
                        case LevelSegment.Axis.Z:
                            points[0].tangent = points[0].position + Vector3.back * firstPointDistance / 3f;
                            points[0].tangent2 = points[0].position + Vector3.forward * firstPointDistance / 3f;
                            points[points.Length - 1].tangent = points[points.Length - 1].position + Vector3.back * lastPointDistance / 3f;
                            points[points.Length - 1].tangent2 = points[points.Length - 1].position + Vector3.forward * lastPointDistance / 3f;
                            break;
                    }
                } else
                {
                    if(editor.segment.customEntrance != null)
                    {
                        Vector3 entranceDir = editor.segment.transform.InverseTransformDirection(editor.segment.customEntrance.forward);
                        points[0].tangent = points[0].position - entranceDir * firstPointDistance / 3f;
                        points[0].tangent2 = points[0].position + entranceDir * firstPointDistance / 3f;
                    }
                    if(editor.segment.customExit != null)
                    {
                        Vector3 exitDir = editor.segment.transform.InverseTransformDirection(editor.segment.customExit.forward);
                        points[points.Length - 1].tangent = points[points.Length - 1].position - exitDir * firstPointDistance / 3f;
                        points[points.Length - 1].tangent2 = points[points.Length - 1].position + exitDir * firstPointDistance / 3f ;
                    }
                }
            }
        }
    }
}
