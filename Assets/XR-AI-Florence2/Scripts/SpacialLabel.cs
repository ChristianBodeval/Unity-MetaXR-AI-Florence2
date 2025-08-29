using UnityEngine;
using TMPro;
using Meta.XR;
using Meta.XR.BuildingBlocks;
using System.Threading.Tasks;
using NaughtyAttributes;




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
        [SerializeField] private string _objectName;     // Exposed to inspector
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

        public OVRSpatialAnchor Anchor
        {
            get
            {
                if (anchor == null)
                    anchor = GetComponent<OVRSpatialAnchor>();
                return anchor;
            }
            set => anchor = value;
        }

        public string ObjectName
        {
            get => _objectName;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _objectName = value;
                    if (text != null) text.text = _objectName;
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

        private void OnEnable()
        {
            anchor = GetComponent<OVRSpatialAnchor>();
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(_objectName) && text != null)
            {
                text.text = _objectName;
            }
        }

        private void Start()
        {
            // Register with label manager if present
            if (SpatialLabelManager.Instance != null)
            {
                SpatialLabelManager.Instance.RegisterLabel(this);
            }
            else
            {
                Debug.LogWarning("SpatialLabelManager instance not found!");
            }

            // ✅ Ensure the label name is applied on spawn/load
            if (SpatialAnchorManager.Instance != null && Anchor != null)
            {
                if (SpatialAnchorManager.Instance.TryGetSavedName(Anchor.Uuid, out var savedName))
                {
                    ObjectName = savedName;
                }
                else if (!string.IsNullOrEmpty(_objectName) && text != null)
                {
                    text.text = _objectName;
                }
            }
            else if (!string.IsNullOrEmpty(_objectName) && text != null)
            {
                text.text = _objectName;
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
        public async void Remove()
        {
            SpatialAnchorManager.Instance.UnsaveAnchor(Anchor);
            Disable();
            Invoke(nameof(MyDestoyer), 2f);
        }

        public void MyDestoyer()
        {
            Destroy(gameObject);
        }

        public void Disable()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                child.gameObject.SetActive(false);
            }
        }

        public void Enable()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                child.gameObject.SetActive(true);
            }
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
        public MeshRenderer meshRenderer;
        public Material foundMaterial;
        public Material normalMaterial;

        public void MakePressenceAware(bool isFound)
        {
            if (isFound)
            {
                meshRenderer.material = foundMaterial;
            }
            else
            {
                meshRenderer.material = normalMaterial;
            }
        }
        [Button]
        public void Tester()
        {
            MakePressenceAware(true);
        }
    }
}
