using Dreamteck.Splines;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamteck.Forever
{
    public delegate void LevelEnterHandler(Level level, int index);

    [AddComponentMenu("Dreamteck/Forever/Level Generator")]
    public class LevelGenerator : MonoBehaviour
    {
        [HideInInspector]
        public int generateSegmentsAhead = 5;
        [HideInInspector]
        public int activateSegmentsAhead = 2;
        [HideInInspector]
        public int maxSegments = 15;
        public bool buildOnAwake = true;

        public enum Type { Infinite, Finite }
        [HideInInspector]
        public Type type = Type.Infinite;
        [HideInInspector]
        public int finiteSegmentsCount = 10;
        [HideInInspector]
        public bool finiteLoop = false;

        [HideInInspector]
        public Level[] levels = new Level[0];

        public enum LevelIteration { Ordered, OrderedClamp, OrderedLoop, Random, None }
        [HideInInspector]
        public LevelIteration levelIteration = LevelIteration.Ordered;
        [HideInInspector]
        public int startLevel = 0;

        static SplineSample resultAlloc = new SplineSample();



        public Level currentLevel
        {
            get { return levels[levelIndex]; }
        }
        public int currentLevelIndex
        {
            get { return levelIndex; }
        }
        private int levelIndex = 0;

        private bool isLoadingLevel = false;
        private int segmentIndex = 0;

        private List<LevelSegment> _segments = new List<LevelSegment>();
        public List<LevelSegment> segments
        {
            get
            {
                return _segments;
            }
        }
        public static LevelGenerator instance;

        public delegate void EmptyHandler();

        public static event LevelEnterHandler onLevelEntered;
        public static event LevelEnterHandler onLevelLoaded;
        public static event LevelEnterHandler onWillLoadLevel;
        public static event EmptyHandler onReady;
        public static event EmptyHandler onLevelsDepleted;


        [HideInInspector]
        public bool testMode = false;
        [HideInInspector]
        public GameObject[] debugSegments = new GameObject[0];

        public bool ready
        {
            get { return _ready; }
        }
        private bool _ready = false;
        Level enteredLevel = null;
        int _enteredSegment = -1;
        public int enteredSegment
        {
            get { return _enteredSegment; }
        }

        public delegate int LevelChangeHandler(int currentLevel, int levelCount);
        public LevelChangeHandler levelChangeHandler;

        private float _generationProgress = 0f;
        public float generationProgress
        {
            get { return _generationProgress; }
        }

        [SerializeField]
        [HideInInspector]
        private LevelPathGenerator sharedPathGenerator;
        private LevelPathGenerator overridePathGenerator;
        private LevelPathGenerator pathGeneratorInstance;
        public LevelPathGenerator pathGenerator
        {
            get
            {
                if (Application.isPlaying && usePathGeneratorInstance) return pathGeneratorInstance;
                return sharedPathGenerator;
            }
            set
            {
                if (value == sharedPathGenerator || (usePathGeneratorInstance && value == pathGeneratorInstance)) return;
                if (Application.isPlaying && !usePathGeneratorInstance && sharedPathGenerator != null) value.Continue(sharedPathGenerator);
                
                if (Application.isPlaying && usePathGeneratorInstance)
                {
                    if (pathGeneratorInstance != null) Destroy(pathGeneratorInstance);
                    pathGeneratorInstance = Instantiate(value);
                    pathGeneratorInstance.Continue(sharedPathGenerator);
                }
                sharedPathGenerator = value;
            }
        }
        private LevelPathGenerator currentPathGenerator
        {
            get
            {
                if (overridePathGenerator != null) return overridePathGenerator;
                else return pathGenerator;
            }
        }

        [HideInInspector]
        public bool usePathGeneratorInstance = false;

        void Awake()
        {
            instance = this;
            LevelSegment.onSegmentEntered += OnSegmentEntered;
            if (gameObject.GetComponent<SegmentExtruder>() == null) gameObject.AddComponent<SegmentExtruder>();
            if (buildOnAwake) StartGeneration();
        }

        private void OnDestroy()
        {
            LevelSegment.onSegmentEntered -= OnSegmentEntered;
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i].isReady) levels[i].UnloadImmediate();
            }
            onLevelEntered = null;
            onWillLoadLevel = null;
            onLevelLoaded = null;
        }

        IEnumerator StartRoutine()
        {
            _ready = false;
            _generationProgress = 0f;
            while (isLoadingLevel && !testMode) yield return null;
            LevelSegment.ResetGenerationState();
            int count = 0;
            if (type == Type.Finite) count = finiteSegmentsCount;
            else count = 1 + generateSegmentsAhead;
            StartCoroutine(ProgressRoutine(count));
            for (int i = 0; i < count; i++)
            {
                CreateNextSegment();
                yield return new WaitForSeconds(0.1f);
            }
            for (int i = 0; i < _segments.Count; i++)
            {
                while (_segments.Count > i && !_segments[i].extruded && _segments[i].type == LevelSegment.Type.Extruded) yield return null;
                if (type == Type.Finite) _segments[i].Activate();
            }
            if (type == Type.Finite)
            {
                if (finiteLoop)
                {
                    _segments[_segments.Count - 1].next = _segments[0];
                    _segments[0].previous = _segments[_segments.Count - 1];
                }
            }
            _segments[0].Enter();
            while (LevelSegment.generationState != LevelSegment.GenerationState.Idle) yield return null;
            _ready = true;

            
            print("ACTIVATE HOVER");
            HoverPlayer.Instance.ActivateHover();

            
            if (onReady != null) onReady();
        }

        IEnumerator ProgressRoutine(int targetCount)
        {
            while(!_ready)
            {
                _generationProgress = 0f;
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i].type == LevelSegment.Type.Custom || segments[i].extruded) _generationProgress++;
                }
                _generationProgress /= targetCount;
                yield return null;
            }
            _generationProgress = 1f;
        }

        void LoadLevel(Level lvl, bool forceHighPriority)
        {
            if (onWillLoadLevel != null) onWillLoadLevel(lvl, levelIndex);
            levels[levelIndex].onSequenceEntered += OnSequenceEntered;
            if (lvl.remoteSequence) StartCoroutine(LoadRemoteLevel(lvl, forceHighPriority ? ThreadPriority.High : lvl.loadingPriority));
            else lvl.Initialize();
        }

        void UnloadLevel(Level lvl, bool forceHighPriority)
        {
            if (lvl.remoteSequence && lvl.isReady)
            {
                StartCoroutine(UnloadRemoteLevel(lvl, forceHighPriority ? ThreadPriority.High : lvl.loadingPriority));
            }
        }

        IEnumerator LoadRemoteLevel(Level lvl, ThreadPriority priority = ThreadPriority.Normal)
        {
            if (testMode) yield break;
            while (isLoadingLevel) yield return null;
            isLoadingLevel = true;
            yield return StartCoroutine(lvl.Load());
            if (!lvl.isReady) Debug.LogError("Failed loading remote level " + lvl.title);
            else if (onLevelLoaded != null) {
                int index = 0;
                for (int i = 0; i < levels.Length; i++)
                {
                    if(levels[i] == lvl)
                    {
                        index = i;
                        break;
                    }
                }
                onLevelLoaded(lvl, index);
            }
            lvl.Initialize();
            isLoadingLevel = false;
        }

        IEnumerator UnloadRemoteLevel(Level lvl, ThreadPriority priority = ThreadPriority.Normal)
        {
            if (testMode) yield break;
            while (isLoadingLevel) yield return null;
            isLoadingLevel = true;
            yield return null; //Make sure the unloading starts on the next frame to give time to resources to get freed up
            yield return StartCoroutine(lvl.Unload());
            isLoadingLevel = false;
        }

        public void Clear()
        {
            StartCoroutine(ClearRoutine());
        }

        IEnumerator ClearRoutine()
        {
            _ready = false;
            if (usePathGeneratorInstance && overridePathGenerator != null) Destroy(overridePathGenerator);
            overridePathGenerator = null;
            while (isLoadingLevel) yield return null;
            LevelSegment.ResetGenerationState();
            SegmentExtruder.instance.Stop();
            for (int i = 0; i < levels.Length; i++) {
                if (levels[i].remoteSequence && levels[i].isReady)
                {
                    yield return StartCoroutine(UnloadRemoteLevel(levels[i], ThreadPriority.High));
                }
            }
            for (int i = 0; i < _segments.Count; i++) _segments[i].DestroyImmediate();
            ResourceManagement.UnloadResources();
            _segments.Clear();
            enteredLevel = null;
            _enteredSegment = -1;
        }

        public void Restart()
        {
            StartCoroutine(RestartRoutine());
        }

        IEnumerator RestartRoutine()
        {
            if (!_ready && enteredLevel == null)
            {
                StartGeneration();
                yield break;
            }
            yield return StartCoroutine(ClearRoutine());
            StartGeneration();
        }

        public void StartGeneration()
        {
            StopAllCoroutines();
            if (usePathGeneratorInstance)
            {
                if (overridePathGenerator != null) Destroy(overridePathGenerator);
                if (pathGeneratorInstance != null) Destroy(pathGeneratorInstance);
                pathGeneratorInstance = Instantiate(sharedPathGenerator);
            }
            overridePathGenerator = null;
            if (currentPathGenerator == null)
            {
                Debug.LogError("Level Generator " + name + " does not have a Path Generator assigned");
                return;
            }
            
            enteredLevel = null;
            _enteredSegment = -1;
            segmentIndex = 0;
            if (startLevel >= levels.Length) startLevel = levels.Length - 1;
            switch (levelIteration)
            {
                case LevelIteration.Ordered: levelChangeHandler = IncrementClamp; break;
                case LevelIteration.OrderedClamp: levelChangeHandler = IncrementClamp; break;
                case LevelIteration.OrderedLoop: levelChangeHandler = IncrementRepeat; break;
                case LevelIteration.Random: levelChangeHandler = RandomLevel; break;
            }
            levelIndex = startLevel;
            while (!levels[levelIndex].enabled)
            {
                levelIndex++;
                if (levelIndex >= levels.Length) break;
            }
            LoadLevel(levels[levelIndex], true);
            currentPathGenerator.Initialize(this);
            StartCoroutine(StartRoutine());


            Debug.Log("LOADED LEVEL");
            if(MenuController.Instance != null)
                MenuController.Instance.Hide();

        }

        public LevelSegment GetSegmentAtPercent(double percent)
        {
            int pathIndex;
            GlobalToLocalPercent(percent, out pathIndex);
            if (_segments.Count == 0) return null;
            return _segments[pathIndex];
        }

        public LevelSegment FindSegmentForPoint(Vector3 point)
        {
            Project(point, resultAlloc);
            return GetSegmentAtPercent(resultAlloc.percent);
        }

        public void Project(Vector3 point, SplineSample result, bool bypassCache = false)
        {
            if (_segments.Count == 0) return;
            int closestPath = 0;
            float closestDist = Mathf.Infinity;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (!_segments[i].extruded && _segments[i].type == LevelSegment.Type.Extruded) continue;
                _segments[i].Project(point, resultAlloc, 0.0, 1.0, bypassCache ? SplinePath.EvaluateMode.Accurate : SplinePath.EvaluateMode.Cached);
                float dist = (resultAlloc.position - point).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPath = i;
                    result.CopyFrom(resultAlloc);
                }
            }
            result.percent = LocalToGlobalPercent(result.percent, closestPath);
        }

        public float CalculateLength(double from = 0.0, double to = 1.0, double resolution = 1.0)
        {
            if (_segments.Count == 0) return 0f;
            if (to < from)
            {
                double temp = from;
                from = to;
                to = temp;
            }
            int fromSegmentIndex = 0, toSegmentIndex = 0;
            double fromSegmentPercent = 0.0, toSegmentPercent = 0.0;
            fromSegmentPercent = GlobalToLocalPercent(from, out fromSegmentIndex);
            toSegmentPercent = GlobalToLocalPercent(to, out toSegmentIndex);
            float length = 0f;
            for (int i = fromSegmentIndex; i <= toSegmentIndex; i++)
            {
                if (i == fromSegmentIndex) length += segments[i].CalculateLength(fromSegmentPercent, 1.0);
                else if (i == toSegmentIndex) length += segments[i].CalculateLength(toSegmentPercent, 1.0);
                else length += segments[i].CalculateLength();
            }
            return length;
        }

        public double Travel(double start, float distance, Spline.Direction direction)
        {
            if (_segments.Count == 0) return 0.0;
            if (direction == Spline.Direction.Forward && start >= 1.0) return 1.0;
            else if (direction == Spline.Direction.Backward && start <= 0.0) return 0.0;
            if (distance == 0f) return DMath.Clamp01(start);
            float moved = 0f;
            Vector3 lastPosition = EvaluatePosition(start);
            double lastPercent = start;
            int iterations = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                iterations += segments[i].path.spline.iterations;
            }
            int step = iterations - 1;
            int nextSampleIndex = direction == Spline.Direction.Forward ? DMath.CeilInt(start * step) : DMath.FloorInt(start * step);
            float lastDistance = 0f;
            Vector3 pos = Vector3.zero;
            double percent = start;

            while (true)
            {
                percent = (double)nextSampleIndex / step;
                pos = EvaluatePosition(percent);
                lastDistance = Vector3.Distance(pos, lastPosition);
                lastPosition = pos;
                moved += lastDistance;
                if (moved >= distance) break;
                lastPercent = percent;
                if (direction == Spline.Direction.Forward)
                {
                    if (nextSampleIndex == step) break;
                    nextSampleIndex++;
                }
                else
                {
                    if (nextSampleIndex == 0) break;
                    nextSampleIndex--;
                }
            }
            return DMath.Lerp(lastPercent, percent, 1f - (moved - distance) / lastDistance);
        }

        public void Evaluate(double percent, SplineSample result)
        {
            if (_segments.Count == 0) return;
            int pathIndex;
            double localPercent = GlobalToLocalPercent(percent, out pathIndex);
            _segments[pathIndex].Evaluate(localPercent, result);
            result.percent = percent;
        }

        public Vector3 EvaluatePosition(double percent)
        {
            if (_segments.Count == 0) return Vector3.zero;
            int pathIndex;
            double localPercent = GlobalToLocalPercent(percent, out pathIndex);
            return _segments[pathIndex].EvaluatePosition(localPercent);
        }

        public double GlobalToLocalPercent(double percent, out int segmentIndex)
        {
            double segmentValue = percent * _segments.Count;
            segmentIndex = Mathf.Clamp(DMath.FloorInt(segmentValue), 0, _segments.Count - 1);
            if (_segments.Count == 0) return 0.0;
            return DMath.InverseLerp(segmentIndex, segmentIndex + 1, segmentValue);
        }

        public double LocalToGlobalPercent(double localPercent, int segmentIndex)
        {
            if (_segments.Count == 0) return 0.0;
            double percentPerPath = 1.0 / _segments.Count;
            return DMath.Clamp01(segmentIndex * percentPerPath + localPercent * percentPerPath);
        }

        public void NextLevel(bool forceHighPriority = false)
        {
            levels[levelIndex].onSequenceEntered -= OnSequenceEntered;
            levelIndex = GetNextLevelIndex();
            LoadLevel(levels[levelIndex], forceHighPriority);
        }

        private void OnSequenceEntered(SegmentSequence sequence)
        {
            if(sequence.customPathGenerator != null)
            {
                LevelPathGenerator lastGenerator = currentPathGenerator;
                if (usePathGeneratorInstance) overridePathGenerator = Instantiate(sequence.customPathGenerator);
                else overridePathGenerator = sequence.customPathGenerator;
                overridePathGenerator.Continue(lastGenerator);
            } else if(overridePathGenerator != null)
            {
                pathGenerator.Continue(overridePathGenerator);
                if (usePathGeneratorInstance) Destroy(overridePathGenerator);
                overridePathGenerator = null;
            }
        }

        public void SetLevel(int index, bool forceHighPriority = false)
        {
            if (index < 0 || index >= levels.Length) return;
            levels[levelIndex].onSequenceEntered -= OnSequenceEntered;
            levelIndex = index;
            LoadLevel(levels[levelIndex], forceHighPriority);
        }

        int GetNextLevelIndex()
        {
            int nextLevel = levelChangeHandler(levelIndex, levels.Length - 1);
            while (!levels[nextLevel].enabled)
            {
                nextLevel++;
                if (nextLevel >= levels.Length)
                {
                    nextLevel = levelIndex;
                    break;
                }
            }
            return nextLevel;
        }

        int IncrementClamp(int current, int max)
        {
            return Mathf.Clamp(current + 1, 0, max);
        }

        int IncrementRepeat(int current, int max)
        {
            current++;
            if (current > max) current = 0;
            return current;
        }

        int RandomLevel(int current, int max)
        {
            int index = Random.Range(0, levels.Length);
            if (index == current) index++;
            if (index >= max) index = 0;
            return index;
        }

        public void CreateNextSegment()
        {
            StartCoroutine(CreateSegment());
        }

        public void DestroySegment(int index)
        {
            _segments[index].Destroy();
            _segments.RemoveAt(index);
            segmentIndex--;
            if (index >= _segments.Count)
            {
                currentPathGenerator.Continue(_segments[_segments.Count - 1]);
            }
        }

        IEnumerator CreateSegment()
        {
            while (!levels[levelIndex].isReady && !testMode) yield return null;
            HandleLevelChange();
            while (LevelSegment.generationState != LevelSegment.GenerationState.Idle) yield return null;
            if (levels[levelIndex].IsDone() && !testMode)
            {
                yield break;
            }
            LevelSegment segment = null;
            
            if (testMode)
            {
                GameObject go = Instantiate(debugSegments[Random.Range(0, debugSegments.Length)]);
                segment = go.GetComponent<LevelSegment>();
            }
            else
            {
                segment = levels[levelIndex].InstantiateSegment();
            }

            Transform segmentTrs = segment.transform;
            Vector3 spawnPos = segmentTrs.position;
            Quaternion spawnRot = segmentTrs.rotation;
            if (segments.Count > 0)
            {
                SplineSample lastSegmentEndResult = new SplineSample();
                _segments[_segments.Count - 1].Evaluate(1.0, lastSegmentEndResult);
                spawnPos = lastSegmentEndResult.position;
                spawnRot = lastSegmentEndResult.rotation;
                switch (segment.axis)
                {
                    case LevelSegment.Axis.X: spawnRot = Quaternion.AngleAxis(90f, Vector3.up) * spawnRot; break;
                    case LevelSegment.Axis.Y: spawnRot = Quaternion.AngleAxis(90f, Vector3.right) * spawnRot; break;
                }
            }

            segmentTrs.position = spawnPos;
            if (segment.objectProperties[0].extrusionSettings.applyRotation) segmentTrs.rotation = spawnRot;
            

            if(segment.type == LevelSegment.Type.Extruded)
            {
                switch (segment.axis)
                {
                    case LevelSegment.Axis.X:
                        segment.transform.position += segment.transform.right * segment.GetBounds().size.x;
                        break;
                    case LevelSegment.Axis.Y:
                        segment.transform.position += segment.transform.up * segment.GetBounds().size.y;
                        break;
                    case LevelSegment.Axis.Z:
                        segment.transform.position += segment.transform.forward * segment.GetBounds().size.z;
                        break;
                }
            }

            if (_segments.Count > 0) segment.previous = _segments[_segments.Count - 1];
            segment.level = levels[levelIndex];

            if (segment.type == LevelSegment.Type.Custom)
            {
                Quaternion entranceRotationDelta = segment.customEntrance.rotation * Quaternion.Inverse(spawnRot);
                segment.transform.rotation = segment.transform.rotation * Quaternion.Inverse(entranceRotationDelta);
                if(segment.customKeepUpright) segment.transform.rotation = Quaternion.FromToRotation(segment.customEntrance.up, Vector3.up) * segment.transform.rotation;
                Vector3 entranceOffset = segment.transform.position - segment.customEntrance.position;
                segment.transform.position = spawnPos + entranceOffset;
            }

            if (segmentIndex == int.MaxValue) segmentIndex = 2;
            segment.Initialize(segmentIndex++);
            segment.transform.parent = transform;
            currentPathGenerator.GeneratePath(segment);
            _segments.Add(segment);
            //Remove old segments
            if (type == Type.Infinite &&  _segments.Count > maxSegments) StartCoroutine(CleanupRoutine());
            if (levels[levelIndex].IsDone() && !testMode)
            {
                if (levelIteration == LevelIteration.Ordered && levelIndex >= levels.Length - 1)
                {
                    if (onLevelsDepleted != null) onLevelsDepleted();
                    yield break;
                }
                if (levelIteration == LevelIteration.None)
                {
                    if (onLevelsDepleted != null) onLevelsDepleted();
                    yield break;
                }
                NextLevel();
            }
        }

        void HandleLevelChange()
        {
            if (!levels[levelIndex].IsDone() || testMode) return;
            if (levelIteration == LevelIteration.Ordered && levelIndex >= levels.Length - 1)
            {
                if (onLevelsDepleted != null) onLevelsDepleted();
                return;
            }
            if (levelIteration == LevelIteration.None)
            {
                if (onLevelsDepleted != null) onLevelsDepleted();
                return;
            }
            NextLevel();
        }

        IEnumerator CleanupRoutine()
        {
            yield return StartCoroutine(DestroySegmentRoutine(0));
            if (_segments.Count > maxSegments) StartCoroutine(CleanupRoutine());
        }

        //First wait for the SegmentBuilder to start building and only after that queue the destruction. Building should come before destruction
        IEnumerator DestroySegmentRoutine(int index)
        {
            Level segmentLevel = _segments[index].level;
            _segments[index].Destroy();
            if (segmentLevel.remoteSequence)
            {
                bool levelFound = false;
                for (int i = 0; i < _segments.Count; i++)
                {
                    if (_segments[i].level == segmentLevel)
                    {
                        levelFound = true;
                        break;
                    }
                }
                yield return null;
                if (!levelFound) UnloadLevel(segmentLevel, false);
            }
            _segments.RemoveAt(index);
        }

        private float Angle(Vector3 a, Vector3 b)
        {
            float angle = Vector3.Angle(a, b);
            Vector3 cross = Vector3.Cross(a, b);
            if (cross.y < 0) angle = -angle;
            return angle;
        }

        private void OnSegmentEntered(LevelSegment entered)
        {
            _enteredSegment = entered.index;
            if (enteredLevel != entered.level)
            {
                enteredLevel = entered.level;
                int enteredIndex = 0;
                for (int i = 0; i < levels.Length; i++)
                {
                    if (enteredLevel == levels[i])
                    {
                        enteredIndex = i;
                        break;
                    }
                }
                if (onLevelEntered != null) onLevelEntered(enteredLevel, enteredIndex);
            }

            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] == entered)
                {
                    if (type == Type.Infinite)
                    {
                        int segmentsAhead = _segments.Count - (i + 1);
                        if (segmentsAhead < generateSegmentsAhead)
                        {
                            for (int j = segmentsAhead; j < generateSegmentsAhead; j++) CreateNextSegment();
                        }
                        //Segment activation
                        for (int j = i; j <= i + activateSegmentsAhead && j < _segments.Count; j++)
                        {
                            if (!segments[j].activated) _segments[j].Activate();
                        }
                    }
                    break;

                }
            }
        }

    }
}
