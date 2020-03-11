using UnityEngine;
namespace Dreamteck.Forever
{ 
    [AddComponentMenu("Dreamteck/Forever/Floating Origin")]
    public class FloatingOrigin : MonoBehaviour
    {
        public Transform cameraTransform;
        public bool x = true;
        public bool y = true;
        public bool z = true;
        public delegate void FloatingOriginHandler(Vector3 direction);
        public static event FloatingOriginHandler onOriginOffset;
        Transform trs;

        public float originResetDistance = 999f;

        private void Awake()
        {
            trs = transform;
        }

        private void Start()
        {
            if(cameraTransform == null)
            {
                Camera cam = Camera.main;
                if (cam != null) cameraTransform = cam.transform;
                else
                {
                    cam = Camera.current;
                    if (cam != null) cameraTransform = cam.transform;
                }
            }
        }

        private void OnEnable()
        {
            if(GetComponent<LevelGenerator>() == null)
            {
                Debug.LogError("The Floating origin is not attached to the Level Generator object but instead is attached to " + name + " - disabling.");
                enabled = false;
                return;
            }
        }

        void LateUpdate()
        {
            bool outOfBounds = false;
            if (x && Mathf.Abs(cameraTransform.position.x) > originResetDistance) outOfBounds = true;
            else if (y && Mathf.Abs(cameraTransform.position.y) > originResetDistance) outOfBounds = true;
            else if (z && Mathf.Abs(cameraTransform.position.z) > originResetDistance) outOfBounds = true;

            if (outOfBounds)
            {
                if (LevelSegment.generationState == LevelSegment.GenerationState.Idle)
                {
                    Vector3 delta = cameraTransform.position;
                    if (!x) delta.x = 0f;
                    if (!y) delta.y = 0f;
                    if (!z) delta.z = 0f;
                    foreach (Transform child in trs) child.position -= delta;
                    if (onOriginOffset != null) onOriginOffset(delta);
                }
            }
        }
    }
}
