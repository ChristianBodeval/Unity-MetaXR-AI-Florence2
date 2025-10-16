using UnityEngine;
using TMPro;
using Meta.XR;
using Meta.XR.BuildingBlocks;
using NaughtyAttributes;
using UnityEngine.Events;
using Oculus.Interaction;

namespace PresentFutures.XRAI.Spatial
{
    [RequireComponent(typeof(Collider))]
    public class SpatialLabel : MonoBehaviour
    {
        #region Types & Enums ─────────────────────────────────────────────────────────────
        [System.Flags]
        public enum AimSources
        {
            None = 0,
            MainCamera = 1 << 0,
            LeftController = 1 << 1,
            RightController = 1 << 2
        }
        #endregion

        #region Inspector: Pointable / Interaction ────────────────────────────────────────
        [Header("MIK: Pointable Events")]
        [Tooltip("If left empty, will auto-find a PointableUnityEventWrapper on this GameObject.")]
        [SerializeField] private PointableUnityEventWrapper pointableWrapper;

        [SerializeField, ReadOnly] private bool _isHovered;
        public bool isHovered => _isHovered;

        [Header("State")]
        public bool isHidden;
        [SerializeField, ReadOnly] public bool isSelected;

        [Header("Pointable Callbacks (Inspector Events)")]
        public UnityEvent OnHoverEntered = new UnityEvent();
        public UnityEvent OnHoverExited = new UnityEvent();
        public UnityEvent OnSelected = new UnityEvent();
        public UnityEvent OnUnselected = new UnityEvent();

        public UnityEvent<bool> OnHoverChanged = new UnityEvent<bool>();
        public UnityEvent<bool> OnSelectedChanged = new UnityEvent<bool>();
        #endregion

        #region Inspector: Label, Text & Visuals ─────────────────────────────────────────
        [Header("Label & Visuals")]
        [SerializeField] private string _objectName;
        [SerializeField] private TMP_Text text;
        public GameObject selectedGO;

        [Tooltip("Optional mesh used for presence-aware material swap.")]
        public MeshRenderer meshRenderer;
        public Material foundMaterial;
        public Material normalMaterial;

        public GameObject note;
        public GameObject openNoteUI;  // Kept (duplicate naming with OpenNoteUIGO below)

        [SerializeField, ReadOnly] private bool _isNoteEnabled;
        public bool isNoteEnabled
        {
            get => _isNoteEnabled;
            set
            {
                _isNoteEnabled = value;
                if (note) note.SetActive(_isNoteEnabled);
            }
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
        #endregion

        #region Inspector: Border Controls ────────────────────────────────────────────────
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
        #endregion

        #region Inspector: Aiming & Removal ───────────────────────────────────────────────
        [Header("Aim & Remove")]
        [Tooltip("Pick one or more sources to aim from.")]
        [EnumFlags][SerializeField] private AimSources aimFrom = AimSources.MainCamera | AimSources.RightController;

        [Tooltip("How far the player can target labels for removal.")]
        [SerializeField] private float maxAimDistance = 10f;

        [Tooltip("Layers that can be aimed at. Put your SpatialLabel objects on this layer (or 'Default').")]
        [SerializeField] private LayerMask aimLayerMask = ~0;

        [Tooltip("Draw a small gizmo when this label is being aimed at.")]
        [SerializeField] private bool drawAimGizmo = true;

        [ShowIf(nameof(NeedsLeftController))] private Transform leftController;
        [ShowIf(nameof(NeedsRightController))] private Transform rightController;

        [Header("Removal Input")]
        [Tooltip("Removal button used for Left Controller aim.")]
        [SerializeField] private OVRInput.Button removeButtonLeft = OVRInput.Button.Three; // X by default
        [Tooltip("Removal button used for Right Controller aim.")]
        [SerializeField] private OVRInput.Button removeButtonRight = OVRInput.Button.Two;   // B by default
        [Tooltip("Allow mouse left-click to remove while aiming (Editor).")]
        [SerializeField] private bool allowMouseRemoveInEditor = true;
        [SerializeField] private bool allowRemovingWithController = false;

        [Header("Aiming Events")]
        [Tooltip("Invoked once when this label becomes aimed at.")]
        public UnityEvent OnAimedAt = new UnityEvent();
        [Tooltip("Invoked once when this label is no longer aimed at.")]
        public UnityEvent OnNotAimedAt = new UnityEvent();
        [Tooltip("Invoked when click input occurs while aimed at (mouse in Editor / controller button).")]
        public UnityEvent OnClick = new UnityEvent();
        #endregion

        #region Backing Components & State ────────────────────────────────────────────────
        private static Camera _mainCam;
        private SphereCollider _sphereCollider;
        private OVRSpatialAnchor _anchor;
        private bool _isAimedAt;
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
        #endregion

        #region Unity Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            _anchor = GetComponent<OVRSpatialAnchor>();

            _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider == null)
            {
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
                Debug.LogWarning("SpatialLabel added a SphereCollider automatically.", this);
            }

