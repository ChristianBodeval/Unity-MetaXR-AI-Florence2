using UnityEngine;
using TMPro;
using Meta.XR;
using Meta.XR.BuildingBlocks;
using NaughtyAttributes;

namespace PresentFutures.XRAI.Spatial
{
    [RequireComponent(typeof(Collider))]
    public class SpatialLabel : MonoBehaviour
    {
        [System.Flags]
        public enum AimSources
        {
            None = 0,
            MainCamera = 1 << 0,
            LeftController = 1 << 1,
            RightController = 1 << 2
        }

        [Header("Aim & Remove")]
        [Tooltip("Pick one or more sources to aim from.")]
        [EnumFlags][SerializeField] private AimSources aimFrom = AimSources.MainCamera | AimSources.RightController;

        [Tooltip("How far the player can target labels for removal.")]
        [SerializeField] private float maxAimDistance = 10f;

        [Tooltip("Layers that can be aimed at. Put your SpatialLabel objects on this layer (or 'Default').")]
        [SerializeField] private LayerMask aimLayerMask = ~0;

        [Tooltip("Draw a small gizmo when this label is being aimed at.")]
        [SerializeField] private bool drawAimGizmo = true;
        private bool _isAimedAt;

        [ShowIf(nameof(NeedsLeftController))] private Transform leftController;
        [ShowIf(nameof(NeedsRightController))] private Transform rightController;

        [Header("Removal Input")]
        [Tooltip("Removal button used for Left Controller aim.")]
        [SerializeField] private OVRInput.Button removeButtonLeft = OVRInput.Button.Three;   // X by default
        [Tooltip("Removal button used for Right Controller aim.")]
        [SerializeField] private OVRInput.Button removeButtonRight = OVRInput.Button.Two;    // B by default
        [Tooltip("Allow mouse left-click to remove while aiming (Editor).")]
        [SerializeField] private bool allowMouseRemoveInEditor = true;
        [SerializeField] private bool allowRemovingWithController = false;



        [Header("Label & Visuals")]
        [SerializeField] private string _objectName;
        [SerializeField] private TMP_Text text;
        public GameObject InteractUI;
        public GameObject selectedGO;

        [Tooltip("Optional mesh used for presence-aware material swap.")]
        public MeshRenderer meshRenderer;
        public Material foundMaterial;
        public Material normalMaterial;

        [Header("Border")]
        public Transform borderTransform;

        [SerializeField] private float _xBorderValue;
        public float XBorderValue
        {
            get => _xBorderValue;
            set
            {
                _xBorderValue = value;
                if (borderTransform)
                    borderTransform.localScale = new Vector3(_xBorderValue, borderTransform.localScale.y, borderTransform.localScale.z);
            }
        }

        [SerializeField] private float _yBorderValue;
        public float YBorderValue
        {
            get => _yBorderValue;
            set
            {
                _yBorderValue = value;
                if (borderTransform)
                    borderTransform.localScale = new Vector3(borderTransform.localScale.x, _yBorderValue, borderTransform.localScale.z);
            }
        }

        private static Camera _mainCam;
        private BoxCollider _boxCollider;
        private OVRSpatialAnchor _anchor;

        // Which source is currently hitting us this frame (for removal button mapping)
        private AimSources _activeHitSource = AimSources.None;

        public OVRSpatialAnchor Anchor
        {
            get
            {
                if (_anchor == null) _anchor = GetComponent<OVRSpatialAnchor>();
                return _anchor;
            }
            set => _anchor = value;
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

        public bool isHidden;
        public bool isSelected;

        private void Awake()
        {
            _anchor = GetComponent<OVRSpatialAnchor>();

            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider == null)
            {
                _boxCollider = gameObject.AddComponent<BoxCollider>();
                Debug.LogWarning("SpatialLabel added a BoxCollider automatically.", this);
            }

            if (_mainCam == null) _mainCam = Camera.main;
            TryAutoFindControllers();
        }

