namespace Dreamteck.Forever
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class SegmentSequence
    {
        public bool enabled = true;
        public string name = "";
        public bool isCustom = false;
        public CustomSequence customSequence = null;
        public SegmentDefinition[] segments = new SegmentDefinition[0];
        public LevelPathGenerator customPathGenerator;
        public int spawnCount = 1;
        public enum Type { Ordered, Random, Custom }
        public Type type = Type.Ordered;
        public SegmentShuffle customShuffle = null;
        private int currentSegmentIndex = 0;
        public bool preventRepeat = false;
        private bool stopped = false;
        private SegmentDefinition _lastDefinition = null;

        public delegate SegmentDefinition SegmentShuffleHandler(SegmentSequence sequence, int index);

        SegmentShuffleHandler shuffle;

        public void Initialize()
        {
            if (isCustom)
            {
                customSequence.Initialize();
                return;
            }
            _lastDefinition = null;
            currentSegmentIndex = 0;
            stopped = false;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].nested && segments[i].nestedSequence != null) segments[i].nestedSequence.Initialize();
            }
           
            switch (type)
            {
                case Type.Ordered: shuffle = SegmentShuffle.GetOrderedSegment; break;
                case Type.Random: shuffle = SegmentShuffle.GetRandomSegment; break;
                case Type.Custom:
                    if (customShuffle != null)
                    {
                        customShuffle.Reset();
                        shuffle = customShuffle.Get;
                    }
                    break;
            }
        }

        public void Stop()
        {
            if (isCustom)
            {
                customSequence.Stop();
                return;
            }
            if (type == Type.Random) currentSegmentIndex = spawnCount - 1;
            else currentSegmentIndex = segments.Length - 1;
            stopped = true;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].nested) segments[i].nestedSequence.Stop();
            }
        }

        public SegmentDefinition Next()
        {
            if(_lastDefinition != null && _lastDefinition.nested && !_lastDefinition.nestedSequence.IsDone())
            {
                return _lastDefinition.nestedSequence.Next();
            }

            if (isCustom)
            {
                return new SegmentDefinition(customSequence.Next());
            }
            if (segments.Length == 0)
            {
                return null;
            }

            _lastDefinition = shuffle(this, currentSegmentIndex);
            currentSegmentIndex++;
            if (_lastDefinition.nested)
            {
                _lastDefinition.nestedSequence.Initialize();
                return _lastDefinition.nestedSequence.Next();
            } else
            {
                return _lastDefinition;
            }
        }

        public bool IsDone()
        {
            if (isCustom) return customSequence.IsDone();
            if (stopped) return true;
            if (segments.Length == 0) return true;

            if (_lastDefinition == null || !_lastDefinition.nested || _lastDefinition.nestedSequence.IsDone())
            {
                switch (type)
                {
                    case Type.Ordered: return currentSegmentIndex >= segments.Length;
                    case Type.Random: if (spawnCount == 0) return false; return currentSegmentIndex >= spawnCount;
                    case Type.Custom: if (customShuffle != null) return customShuffle.IsDone(); break;
                }
            }
            return false;
        }

        public SegmentSequence Duplicate()
        {
            SegmentSequence sequence = new SegmentSequence();
            sequence.enabled = enabled;
            sequence.name = name;
            sequence.preventRepeat = preventRepeat;
            sequence.isCustom = isCustom;
            sequence.customSequence = customSequence;
            sequence.spawnCount = spawnCount;
            sequence.type = type;
            sequence.customShuffle = customShuffle;
            sequence.segments = new SegmentDefinition[segments.Length];
            for (int i = 0; i < segments.Length; i++) sequence.segments[i] = segments[i].Duplicate();
            return sequence;
        }
    }


    [System.Serializable]
    public class SegmentSequenceCollection : ISerializationCallbackReceiver
    {
        public SegmentSequence[] sequences = new SegmentSequence[0];
        [SerializeField]
        private List<SequenceSerialization> sequencePositions = new List<SequenceSerialization>();
        [SerializeField]
        private List<SegmentSequence> allSequences = new List<SegmentSequence>();

        [System.Serializable]
        internal class SequenceSerialization
        {
            [SerializeField]
            internal int parent = -1;
            [SerializeField]
            internal int segmentIndex = -1;

            internal SequenceSerialization(int p, int s)
            {
                parent = p;
                segmentIndex = s;
            }
        }

        public void OnBeforeSerialize()
        {
            if (Application.isPlaying) return;
            allSequences.Clear();
            sequencePositions.Clear();
            for (int i = 0; i < sequences.Length; i++)
            {
                UnpackSequence(sequences[i], -1, -1, ref allSequences, ref sequencePositions);
            }
        }

        public void OnAfterDeserialize()
        {
            if (sequencePositions.Count == 0) return;
            List<SegmentSequence> sequenceList = new List<SegmentSequence>();
            for (int i = 0; i < allSequences.Count; i++)
            {
                if (sequencePositions[i].parent < 0) sequenceList.Add(allSequences[i]);
                else
                {
                    allSequences[sequencePositions[i].parent].segments[sequencePositions[i].segmentIndex].nestedSequence = allSequences[i];
                }
            }
            sequences = sequenceList.ToArray();
        }

        void UnpackSequence(SegmentSequence sequence, int parent, int segmentIndex, ref List<SegmentSequence> flat, ref List<SequenceSerialization> parentIndices)
        {
            flat.Add(sequence);
            parentIndices.Add(new SequenceSerialization(parent, segmentIndex));
            int parentIndex = flat.Count - 1;
            for (int i = 0; i < sequence.segments.Length; i++)
            {
                if (sequence.segments[i].nestedSequence != null)
                {
                    UnpackSequence(sequence.segments[i].nestedSequence, parentIndex, i, ref flat, ref parentIndices);
                }
            }
        }
    }
}