            if (_mainCam == null) _mainCam = Camera.main;
            TryAutoFindControllers();
        }

        private void OnEnable()
        {
            _anchor = GetComponent<OVRSpatialAnchor>();
            EnsurePointableWrapper();
            SubscribePointable();
        }

        private void OnDisable()
        {
            UnsubscribePointable();
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(_objectName) && text != null) text.text = _objectName;
            if (borderTransform)
                borderTransform.localScale = new Vector3(_xBorderValue, _yBorderValue, borderTransform.localScale.z);
        }

        private void Start()
        {
            note.SetActive(isNoteEnabled);
            if (openNoteUI) openNoteUI.SetActive(isNoteEnabled);

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
            // Keep original aiming logic intact.
            bool wasAimedAt = _isAimedAt;
            _isAimedAt = false;
            _activeHitSource = AimSources.None;

            bool hitSomething = TryGetBestHit(out RaycastHit bestHit, out AimSources hitSource);

            if (hitSomething && bestHit.collider == _sphereCollider)
            {
                _isAimedAt = true;
                _activeHitSource = hitSource;

#if UNITY_EDITOR
                if (allowMouseRemoveInEditor && Input.GetMouseButtonDown(0))
                {
                    OnClick?.Invoke();
                    Remove();
                }
#endif
                if (OVRInput.GetDown(GetRemoveButtonFor(hitSource)))
                {
                    OnClick?.Invoke();
                    if (allowRemovingWithController) Remove();
                }
            }
            else if (!hitSomething && hitSource == AimSources.RightController && XRInputManager.Instance != null)
            {
                XRInputManager.Instance.rightRay.Forward = Vector3.zero;
            }
            else if (!hitSomething && hitSource == AimSources.LeftController && XRInputManager.Instance != null)
            {
                XRInputManager.Instance.leftRay.Forward = Vector3.zero;
            }

            if (!wasAimedAt && _isAimedAt) OnAimedAt?.Invoke();
            if (wasAimedAt && !_isAimedAt)
            {
                OnNotAimedAt?.Invoke();
                Debug.Log($"[{name}] No longer aimed at.");
            }
        }

        private void OnDestroy()
        {
            if (SpatialAnchorManager.Instance != null && _anchor != null)
                SpatialAnchorManager.Instance.UnsaveAnchor(_anchor);
        }
        #endregion

        #region Pointable Wrapper Hooks ──────────────────────────────────────────────────
        private void EnsurePointableWrapper()
        {
            if (!pointableWrapper)
            {
                pointableWrapper = GetComponent<PointableUnityEventWrapper>();
                if (!pointableWrapper)
                {
                    Debug.LogWarning($"[{name}] No PointableUnityEventWrapper found. Add one to receive hover/select events.");
                }
            }
        }

        private void SubscribePointable()
        {
            if (!pointableWrapper) return;

            pointableWrapper.WhenHover.AddListener(OnMIKHover);
            pointableWrapper.WhenUnhover.AddListener(OnMIKUnhover);
            pointableWrapper.WhenSelect.AddListener(OnMIKSelect);
            pointableWrapper.WhenUnselect.AddListener(OnMIKUnselect);
        }

        private void UnsubscribePointable()
        {
            if (!pointableWrapper) return;

            pointableWrapper.WhenHover.RemoveListener(OnMIKHover);
            pointableWrapper.WhenUnhover.RemoveListener(OnMIKUnhover);
            pointableWrapper.WhenSelect.RemoveListener(OnMIKSelect);
            pointableWrapper.WhenUnselect.RemoveListener(OnMIKUnselect);
        }

        // MIK event handlers (PointerEvent carries interactor info if needed later)
        private void OnMIKHover(PointerEvent evt) => SetHovered(true);
        private void OnMIKUnhover(PointerEvent evt) => SetHovered(false);
        private void OnMIKSelect(PointerEvent evt) => SetSelected(true);
        private void OnMIKUnselect(PointerEvent evt) => SetSelected(false);
        #endregion

        #region Hover/Select State Updates ───────────────────────────────────────────────
        private void SetHovered(bool value)
        {
            if (_isHovered == value) return;
            _isHovered = value;

            if (value)
            {
                OnHoverEntered?.Invoke();
                if (XRInputManager.Instance != null) XRInputManager.Instance.currentlySelectedAnchor = Anchor;
                XRInputManager.Instance.currentlySelectedAnchor = this.Anchor; // Original duplicate line kept
            }
            else
            {
                if (XRInputManager.Instance != null) XRInputManager.Instance.currentlySelectedAnchor = null;
            }
            OnHoverExited?.Invoke(); // Kept as-is (invoked regardless, per original)

            OnHoverChanged?.Invoke(value);
        }

        private void SetSelected(bool value)
        {
            if (isSelected == value) return;
            isSelected = value;

            if (selectedGO) selectedGO.SetActive(value);

            if (value)
            {
                OnSelected?.Invoke();
            }
            else OnUnselected?.Invoke();

            OnSelectedChanged?.Invoke(value);
        }
        #endregion

        #region Public Controls / Existing Helpers ───────────────────────────────────────
        bool isAimable;
        public Collider aimPointerCollider; // Kept (note: also referenced earlier in comments)

        public void SetIsAimable(bool b)
        {
            isAimable = b;
            GetComponent<Collider>().enabled = b;
            if (aimPointerCollider) aimPointerCollider.enabled = b;
        }

        public void SetSelectedArrow(bool b)
        {
            if (selectedGO) selectedGO.SetActive(b);
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
        #endregion

        #region Aiming Logic ─────────────────────────────────────────────────────────────
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
            return removeButtonRight;
        }
        #endregion

        #region Notes & Presence ─────────────────────────────────────────────────────────
        [Button]
        public void NoteEnableTester()
        {
            NoteEnable(!isNoteEnabled);
        }

        public GameObject OpenNoteUIGO; // Kept (duplicate naming with openNoteUI above)

        public void NoteEnable(bool b)
        {
            if (OpenNoteUIGO) OpenNoteUIGO.SetActive(b);
            isNoteEnabled = b;
            if (meshRenderer) meshRenderer.enabled = !b;
            if (note) note.SetActive(b);
        }

        public void Hide(bool b)
        {
            isHidden = b;
            if (meshRenderer) meshRenderer.enabled = !b;
            if (text) text.enabled = !b;
        }

        public void Select(bool b)
        {
            // External callers can still drive selection; we propagate to runtime state + visuals
            SetSelected(b);
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
        #endregion

        #region Gizmos ───────────────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!drawAimGizmo || !_isAimedAt) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, 0.08f);
        }
        #endregion

        #region Controller Resolution Helpers ────────────────────────────────────────────
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

        // NaughtyAttributes conditions
        private bool NeedsLeftController() => HasFlag(aimFrom, AimSources.LeftController);
        private bool NeedsRightController() => HasFlag(aimFrom, AimSources.RightController);
        #endregion
    }
}
