using UnityEngine;

namespace PresentFutures.XRAI.Florence
{
    /// <summary>
    /// Rotates this object to face the camera, but only on the chosen axes.
    /// Works both in Play Mode and in the Editor (if runInEditor is true).
    /// </summary>
    [ExecuteAlways] // allows updates in the editor
    public class FaceCameraAxes : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("If empty, defaults to Camera.main.")]
        [SerializeField] private Transform target;

        [Header("Axes")]
        public bool useX = false;
        public bool useY = true;
        public bool useZ = false;

        [Header("Options")]
        [Tooltip("Flip the X axis (like your original script).")]
        public bool invertX = true;
        public bool invertY = false;
        public bool invertZ = false;

        [Tooltip("Preview rotation in the Editor without Play Mode.")]
        public bool runInEditor = true;

        private void OnEnable()
        {
            if (!target && Camera.main)
                target = Camera.main.transform;
        }

        private void Update()
        {
            if (Application.isPlaying)
                ApplyRotation();
            else if (runInEditor)
                ApplyRotation();
        }

        private void ApplyRotation()
        {
            if (!target) return;

            Vector3 dirToTarget = target.position - transform.position;
            if (dirToTarget.sqrMagnitude < 1e-8f) return;

            Quaternion lookRotation = Quaternion.LookRotation(dirToTarget, Vector3.up);
            Vector3 lookEuler = lookRotation.eulerAngles;
            Vector3 currentEuler = transform.eulerAngles;

            float x = useX ? (invertX ? -lookEuler.x : lookEuler.x) : currentEuler.x;
            float y = useY ? (invertY ? -lookEuler.y : lookEuler.y) : currentEuler.y;
            float z = useZ ? (invertZ ? -lookEuler.z : lookEuler.z) : currentEuler.z;

            transform.rotation = Quaternion.Euler(x, y, z);
        }
    }
}
