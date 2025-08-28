using UnityEngine;
using TMPro;
using Meta.XR;
using Meta.XR.BuildingBlocks;

namespace PresentFutures.XRAI.Spatial
{
    [RequireComponent(typeof(Collider))]
    public class SpatialLabel : MonoBehaviour
    {
        public enum AimSource
        {
            MainCamera,
            LeftController,
            RightController
        }

        [Header("UI")]
        [SerializeField] private string _name;     // Exposed to inspector
        [SerializeField] private TMP_Text text;

        [Header("Aim & Remove")]
        [Tooltip("Where the aiming ray originates from.")]
        [SerializeField] private AimSource aimFrom = AimSource.MainCamera;

        [Tooltip("Optional. If AimFrom = Left/RightController and this is assigned, we use its position/forward.")]
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;




        [Tooltip("How far the player can target labels for removal.")]
        [SerializeField] private float maxAimDistance = 10f;

        [Tooltip("Layers that can be aimed at. Put your SpatialLabel objects on this layer (or 'Default').")]
        [SerializeField] private LayerMask aimLayerMask = ~0;

        [Tooltip("Draw a small gizmo when this label is being aimed at.")]
        [SerializeField] private bool drawAimGizmo = true;

        private static Camera _mainCam;
        private Collider _col;
        private bool _isAimedAt;
        private OVRSpatialAnchor anchor;

        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _name = value;
                    if (text != null) text.text = _name;
                }
            }
        }

        [Header("References")]
        public GameObject InteractUI;

        private void Awake()
        {
            anchor = GetComponent<OVRSpatialAnchor>();
            _col = GetComponent<Collider>();
            if (_col == null)
            {
                Debug.LogWarning("SpatialLabel requires a Collider for aiming/removal. Adding a BoxCollider by default.", this);
                _col = gameObject.AddComponent<BoxCollider>();
            }

            if (_mainCam == null) _mainCam = Camera.main;

            // Best-effort auto-find for controller anchors if not assigned
            TryAutoFindControllers();
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(_name) && text != null)
            {
                text.text = _name;
            }
        }

        private void Start()
        {
            if (SpatialLabelManager.Instance != null)
            {
                SpatialLabelManager.Instance.RegisterLabel(this);
            }
            else
            {
                Debug.LogWarning("SpatialLabelManager instance not found!");
            }

            if (!string.IsNullOrEmpty(_name) && text != null)
            {
                text.text = _name;
            }

            if (_mainCam == null) _mainCam = Camera.main;
        }

        private void Update()
        {
            Ray ray;
            if (!TryGetAimRay(out ray)) return;

            bool wasAimedAt = _isAimedAt;
            _isAimedAt = false;

            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == _col)
                {
                    _isAimedAt = true;

                    if (!wasAimedAt) Debug.Log($"[{name}] Aimed at.");

                    // Remove on B / Button.Two (right controller) or LMB (editor)
                    if (OVRInput.GetDown(OVRInput.Button.Two))
                    {
                        Remove();
                    }
                    if (Input.GetMouseButtonDown(0))
                    {
                        Remove();
                    }
                }
            }

            if (InteractUI) InteractUI.SetActive(_isAimedAt);

            if (wasAimedAt && !_isAimedAt)
            {
                Debug.Log($"[{name}] No longer aimed at.");
            }
        }

        /// <summary>
        /// Compose the aiming ray based on the selected source.
        /// </summary>
        private bool TryGetAimRay(out Ray ray)
        {
            // Default
            ray = default;

            switch (aimFrom)
            {
                case AimSource.MainCamera:
                    if (_mainCam == null) _mainCam = Camera.main;
                    if (_mainCam == null) return false;
                    ray = new Ray(_mainCam.transform.position, _mainCam.transform.forward);
                    return true;

                case AimSource.LeftController:
                    {
                        Transform t = ResolveLeftController();
                        if (!t) return false;
                        ray = new Ray(t.position, t.forward);
                        return true;
                    }

                case AimSource.RightController:
                    {
                        Transform t = ResolveRightController();
                        if (!t) return false;
                        ray = new Ray(t.position, t.forward);
                        return true;
                    }
            }

            return false;
        }

        /// <summary>
        /// Removes this label from the scene and unregisters it from the manager.
        /// </summary>
        public void Remove()
        {
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.UnsaveAnchor(anchor);
            }

            Debug.Log($"[{name}] Removed.");
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.UnsaveAnchor(anchor);
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawAimGizmo || !_isAimedAt) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, 0.08f);
        }

        // -------------------- Helpers --------------------

        private void TryAutoFindControllers()
        {
            // If user didn’t assign, try common anchor names under OVRCameraRig
            if (!leftController)
            {
                leftController = FindTransformByNames(
                    "LeftHandAnchor", "LeftControllerAnchor", "OVRLeftControllerAnchor", "LeftHand", "LeftController");
            }
            if (!rightController)
            {
                rightController = FindTransformByNames(
                    "RightHandAnchor", "RightControllerAnchor", "OVRRightControllerAnchor", "RightHand", "RightController");
            }
        }

        private Transform ResolveLeftController()
        {
            if (leftController) return leftController;
            TryAutoFindControllers();
            return leftController;
        }

        private Transform ResolveRightController()
        {
            if (rightController) return rightController;
            TryAutoFindControllers();
            return rightController;
        }

        private static Transform FindTransformByNames(params string[] names)
        {
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go) return go.transform;
            }
            return null;
        }
    }
}
