namespace Dreamteck.Forever
{
    using UnityEngine;
    using System.Collections;

 
    [AddComponentMenu("Dreamteck/Forever/Builders/Builder")]
    public class Builder : MonoBehaviour //Basic behavior for level segment generation.
    {
        public enum Queue { OnGenerate, OnActivate }
        public Queue queue = Queue.OnGenerate;
        public int priority = 0;
        public bool isBuilding
        {
            get { return _isBuilding; }
        }
        public bool isDone
        {
            get
            {
                return _isDone;
            }
        }
        private bool _isDone = false;
        private bool _isBuilding = false;
        protected bool buildQueued = false;
        [HideInInspector]
        public LevelSegment levelSegment;
        protected Transform trs = null;


        protected virtual void Awake()
        {
            trs = transform;
        }

#if UNITY_EDITOR
        public virtual void OnPack()
        {

        }

        public virtual void OnUnpack()
        {
        }
#endif

        public void StartBuild(LevelSegment segment)
        {
            if (_isDone) return;
            if (_isBuilding) return;
            if (buildQueued) return;
            levelSegment = segment;
            buildQueued = true;
            Build();
            StartCoroutine(BuildRoutine());
        }

        protected virtual void Build()
        {
            
        }

        protected virtual IEnumerator BuildAsync()
        {
            yield return null;
        }

        IEnumerator BuildRoutine()
        {
            yield return StartCoroutine(BuildAsync());
            FinalizeBuild();
        }

        private void FinalizeBuild()
        {
            _isBuilding = buildQueued = false;
            _isDone = true;
        }
    }
}
