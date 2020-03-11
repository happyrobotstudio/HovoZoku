namespace Dreamteck.Forever
{
    using UnityEngine;
    using UnityEngine.Serialization;
    using Dreamteck.Splines;

    public partial class LevelSegment : MonoBehaviour
    {
        //Old class used for migration. Will be removed in 1.05
        [System.Serializable]
        public class LevelSegmentCustomPath
        {
            [SerializeField]
            internal LevelSegment segment = null;
            [SerializeField]
            internal Transform transform = null;
            [SerializeField]
            internal SplinePoint[] localPoints = new SplinePoint[0];
            [SerializeField]
            [HideInInspector]
            [FormerlySerializedAs("_spline")]
            internal Spline spline = new Spline(Spline.Type.Bezier, 10);
        }

        [System.Serializable]
        public class LevelSegmentPath : SplinePath
        {
            public string name = "Path";
            public Color color = Color.white;
            public bool seamlessEnds = true;
            [SerializeField]
            [HideInInspector]
            private LevelSegment segment;
            [SerializeField]
            [HideInInspector]
            private Transform transform;
            public SplinePoint[] localPoints = new SplinePoint[0];

            internal void MigrateOldCustompath(LevelSegmentCustomPath old)
            {
                segment = old.segment;
                transform = old.transform; 
                spline = old.spline;
                localPoints = old.localPoints;
            }

            internal LevelSegmentPath(LevelSegment s)
            {
                segment = s;
                transform = s.transform;
                spline = new Spline(Spline.Type.Bezier);
            }

            internal LevelSegmentPath(LevelSegmentCustomPath old)
            {
                MigrateOldCustompath(old);
            }

            public void Transform()
            {
                if (spline == null || localPoints == null) return;
                if (spline.points.Length != localPoints.Length) spline.points = new SplinePoint[localPoints.Length];
                for (int i = 0; i < localPoints.Length; i++)
                {
                    if (segment.type == Type.Extruded)
                    {
                        Vector3 pos = localPoints[i].position;
                        switch (segment.axis)
                        {
                            case Axis.X:
                                pos.x = Mathf.Clamp(localPoints[i].position.x, segment.bounds.min.x, segment.bounds.max.x);
                                break;
                            case Axis.Y:
                                pos.y = Mathf.Clamp(localPoints[i].position.y, segment.bounds.min.y, segment.bounds.max.y);
                                break;
                            case Axis.Z:
                                pos.z = Mathf.Clamp(localPoints[i].position.z, segment.bounds.min.z, segment.bounds.max.z);
                                break;
                        }
                        localPoints[i].SetPosition(pos);
                    }
                    spline.points[i].size = localPoints[i].size;
                    spline.points[i].color = localPoints[i].color;
                    TransformPoint(ref localPoints[i], ref spline.points[i]);
                }
            }

            public void InverseTransform()
            {
                if (spline == null || localPoints == null) return;
                if (spline.points.Length != localPoints.Length) localPoints = new SplinePoint[spline.points.Length];
                for (int i = 0; i < localPoints.Length; i++)
                {
                    localPoints[i].size = spline.points[i].size;
                    localPoints[i].color = spline.points[i].color;
                    InverseTransformPoint(ref spline.points[i], ref localPoints[i]);
                }
            }

            public LevelSegmentPath Copy()
            {
                LevelSegmentPath newPath = new LevelSegmentPath(segment);
                newPath.name = name;
                newPath.localPoints = new SplinePoint[localPoints.Length];
                localPoints.CopyTo(newPath.localPoints, 0);
                newPath.spline = new Spline(spline.type, spline.sampleRate);
                newPath.Transform();
                return newPath;
            }

            void TransformPoint(ref SplinePoint source, ref SplinePoint target)
            {
                target.position = transform.TransformPoint(source.position);
                target.tangent = transform.TransformPoint(source.tangent);
                target.tangent2 = transform.TransformPoint(source.tangent2);
                target.normal = transform.TransformDirection(source.normal).normalized;
            }

            void InverseTransformPoint(ref SplinePoint source, ref SplinePoint target)
            {
                target.position = transform.InverseTransformPoint(source.position);
                target.tangent = transform.InverseTransformPoint(source.tangent);
                target.tangent2 = transform.InverseTransformPoint(source.tangent2);
                target.normal = transform.InverseTransformDirection(source.normal).normalized;
            }
        }
    }
}
