namespace Dreamteck.Forever
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class SegmentDefinition
    {
        public GameObject prefab
        {
            get
            {
                return _prefab;
            }
            set
            {
                if (value == null) return;
                if (value.GetComponent<LevelSegment>() == null) return;
                _prefab = value;
            }
        }
        [System.NonSerialized]
        public SegmentSequence nestedSequence = null;
        public bool nested = false;
        public float randomPickChance = 1f;

        [SerializeField]
        private GameObject _prefab;

        public SegmentDefinition()
        {
            nestedSequence = null;
            nested = false;
        }

        public SegmentDefinition(string nestedName)
        {
            nestedSequence = new SegmentSequence();
            nestedSequence.name = nestedName;
            nested = true;
        }

        public SegmentDefinition(GameObject input)
        {
            prefab = input;
            nested = false;
        }

        public LevelSegment Instantiate()
        {
            GameObject go = Object.Instantiate(_prefab);
            LevelSegment seg = go.GetComponent<LevelSegment>();
            return seg;
        }

        public SegmentDefinition Duplicate()
        {
            SegmentDefinition def = new SegmentDefinition();
            def._prefab = _prefab;
            def.randomPickChance = randomPickChance;
            if (def.nestedSequence != null) def.nestedSequence = nestedSequence.Duplicate();
            def.nested = nested;
            return def;
        }
    }
}
