using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Dreamteck.Splines;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Serialization;
#endif

namespace Dreamteck.Forever
{
    [AddComponentMenu("Dreamteck/Forever/Level Segment")]
    [RequireComponent(typeof(Rigidbody))]
    [System.Serializable]
    public partial class LevelSegment : MonoBehaviour, ISerializationCallbackReceiver
    {
        public delegate void SegmentEnterHandler(LevelSegment segment);
        public enum Type { Extruded, Custom }
        public enum Axis { X, Y, Z }
        [HideInInspector]
        public Type type = Type.Extruded;
        [HideInInspector]
        public Transform customEntrance;
        [HideInInspector]
        public Transform customExit;
        [HideInInspector]
        public bool customKeepUpright = false;
        public int customMainPath
        {
            get { return _customMainPath; }
            set
            {
                _customMainPath = value;
                if (_customMainPath < -1) _customMainPath = -1;
                else if (_customMainPath >= customPaths.Length) _customMainPath = customPaths.Length - 1;
            }
        }
        [HideInInspector]
        [SerializeField]
        private int _customMainPath = -1;
        [HideInInspector]
        public Axis axis = Axis.Z;


        [HideInInspector]
#if UNITY_EDITOR
        [FormerlySerializedAs("customPaths")]
#endif
        public LevelSegmentCustomPath[] oldCustomPaths = null;
        [HideInInspector]
        public LevelSegmentPath[] customPaths = new LevelSegmentPath[0];

        [HideInInspector]
        public ObjectProperty[] objectProperties = new ObjectProperty[0];
        [SerializeField]
        [HideInInspector]
        private TS_Bounds bounds = null;
        [SerializeField]
        [HideInInspector]
        private Builder[] builders = new Builder[0];

        [System.NonSerialized]
        public int index = 0;
        [System.NonSerialized]
        public SplinePath path = new SplinePath();
        [HideInInspector]
        [System.NonSerialized]
        public int sampleRate = 10;
        [System.NonSerialized]
        public LevelSegment next = null;
        [System.NonSerialized]
        public LevelSegment previous = null;
        [System.NonSerialized]
        public Level level = null;
        [System.NonSerialized]
        public bool stitch = true;
        private Matrix4x4 rootMatrix;
        private Matrix4x4 inverseRootMatrix;

        [SerializeField]
        [HideInInspector]
        private Object[] uniqueResources = new Object[0];

        public static event SegmentEnterHandler onSegmentEntered;

        private bool _activated = false;


        public bool activated
        {
            get
            {
                return _activated;
            }
        }

        public bool isReady
        {
            get
            {
                return _activated && (_extruded || type == Type.Custom);
            }
        }

        //Think of a way to move this into SegmentBuilder
        public enum GenerationState { Idle, Free, Extruding, Preparing, Destroying }; 
        public static GenerationState generationState
        {
            get { return _generationState; }
        }
        private static GenerationState _generationState = GenerationState.Idle;
        public delegate void EmptyHandler();
        public event EmptyHandler onActivate;
        public event EmptyHandler onEnter;

#if UNITY_EDITOR
        [HideInInspector]
        public bool unpacked = false;
        [HideInInspector]
        public bool alwaysDraw = false;
        List<ObjectProperty> discoveredProperties = new List<ObjectProperty>();
        [HideInInspector]
        public bool drawGeneratedSpline = true;
        [HideInInspector]
        public bool drawGeneratedSamples = true;
        [HideInInspector]
        public float drawSampleScale = 1f;
        [HideInInspector]
        public bool drawEntranceAndExit = true;
        [HideInInspector]
        public bool drawBounds = true;
        [HideInInspector]
        public bool drawCustomPaths = true;
        [HideInInspector]
        public int editorChildCount = 0;

#endif

        void Reset()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if(rb != null) rb.isKinematic = true;
#if UNITY_EDITOR
            unpacked = true;
            type = ForeverPrefs.newSegmentType;
#endif
        }

