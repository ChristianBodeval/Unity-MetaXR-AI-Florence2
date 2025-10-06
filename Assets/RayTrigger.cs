using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class RayTrigger : MonoBehaviour
{
    [System.Flags]
    public enum AimSources
    {
        None = 0,
        LeftController = 1 << 0,
        RightController = 1 << 1,
    }

    [Header("Aim")]
    [EnumFlags, SerializeField] private AimSources aimFrom = AimSources.RightController;
    [SerializeField] private float maxAimDistance = 10f;
    [SerializeField] private LayerMask aimLayerMask = ~0;
    [SerializeField] private bool drawAimGizmo = true;

    [ShowIf(nameof(NeedsLeftController))][SerializeField] private Transform leftController;
    [ShowIf(nameof(NeedsRightController))][SerializeField] private Transform rightController;

    [Header("Input (OVR)")]
    [Tooltip("Button to use when aiming with LEFT controller.")]
    [SerializeField] private OVRInput.Button removeButtonLeft = OVRInput.Button.Three; // X (default)
    [Tooltip("Button to use when aiming with RIGHT controller.")]
    [SerializeField] private OVRInput.Button removeButtonRight = OVRInput.Button.Two;  // B (default)

    [Header("Presence FX (Optional)")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Material foundMaterial;
    [SerializeField] private Material normalMaterial;

    [Header("Events")]
    public UnityEvent OnAimedAt;          // fires when aiming begins
    public UnityEvent OnNoLongerAimed;    // fires when aiming ends
    public UnityEvent OnClicked;          // fires on button press while aimed
    [System.Serializable] public class HitEvent : UnityEvent<RaycastHit> { }
    public HitEvent OnClickedWithHit;     // same, but passes the RaycastHit

    // State
    private AimSources _activeHitSource = AimSources.None;
    private bool _isAimedAt;
    private Collider _collider;
    private RaycastHit _lastHit; // stored so we can pass it to events

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        TryAutoFindControllers();
        ApplyPresence(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!_collider) _collider = GetComponent<Collider>();
        TryAutoFindControllers();
    }
#endif

    private void Update()
    {
        bool wasAimedAt = _isAimedAt;
        _isAimedAt = false;
        _activeHitSource = AimSources.None;
        _lastHit = default;

        if (TryGetBestHit(out RaycastHit bestHit, out AimSources hitSource))
        {
            if (bestHit.collider == _collider)
            {
                _isAimedAt = true;
                _activeHitSource = hitSource;
                _lastHit = bestHit;

                var button = GetRemoveButtonFor(hitSource);
                if (OVRInput.GetDown(button))
                {
                    Debug.Log($"[RayTrigger:{name}] Clicked. Source={_activeHitSource}, Button={button}, HitPoint={bestHit.point}");
                    OnClicked?.Invoke();
                    OnClickedWithHit?.Invoke(bestHit);
                }
            }
        }

        if (_isAimedAt != wasAimedAt)
        {
            if (_isAimedAt)
            {
                Debug.Log($"[RayTrigger:{name}] Aimed at.");
                OnAimedAt?.Invoke();
            }
            else
            {
                Debug.Log($"[RayTrigger:{name}] No longer aimed at.");
                OnNoLongerAimed?.Invoke();
            }
            ApplyPresence(_isAimedAt);
        }
    }

    // --- Raycast helpers ---
    private bool TryGetBestHit(out RaycastHit bestHit, out AimSources hitSource)
    {
        bestHit = default;
        hitSource = AimSources.None;
        float bestDist = float.PositiveInfinity;

        if (HasFlag(aimFrom, AimSources.LeftController))
            CheckRay(ResolveLeftController(), AimSources.LeftController, ref bestHit, ref hitSource, ref bestDist);

        if (HasFlag(aimFrom, AimSources.RightController))
            CheckRay(ResolveRightController(), AimSources.RightController, ref bestHit, ref hitSource, ref bestDist);

        return hitSource != AimSources.None;
    }

    private void CheckRay(Transform origin, AimSources source, ref RaycastHit bestHit, ref AimSources hitSource, ref float bestDist)
    {
        if (!origin) return;
        var ray = new Ray(origin.position, origin.forward);
        if (Physics.Raycast(ray, out var hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Collide))
        {
            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestHit = hit;
                hitSource = source;
            }
        }
    }

    private static bool HasFlag(AimSources value, AimSources flag) => (value & flag) == flag;

    private OVRInput.Button GetRemoveButtonFor(AimSources source)
    {
        // Inspector-configurable per controller
        return source == AimSources.LeftController ? removeButtonLeft : removeButtonRight;
    }

    private void OnDrawGizmos()
    {
        if (!drawAimGizmo || !_isAimedAt) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(Vector3.zero, 0.08f);
    }

    // --- Controller resolution ---
    private void TryAutoFindControllers()
    {
        if (!leftController)
            leftController = FindTransformByNames("LeftHandAnchor", "LeftControllerAnchor", "OVRLeftControllerAnchor", "LeftHand", "LeftController");
        if (!rightController)
            rightController = FindTransformByNames("RightHandAnchor", "RightControllerAnchor", "OVRRightControllerAnchor", "RightHand", "RightController");
    }

    private Transform ResolveLeftController() { if (leftController) return leftController; TryAutoFindControllers(); return leftController; }
    private Transform ResolveRightController() { if (rightController) return rightController; TryAutoFindControllers(); return rightController; }

    private static Transform FindTransformByNames(params string[] names)
    {
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go) return go.transform;
        }
        return null;
    }

    private void ApplyPresence(bool aimed)
    {
        if (!meshRenderer) return;

        if (foundMaterial && normalMaterial)
        {
            var mats = meshRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = aimed ? foundMaterial : normalMaterial;
            meshRenderer.sharedMaterials = mats;
        }
        else if (foundMaterial || normalMaterial)
        {
            meshRenderer.sharedMaterial = aimed
                ? (foundMaterial ? foundMaterial : meshRenderer.sharedMaterial)
                : (normalMaterial ? normalMaterial : meshRenderer.sharedMaterial);
        }
    }

    // --- NaughtyAttributes visibility ---
    private bool NeedsLeftController() => HasFlag(aimFrom, AimSources.LeftController);
    private bool NeedsRightController() => HasFlag(aimFrom, AimSources.RightController);
}
