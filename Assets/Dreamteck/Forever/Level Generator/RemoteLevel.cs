using UnityEngine;
using System.Collections.Generic;

namespace Dreamteck.Forever
{
    [AddComponentMenu("Dreamteck/Forever/Remote Level")]
    public class RemoteLevel : MonoBehaviour
    {
        public SegmentSequenceCollection sequenceCollection = new SegmentSequenceCollection();

        void Awake()
        {
            //Deactivate all segments in the scene
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