        protected virtual void Awake()
        {
            //Deactivate all registered colliders for the extrusion for perofmrnace reasons
            for (int i = 0; i < objectProperties.Length; i++)
            {
                objectProperties[i].collidersEnabled = new bool[objectProperties[i].colliders.Length];
                for (int j = 0; j < objectProperties[i].colliders.Length; j++)
                {
                    objectProperties[i].collidersEnabled[j] = objectProperties[i].colliders[j].enabled;
                    objectProperties[i].colliders[j].enabled = false;
                }
            }
        }

        public void Start()
        {
            //Register unique resources to be unloaded when the segment is destroyed
            for (int i = 0; i < uniqueResources.Length; i++) ResourceManagement.RegisterUnloadableResource(uniqueResources[i], index);
            SegmentExtruder.instance.AddToBuildQueue(this);
        }

        private void OnEnable()
        {
            FloatingOrigin.onOriginOffset += OnOriginOffset;
        }

        private void OnDisable()
        {
            FloatingOrigin.onOriginOffset -= OnOriginOffset;
        }

        /// <summary>
        /// Handle floating origin - offset all generated splines and samples
        /// </summary>
        /// <param name="dir"></param>
        void OnOriginOffset(Vector3 dir)
        {
            for (int i = 0; i < path.samples.Length; i++) path.samples[i].position -= dir;
            if (path != null)
            {
                for (int i = 0; i < path.spline.points.Length; i++)
                {
                    path.spline.points[i].position -= dir;
                    path.spline.points[i].tangent -= dir;
                    path.spline.points[i].tangent2 -= dir;
                }
            }
            for (int i = 0; i < customPaths.Length; i++)
            {
                for (int j = 0; j < customPaths[i].spline.points.Length; j++)
                {
                    customPaths[i].spline.points[j].position -= dir;
                    customPaths[i].spline.points[j].tangent -= dir;
                    customPaths[i].spline.points[j].tangent2 -= dir;
                }
                for (int j = 0; j < customPaths[i].samples.Length; j++) customPaths[i].samples[j].position -= dir;
            }
        }

        /// <summary>
        /// Initialize the level segment. Called by the LevelGenerator.
        /// </summary>
        /// <param name="i"></param>
        public void Initialize(int i)
        {
            index = i;
            _generationState = GenerationState.Free;
            if (previous != null) previous.next = this;
        }

        /// <summary>
        /// Get the calculated segment bounds.
        /// </summary>
        /// <returns></returns>
        public TS_Bounds GetBounds()
        {
            return new TS_Bounds(bounds.min, bounds.max, bounds.center);
        }

        public static void ResetGenerationState()
        {
            _generationState = GenerationState.Idle;
        }

        /// <summary>
        /// Queue the segment for destruction. It will be destroyed when the generator is not busy
        /// </summary>
        public void Destroy()
        {
            StartCoroutine(DestroyRoutine());
        }

        IEnumerator DestroyRoutine()
        {
            float timer = Time.time;
            while (_generationState != GenerationState.Idle)
            {
                if (Time.time - timer >= 3f) break;
                yield return null;
            }
            _generationState = GenerationState.Destroying;
            yield return null;
            Destroy(gameObject);
            yield return null;
        }

        /// <summary>
        /// Forces the segment to be destroyed immediately without waiting for the generation queue to free
        /// </summary>
        public void DestroyImmediate()
        {
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!alwaysDraw) return;
            Vector3 a = transform.TransformPoint(bounds.min);
            Vector3 b = transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
            Vector3 c = transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
            Vector3 d = transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));

            Vector3 e = transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
            Vector3 f = transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
            Vector3 g = transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));
            Vector3 h = transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);

            Gizmos.DrawLine(e, f);
            Gizmos.DrawLine(f, g);
            Gizmos.DrawLine(g, h);
            Gizmos.DrawLine(h, e);

            Gizmos.DrawLine(a, e);
            Gizmos.DrawLine(b, f);
            Gizmos.DrawLine(c, g);
            Gizmos.DrawLine(d, h);
        }
