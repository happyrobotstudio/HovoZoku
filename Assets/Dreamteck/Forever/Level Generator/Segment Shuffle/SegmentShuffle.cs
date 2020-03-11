
namespace Dreamteck.Forever
{
    using UnityEngine;

    public class SegmentShuffle : ScriptableObject
    {
        private static int lastRandom = -1;
        private static SegmentSequence lastSequence = null;
        protected bool isDone = false;

        public bool IsDone()
        {
            return isDone;
        }

        public virtual void Reset()
        {
            isDone = false;
        }

        public virtual SegmentDefinition Get(SegmentSequence sequence, int index)
        {
            if (sequence.segments.Length == 0) return null;
            return sequence.segments[0];
        }

        public static SegmentDefinition GetOrderedSegment(SegmentSequence sequence, int index)
        {
            int segmentIndex = index;
            if (segmentIndex < 0) segmentIndex = 0;
            if (segmentIndex >= sequence.segments.Length) segmentIndex = sequence.segments.Length - 1;
            return sequence.segments[segmentIndex];
        }

        public static SegmentDefinition GetRandomSegment(SegmentSequence sequence, int index)
        {
            if(lastSequence != sequence)
            {
                lastRandom = -1;
                lastSequence = sequence;
            }
            int segmentIndex = GetRandomSegmentByChance(sequence, sequence.preventRepeat ? lastRandom : -1);
            lastRandom = segmentIndex;
            return sequence.segments[segmentIndex];
        }

        internal static int GetRandomSegmentByChance(SegmentSequence sequence, int exclude = -1)
        {
            float totalChance = 0f;
            for (int i = 0; i < sequence.segments.Length; i++)
            {
                if (i == exclude) continue;
                totalChance += sequence.segments[i].randomPickChance;
            }
            float randomValue = Random.Range(0f, totalChance);
            float passed = 0f;
            for (int i = 0; i < sequence.segments.Length; i++)
            {
                if (i == exclude) continue;
                if (sequence.segments[i].randomPickChance <= 0f) continue;
                if (randomValue >= passed && randomValue <= passed + sequence.segments[i].randomPickChance) return i;
                passed += sequence.segments[i].randomPickChance;
            }
            return 0;
        }
    }
}