        private void OnEnable() => _anchor = GetComponent<OVRSpatialAnchor>();

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(_objectName) && text != null) text.text = _objectName;
            if (borderTransform)
                borderTransform.localScale = new Vector3(_xBorderValue, _yBorderValue, borderTransform.localScale.z);
        }

        private void Start()
        {
            if (SpatialLabelManager.Instance != null)
                SpatialLabelManager.Instance.RegisterLabel(this);
            else
                Debug.LogWarning("SpatialLabelManager instance not found!");

            if (SpatialAnchorManager.Instance != null && Anchor != null)
            {
                if (SpatialAnchorManager.Instance.TryGetSavedName(Anchor.Uuid, out var savedName))
                    ObjectName = savedName;
                else if (!string.IsNullOrEmpty(_objectName) && text != null)
                    text.text = _objectName;
            }
            else if (!string.IsNullOrEmpty(_objectName) && text != null)
            {
                text.text = _objectName;
            }

            if (_mainCam == null) _mainCam = Camera.main;
        }

        private void Update()
        {
            bool wasAimedAt = _isAimedAt;
            _isAimedAt = false;
            _activeHitSource = AimSources.None;

            // Check all selected sources; pick the closest hit on *this* collider
            bool hitSomething = TryGetBestHit(out RaycastHit bestHit, out AimSources hitSource);

            if (hitSomething && bestHit.collider == _boxCollider)
            {
                _isAimedAt = true;
                _activeHitSource = hitSource;

#if UNITY_EDITOR
                if (allowMouseRemoveInEditor && Input.GetMouseButtonDown(0)) Remove();
#endif
                if (OVRInput.GetDown(GetRemoveButtonFor(hitSource)) && allowRemovingWithController) Remove();

                if (XRInputManager.Instance != null) XRInputManager.Instance.currentlySelectedAnchor = Anchor;
            }

            if (InteractUI) InteractUI.SetActive(_isAimedAt);
            if (selectedGO) selectedGO.SetActive(_isAimedAt);

            if (wasAimedAt && !_isAimedAt) Debug.Log($"[{name}] No longer aimed at.");
        }

        private bool TryGetBestHit(out RaycastHit bestHit, out AimSources hitSource)
        {
            bestHit = default;
            hitSource = AimSources.None;
            float bestDist = float.PositiveInfinity;

            // Main Camera
            if (HasFlag(aimFrom, AimSources.MainCamera))
            {
                var cam = (_mainCam != null) ? _mainCam : Camera.main;
                if (cam)
                {
                    var ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out var hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.distance < bestDist) { bestDist = hit.distance; bestHit = hit; hitSource = AimSources.MainCamera; }
                    }
                }
            }

            // Left Controller
            if (HasFlag(aimFrom, AimSources.LeftController))
            {
                var t = ResolveLeftController();
                if (t)
                {
                    var ray = new Ray(t.position, t.forward);
                    if (Physics.Raycast(ray, out var hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.distance < bestDist) { bestDist = hit.distance; bestHit = hit; hitSource = AimSources.LeftController; }
                    }
                }
            }

            // Right Controller
            if (HasFlag(aimFrom, AimSources.RightController))
            {
                var t = ResolveRightController();
                if (t)
                {
                    var ray = new Ray(t.position, t.forward);
                    if (Physics.Raycast(ray, out var hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.distance < bestDist) { bestDist = hit.distance; bestHit = hit; hitSource = AimSources.RightController; }
                    }
                }
            }

            return hitSource != AimSources.None;
        }

        private static bool HasFlag(AimSources value, AimSources flag) => (value & flag) == flag;

        private OVRInput.Button GetRemoveButtonFor(AimSources source)
        {
            if (source == AimSources.LeftController) return removeButtonLeft;
            if (source == AimSources.RightController) return removeButtonRight;
            // If aiming via camera, allow right-hand mapping by default
            return removeButtonRight;
        }

        public void Remove()
        {
            if (SpatialAnchorManager.Instance != null && Anchor != null)
                SpatialAnchorManager.Instance.UnsaveAnchor(Anchor);

            Disable();
            Invoke(nameof(MyDestoyer), 0.1f);
        }

        public void MyDestoyer() => Destroy(gameObject);

        public void Disable()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
                child.gameObject.SetActive(false);
        }

        public void Enable()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
                child.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (SpatialAnchorManager.Instance != null && _anchor != null)
                SpatialAnchorManager.Instance.UnsaveAnchor(_anchor);
        }

        private void OnDrawGizmos()
        {
            if (!drawAimGizmo || !_isAimedAt) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, 0.08f);
        }

        // -------- Selection / Visibility --------
        public void Hide(bool b)
        {
            isHidden = b;
            if (meshRenderer) meshRenderer.enabled = !b;
            if (text) text.enabled = !b;
        }

        public void Select(bool b)
        {
            isSelected = b;
            if (selectedGO) selectedGO.SetActive(b);
        }

        [Button] public void SelectTester() => Select(!isSelected);
        [Button] public void HideTester() => Hide(!isHidden);

        public void MakePressenceAware(bool isFound)
        {
            if (!meshRenderer) return;
            if (isFound && foundMaterial != null) meshRenderer.material = foundMaterial;
            else if (normalMaterial != null) meshRenderer.material = normalMaterial;
        }

        [Button] public void Tester() => MakePressenceAware(true);

        // -------- Helpers --------
        private void TryAutoFindControllers()
        {
            if (!leftController)
                leftController = FindTransformByNames("LeftHandAnchor", "LeftControllerAnchor", "OVRLeftControllerAnchor", "LeftHand", "LeftController");
            if (!rightController)
                rightController = FindTransformByNames("RightHandAnchor", "RightControllerAnchor", "OVRRightControllerAnchor", "RightHand", "RightController");
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

        // ----- NaughtyAttributes conditions -----
        private bool NeedsLeftController() => HasFlag(aimFrom, AimSources.LeftController);
        private bool NeedsRightController() => HasFlag(aimFrom, AimSources.RightController);
    }
}