#endif

        protected virtual void OnDestroy()
        {
            //Destroy generated resources
            for (int i = 0; i < objectProperties.Length; i++) objectProperties[i].Dispose();
            //Unload assets used only by this segment and no upcoming segments
            ResourceManagement.UnloadResources(index);
            if (_generationState == GenerationState.Destroying) _generationState = GenerationState.Idle;
        }

        /// <summary>
        /// Create a custom path for the segment. Points will automatically be generated based on the segment type and axis.
        /// </summary>
        /// <param name="name">Name of the path</param>
        public void AddCustomPath(string name)
        {
            LevelSegmentPath path = new LevelSegmentPath(this);
            path.name = name;
            SplinePoint[] points = new SplinePoint[2];
            Vector3 pos = Vector3.zero;
            switch (type)
            {
                case Type.Extruded:
                    switch (axis)
                    {
                        case Axis.X:
                            pos = -Vector3.right * bounds.size.x * 0.5f;
                            points[0] = new SplinePoint(pos, pos - Vector3.right * bounds.size.x / 3f, Vector3.up, 1f, Color.white);
                            pos = Vector3.right * bounds.size.x * 0.5f;
                            points[1] = new SplinePoint(pos, pos - Vector3.right * bounds.size.x / 3f, Vector3.up, 1f, Color.white);
                            break;
                        case Axis.Y:
                            pos = -Vector3.up * bounds.size.y * 0.5f;
                            points[0] = new SplinePoint(pos, pos - Vector3.up * bounds.size.y / 3f, -Vector3.forward, 1f, Color.white);
                            pos = Vector3.up * bounds.size.y * 0.5f;
                            points[1] = new SplinePoint(pos, pos - Vector3.up * bounds.size.y / 3f, -Vector3.forward, 1f, Color.white);
                            break;
                        case Axis.Z:
                            pos = -Vector3.forward * bounds.size.z * 0.5f;
                            points[0] = new SplinePoint(pos, pos - Vector3.forward * bounds.size.z / 3f, Vector3.up, 1f, Color.white);
                            pos = Vector3.forward * bounds.size.z * 0.5f;
                            points[1] = new SplinePoint(pos, pos -Vector3.forward * bounds.size.z / 3f, Vector3.up, 1f, Color.white);
                            break;
                    }
                    break;

                case Type.Custom:
                    float distance = Vector3.Distance(transform.InverseTransformPoint(customEntrance.position), transform.InverseTransformPoint(customExit.position));
                    pos = transform.InverseTransformPoint(customEntrance.position);
                    points[0] = new SplinePoint(pos, pos - transform.InverseTransformDirection(customEntrance.forward) * distance / 3f, transform.InverseTransformDirection(customEntrance.up), 1f, Color.white);
                    pos = transform.InverseTransformPoint(customExit.position);
                    points[1] = new SplinePoint(pos, pos - transform.InverseTransformDirection(customExit.forward) * distance / 3f, transform.InverseTransformDirection(customExit.up), 1f, Color.white);
                    break;
            }
            path.localPoints = points;
            ArrayUtility.Add(ref customPaths, path);
        }

        /// <summary>
        /// Removes the custom path at the given index
        /// </summary>
        /// <param name="index">Index of the path</param>
        public void RemoveCustomPath(int index)
        {
            ArrayUtility.RemoveAt(ref customPaths, index);
        }

        /// <summary>
        /// Evaluate the generated path of the segment. This is the path after the segment is extruded.
        /// </summary>
        /// <param name="percent">Percent along the segment path [0-1]</param>
        /// <param name="result">The SplineSample object to write the result into</param>
        /// <param name="mode">If set to EvaluateMode.Accurate, the actual spline will be evaluated instead of the cached samples.</param>
        public void Evaluate(double percent, SplineSample result, SplinePath.EvaluateMode mode = SplinePath.EvaluateMode.Cached)
        {
            path.Evaluate(percent, result, mode);
        }

        /// <summary>
        /// Evaluate the generated path of the segment and return only the position. This is the path after the segment is extruded.
        /// </summary>
        /// <param name="percent">Percent along the segment path [0-1]</param>
        /// <param name="mode">If set to EvaluateMode.Accurate, the actual spline will be evaluated instead of the cached samples.</param>
        /// <returns></returns>
        public Vector3 EvaluatePosition(double percent, SplinePath.EvaluateMode mode = SplinePath.EvaluateMode.Cached)
        {
            return path.EvaluatePosition(percent, mode);
        }

        /// <summary>
        /// Calculates the length of the generated path in world units.
        /// </summary>
        /// <param name="from">The start of the segment to calculate the length of</param>
        /// <param name="to">The end of the segment</param>
        /// <returns></returns>
        public virtual float CalculateLength(double from = 0.0, double to = 1.0, SplinePath.EvaluateMode mode = SplinePath.EvaluateMode.Cached)
        {
            return path.CalculateLength(from, to, mode);
        }

        /// <summary>
        /// Returns the percent from the spline at a given distance from the start point
        /// </summary>
        /// <param name="start">The start point</param>
        /// <param name="distance">The distance to travel</param>
        /// <param name="direction">The direction towards which to move</param>
        /// <param name="traveled">Returns the distance actually traveled. Usually equal to distance but could be less if the travel distance exceeds the remaining spline length.</param>
        /// <param name="mode">If set to EvaluateMode.Accurate, the actual spline will be evaluated instead of the cached samples.</param>
        /// <returns></returns>
        public double Travel(double start, float distance, Spline.Direction direction, out float traveled, SplinePath.EvaluateMode mode = SplinePath.EvaluateMode.Cached)
        {
            return path.Travel(start, distance, direction, out traveled, mode);
        }

        /// <summary>
        /// Project a world space point onto the spline and write to a SplineSample.
        /// </summary>
        /// <param name="point">3D Point in world space</param>
        /// <param name="result">The SplineSample object to write the result into</param>
        /// <param name="from">Sample from [0-1] default: 0.0</param>
        /// <param name="to">Sample to [0-1] default: 1.0</param>
        /// <returns></returns>
        public virtual void Project(Vector3 point, SplineSample result, double from = 0.0, double to = 1.0, SplinePath.EvaluateMode mode = SplinePath.EvaluateMode.Cached)
        {
            path.Project(point, result, from, to, mode);
        }

        /// <summary>
        /// Activate the segment after it has been extruded - will run all registered builders.
        /// </summary>
        public void Activate()
        {
            StartCoroutine(ActivateRoutine());
        }

        IEnumerator ActivateRoutine()
        {
            while (!_extruded && type == Type.Extruded) yield return null;
            while (_generationState != GenerationState.Idle) yield return null;
            _generationState = GenerationState.Preparing;
            //Activate colliders back
            for (int i = 0; i < objectProperties.Length; i++)
            {
                for (int j = 0; j < objectProperties[i].colliders.Length; j++)
                {
                    if (objectProperties[i].colliders[j] == null) continue;
                    objectProperties[i].colliders[j].enabled = objectProperties[i].collidersEnabled[j];
                }
            }
            //Run all builders
            for (int i = 0; i < builders.Length; i++)
            {
                if (builders[i].enabled && builders[i].gameObject.activeInHierarchy && builders[i].queue == Builder.Queue.OnActivate) builders[i].StartBuild(this);
            }
            bool doneBuilding = false;
            float timeout = Time.time + 10f;
            while (!doneBuilding)
            {
                if (timeout <= Time.time)
                {
                    for (int i = 0; i < builders.Length; i++)
                    {
                        if (builders[i].enabled && builders[i].gameObject.activeInHierarchy && !builders[i].isDone) Debug.Log("Segment: " + name + "Builder " + builders[i].name + " is taking too long.");
                    }
                    break;
                }

                doneBuilding = true;
                for (int i = 0; i < builders.Length; i++)
                {
                    if (builders[i].enabled && builders[i].gameObject.activeInHierarchy && builders[i].isBuilding)
                    {
                        doneBuilding = false;
                        break;
                    }
                }
                yield return null;
            }
            OnActivate();
        }

        private void OnActivate()
        {
            if (_generationState == GenerationState.Preparing) _generationState = GenerationState.Idle;
            _activated = true;
            if (onActivate != null) onActivate();
        }

        /// <summary>
        /// Enter the segment. Called from gameplay logic to let the system know in which segment the player is.
        /// </summary>
        public void Enter()
        {
            if (onSegmentEntered != null) onSegmentEntered(this);
            if (onEnter != null) onEnter();
        }

