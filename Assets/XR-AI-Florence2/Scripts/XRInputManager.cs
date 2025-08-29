using OVR; // optional; harmless if missing as long as Oculus Integration is installed
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif
// Place in any namespace you prefer

public class XRInputManager : MonoBehaviour
{
    [Header("Hookups (auto-found if left empty)")]
    public PresentFutures.XRAI.Florence.Florence2Controller florence;
    public SpatialAnchorManager anchors;

    [Header("Behavior")]
    [Tooltip("Use handy keyboard fallbacks in the Editor (A=Detect, L=Load, C=Clear, N=Quick Anchor).")]
    public bool enableKeyboardFallback = true;

    [Tooltip("Quick-anchor: spawn at right controller pose (mirrors your SpatialAnchorManager.CreateSpatialAnchor()).")]
    public bool enableQuickAnchor = true;

    [Header("Unity Events (optional)")]
    public UnityEvent OnDetectRequested;     // Fired when we request Florence detection
    public UnityEvent OnLoadAnchors;         // Fired when we load saved anchors
    public UnityEvent OnClearAllAnchors;     // Fired when we clear/unsave all
    public UnityEvent OnQuickAnchor;         // Fired when we create a quick test anchor

    // --- Default bindings (Meta Quest Touch controllers) ---
    // Right controller:
    //   A  => Detect (OVRInput.Button.One)
    //   Right Grip => Clear/Unsave All (OVRInput.Button.PrimaryHandTrigger, RTouch)
    //   Right Thumbstick Click => Load (OVRInput.Button.PrimaryThumbstick, RTouch)
    //
    // Optional:
    //   Right Index Trigger click => Quick Anchor (toggle via enableQuickAnchor)

    void Awake()
    {
        if (!florence) florence = FindObjectOfType<PresentFutures.XRAI.Florence.Florence2Controller>();
        if (!anchors) anchors = FindObjectOfType<SpatialAnchorManager>();
    }

    private void Start()
    {
        Invoke("LoadAll",2);
    }

    void Update()
    {
        PollOVRInputs();
        if (enableKeyboardFallback) PollKeyboardFallbacks();
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.A) Detect();
            if (e.keyCode == KeyCode.C) ClearAll();
            if (e.keyCode == KeyCode.L) LoadAll();
            if (e.keyCode == KeyCode.N) QuickAnchorAtRightController();
        }
    }
#endif

    private void PollOVRInputs()
    {
        // A (Right) => Detect
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            Detect();

        // Right Grip => Clear/Unsave All
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            ClearAll();

        // Right Thumbstick Click => Load
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
            LoadAll();

        // Optional: Quick Anchor on Right Index Trigger
        if (enableQuickAnchor && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            QuickAnchorAtRightController();
    }

    public bool useKeyboardNotSimulator;

    private void PollKeyboardFallbacks()
    {
        if (useKeyboardNotSimulator)
        {
            // A => Detect
            if (Input.GetKeyDown(KeyCode.A)) Detect();

            // C => Clear/Unsave All
            if (Input.GetKeyDown(KeyCode.C)) ClearAll();

            // L => Load
            if (Input.GetKeyDown(KeyCode.L)) LoadAll();

            // N => Quick Anchor
            if (enableQuickAnchor && Input.GetKeyDown(KeyCode.N)) QuickAnchorAtRightController();

            if (Input.GetKeyDown(KeyCode.K) && anchors)
                anchors.DeleteAnchorsInSceneOnly();
        }
    }


    // ---- Actions -------------------------------------------------------------

    private void Detect()
    {
        OnDetectRequested?.Invoke();
        if (florence)
        {
            florence.SendRequest();
            Debug.Log("Sending request");
        }
        else
        {
            Debug.LogWarning("[XRInputManager] Florence2Controller not found.");
        }
    }

    private void LoadAll()
    {
        Debug.Log("LOading all");
        OnLoadAnchors?.Invoke();
        if (anchors)
        {
            anchors.LoadSavedAnchors();
        }
        else
        {
            Debug.LogWarning("[XRInputManager] SpatialAnchorManager not found.");
        }
    }

    private void ClearAll()
    {
        Debug.Log("Clearing all");
        OnClearAllAnchors?.Invoke();
        if (anchors)
        {
            // Your manager exposes UnsaveAllAnchors() as private; call the public path by iterating?
            // But you already wired this exact input inside the manager.
            // To keep concerns separated, we call its public helper instead by simulating your existing behavior:
            // Provide a tiny public wrapper if you want to avoid duplication. For now, do the safe route:
            anchors.UnsaveAllAnchors();
        }
        else
        {
            Debug.LogWarning("[XRInputManager] SpatialAnchorManager not found.");
        }
    }

    private void QuickAnchorAtRightController()
    {
        OnQuickAnchor?.Invoke();

        if (!anchors)
        {
            Debug.LogWarning("[XRInputManager] SpatialAnchorManager not found.");
            return;
        }

        // Mirror your CreateSpatialAnchor() (controller-based quick spawn)
        var pos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        var rot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        // Use your existing public method so all the same initialization happens
        anchors.CreateSpatialAnchor();



        // (Optional) If you prefer exact transform, uncomment the line below and comment the method above:
        // Instantiate(anchors.anchorPrefab, pos, rot);
    }
}
