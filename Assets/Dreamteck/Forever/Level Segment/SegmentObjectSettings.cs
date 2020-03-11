namespace Dreamteck.Forever
{
    using UnityEngine;

    [AddComponentMenu("Dreamteck/Forever/Segment Object Settings")]
    public class SegmentObjectSettings : MonoBehaviour
    {
        enum IndexType { Normal, Terminate, Ignore }
        [HideInInspector]
        public ExtrusionSettings settings = new ExtrusionSettings();
    }
}