#if UNITY_EDITOR
        private bool IsObjectPrefabInstance(GameObject obj)
        {
#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab && PrefabUtility.GetOutermostPrefabInstanceRoot(obj) == obj;
#else
            return PrefabUtility.GetPrefabType(obj) == PrefabType.PrefabInstance && PrefabUtility.FindValidUploadPrefabInstanceRoot(obj) == obj;
#endif
        }


        public void EditorPack()
        {
            UpdateReferences();
            List<Builder> builderList = new List<Builder>();
            List<Transform> allChildren = new List<Transform>();
            SceneUtility.GetChildrenRecursively(transform, ref allChildren);
            editorChildCount = allChildren.Count;
            List<Object> unique = new List<Object>();

            for (int i = 0; i < allChildren.Count; i++)
            {
#region Extract Unique Resources
                ResourceManagement.ExtractUniqueResources(allChildren[i].gameObject, unique);
#endregion
            
#region Find Builders
                Builder[] builders = allChildren[i].GetComponents<Builder>();
                for (int j = 0; j < builders.Length; j++)
                {
                    builders[j].OnPack();
                    builderList.Add(builders[j]);
                }
#endregion

                EditorUtility.DisplayProgressBar("Caching segment", "Caching objects for faster generation.", ((float)i / (allChildren.Count - 1)));
            }
            uniqueResources = unique.ToArray();
            EditorUtility.ClearProgressBar();
            builders = builderList.ToArray();
            if (builders.Length > 1) SortBuilders(0, builders.Length);
            unpacked = false;
        }

        public void EditorUnpack()
        {
            List<Transform> allChildren = new List<Transform>();
            SceneUtility.GetChildrenRecursively(transform, ref allChildren);
            editorChildCount = allChildren.Count;
            for (int i = 0; i < allChildren.Count; i++)
            {
                Builder[] builders = allChildren[i].GetComponents<Builder>();
                for (int j = 0; j < builders.Length; j++) builders[j].OnUnpack();
            }
            unpacked = true;
            UpdateReferences();
        }

        public void UpdateReferences()
        {
#if UNITY_2018_3_OR_NEWER
#else
            if (AssetDatabase.Contains(gameObject)) return;
#endif
            editorChildCount = 0;
            TransformUtility.GetChildCount(transform, ref editorChildCount);
            discoveredProperties.Clear();
            GetBendableObjects(transform, ref discoveredProperties);
            if(objectProperties.Length != discoveredProperties.Count) objectProperties = discoveredProperties.ToArray();
            else
            {
                for (int i = 0; i < discoveredProperties.Count; i++) objectProperties[i] = discoveredProperties[i];
            }
            CalculateBounds();
        }


        public void GetBendableObjects(Transform current, ref List<ObjectProperty> propertyList)
        {
            ObjectProperty property = GetProperty(current);
            propertyList.Add(GetProperty(current));
            if (property.extrusionSettings.ignoreChildren) return;
            foreach (Transform child in current) GetBendableObjects(child, ref propertyList);
        }

        private void CalculateBounds()
        {
            if (bounds == null) bounds = new TS_Bounds(Vector3.zero, Vector3.zero);
            bounds.min = bounds.max = Vector3.zero;
            for (int i = 0; i < objectProperties.Length; i++) GetBounds(ref objectProperties[i]);
            for (int i = 0; i < customPaths.Length; i++)
            {
                customPaths[i].Transform();
                for (int j = 0; j < customPaths[i].localPoints.Length; j++)
                {
                    //ExpandBounds(ref bounds, customPaths[i].localPoints[j].position);
                }
            }
        }

        private void GetBounds(ref ObjectProperty property)
        {
            if ((property.extrusionSettings.boundsInclusion & ExtrusionSettings.BoundsInclusion.Transform) != 0)
            {
                Vector3 localPos = transform.InverseTransformPoint(property.transform.position);
                ExpandBounds(ref bounds, localPos);
            }

            if ((property.extrusionSettings.boundsInclusion & ExtrusionSettings.BoundsInclusion.Mesh) != 0)
            {
                MeshFilter filter = property.meshFilter;
                if (filter == null) filter = property.transform.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        Vector3 localPos = property.transform.TransformPoint(vertices[j]);
                        localPos = transform.InverseTransformPoint(localPos);
                        ExpandBounds(ref bounds, localPos);
                    }
                }
            }

            if ((property.extrusionSettings.boundsInclusion & ExtrusionSettings.BoundsInclusion.Sprite) != 0)
            {
                SpriteRenderer spriteRend = property.spriteRenderer;
                if (spriteRend == null) spriteRend = property.transform.GetComponent<SpriteRenderer>();
                if (spriteRend)
                {
                    Vector2[] vertices = property.spriteRenderer.sprite.vertices;
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        Vector3 localPos = property.transform.TransformPoint(vertices[j]);
                        localPos = transform.InverseTransformPoint(localPos);
                        ExpandBounds(ref bounds, localPos);
                    }
                }
            }

            if ((property.extrusionSettings.boundsInclusion & ExtrusionSettings.BoundsInclusion.Collider) != 0)
            {
                foreach (Collider collider in property.transform.GetComponents<Collider>())
                {
                    ExpandBounds(ref bounds, transform.InverseTransformPoint(collider.bounds.min));
                    ExpandBounds(ref bounds, transform.InverseTransformPoint(collider.bounds.max));
                }
                foreach (Collider2D collider in property.transform.GetComponents<Collider2D>())
                {
                    ExpandBounds(ref bounds, transform.InverseTransformPoint(collider.bounds.min));
                    ExpandBounds(ref bounds, transform.InverseTransformPoint(collider.bounds.max));
                }
            }

#if DREAMTECK_SPLINES
                if ((property.extrusionSettings.boundsInclusion & ExtrusionSettings.BoundsInclusion.Spline) != 0)
            {
                SplineComputer splineComputer = property.splineComputer;
                if (splineComputer == null) splineComputer = property.transform.GetComponent<SplineComputer>();
                if (splineComputer != null)
                {
                    if (property.extrusionSettings.bendSpline && property.splineComputer != null)
                    {
                        for (int j = 0; j < property.splineComputer.pointCount; j++)
                        {
                            Vector3 localPos = transform.InverseTransformPoint(property.splineComputer.GetPoint(j).position);
                            ExpandBounds(ref bounds, localPos);
                        }
                    }
                }
            }
#endif
            bounds.CreateFromMinMax(bounds.min, bounds.max);
        }

        void ExpandBounds(ref TS_Bounds b, Vector3 point)
        {
            if (point.x < b.min.x) b.min.x = point.x;
            if (point.y < b.min.y) b.min.y = point.y;
            if (point.z < b.min.z) b.min.z = point.z;
            if (point.x > b.max.x) b.max.x = point.x;
            if (point.y > b.max.y) b.max.y = point.y;
            if (point.z > b.max.z) b.max.z = point.z;
        }

        private ObjectProperty GetProperty(Transform t)
        {
             //Create a new bend property for each child
            for (int i = 0; i < objectProperties.Length; i++)
            {
                //Search for properties that have the same trasform and copy their settings
                try
                {
                    if (objectProperties[i].transform == t)
                    {
                        objectProperties[i].GetReferences();
                        return objectProperties[i];
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("SEGMENT " + name + " PROPERTY " + i + " ERROR: " + ex);
                }
            }
            return new ObjectProperty(t, t == transform);
        }

        void SortBuilders(int left, int right)
        {
            int i = left - 1,
            j = right;

            while (true)
            {
                int d = builders[left].priority;
                do i++; while (builders[i].priority < d);
                do j--; while (builders[j].priority > d);

                if (i < j)
                {
                    Builder tmp = builders[i];
                    builders[i] = builders[j];
                    builders[j] = tmp;
                }
                else
                {
                    if (left < j) SortBuilders(left, j);
                    if (++j < right) SortBuilders(j, right);
                    return;
                }
            }
        }
#endif

        public void OnBeforeSerialize()
        {

        }

        //Migrate Custom paths made with the old system to the new
        public void OnAfterDeserialize()
        {
            if (oldCustomPaths != null && customPaths.Length == 0)
            {
                customPaths = new LevelSegmentPath[oldCustomPaths.Length];
                for (int i = 0; i < customPaths.Length; i++) customPaths[i] = new LevelSegmentPath(oldCustomPaths[i]);
                oldCustomPaths = null;
            }
        }
    }
}
