using OVR; // optional; harmless if missing as long as Oculus Integration is installed
using UnityEngine;
using UnityEngine.Events;
using System;
using Meta.WitAi.Dictation;
using Oculus.Voice.Dictation;
using Meta.Voice.Samples.Dictation;
using PresentFutures.XRAI.Florence;
using Oculus.Interaction;
using PresentFutures.XRAI.Spatial;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class XRInputManager : MonoBehaviour
{
    public static XRInputManager Instance;

    public RayInteractor leftRay;
    public RayInteractor rightRay;

    public OVRMicrogestureEventSource leftMicrogestures;
    public OVRMicrogestureEventSource rightMicrogestures;

    [Header("Hookups (auto-found if left empty)")]
    public Florence2Controller florenceController;
    public SpatialAnchorManager anchorManager;

    [Header("Attributes")]
    public bool enableKeyboardFallback = true;
    public bool enableQuickAnchor = true;
    public bool useKeyboardNotSimulator;

    [Header("References")]
    public GameObject TranscriptionUI;
    public MultiRequestTranscription textScript;
    public DictationActivation dictationActivation;
    public AppDictationExperience dictationExperience;
    public TranscriptionUI transcriptionUI;
    public VoiceActionHandler voiceActionHandler;

    [Header("Unity Events (optional)")]
    public UnityEvent OnDetectRequested;     // Fired when we request Florence detection
    public UnityEvent OnLoadAnchors;         // Fired when we load saved anchors
    public UnityEvent OnClearAllAnchors;     // Fired when we clear/unsave all
    public UnityEvent OnQuickAnchor;         // Fired when we create a quick test anchor

    // NEW: Directional events (any hand)
    [Header("Directional Events (any hand)")]
    public UnityEvent OnUp;
    public UnityEvent OnDown;
    public UnityEvent OnLeft;
    public UnityEvent OnRight;

    [Header("RunTime values")]
    public OVRSpatialAnchor currentlySelectedAnchor;

    // ─────────────────────────────────────────────────────────────────────
    // ThumbTap Double-Tap (Microgestures)
    // ─────────────────────────────────────────────────────────────────────
    [Header("Microgestures")]
    [Tooltip("Enable to listen for ThumbTap double-tap and trigger ActivateVoiceCommand.")]
    [SerializeField] private bool enableThumbTapDoubleTap = true;

    [Tooltip("Max seconds between two ThumbTaps to count as a double-tap.")]
    [SerializeField] private float thumbTapDoubleWindow = 0.35f;

    private float _lastLeftThumbTapTime = -999f;
    private int _leftThumbTapCount = 0;

    private float _lastRightThumbTapTime = -999f;
    private int _rightThumbTapCount = 0;

    // Keep delegates so we can properly unsubscribe
    private UnityAction<OVRHand.MicrogestureType> _leftGestureHandler;
    private UnityAction<OVRHand.MicrogestureType> _rightGestureHandler;

    // ─────────────────────────────────────────────────────────────────────
    // Thumbstick-to-DPad (either controller)
    // ─────────────────────────────────────────────────────────────────────
    [Header("Thumbstick D-Pad Settings")]
    [Tooltip("Enable to treat thumbstick tilts as Up/Down/Left/Right on either controller.")]
    public bool enableThumbstickDirectional = true;

    [Range(0.2f, 0.95f)]
    [Tooltip("Deadzone threshold magnitude required to register a direction.")]
    public float thumbstickThreshold = 0.6f;

    // Debounce flags so each tilt fires once until released/changed
    private bool _stickUpActive, _stickDownActive, _stickLeftActive, _stickRightActive;

    void Awake()
    {
        Instance = this;
        if (!florenceController) florenceController = FindObjectOfType<PresentFutures.XRAI.Florence.Florence2Controller>();
        if (!anchorManager) anchorManager = FindObjectOfType<SpatialAnchorManager>();
    }

    private void Start()
    {
        Invoke("LoadAll", 2);

        // Hook up microgesture listeners (if provided in inspector)
        if (leftMicrogestures != null)
        {
            _leftGestureHandler = g => OnGestureRecognized(OVRPlugin.Hand.HandLeft, g);
            leftMicrogestures.GestureRecognizedEvent.AddListener(_leftGestureHandler);
        }
        if (rightMicrogestures != null)
        {
            _rightGestureHandler = g => OnGestureRecognized(OVRPlugin.Hand.HandRight, g);
            rightMicrogestures.GestureRecognizedEvent.AddListener(_rightGestureHandler);
        }
    }

    private void OnDestroy()
    {
        // Clean up listeners if we added them
        if (leftMicrogestures != null && _leftGestureHandler != null)
            leftMicrogestures.GestureRecognizedEvent.RemoveListener(_leftGestureHandler);
        if (rightMicrogestures != null && _rightGestureHandler != null)
            rightMicrogestures.GestureRecognizedEvent.RemoveListener(_rightGestureHandler);
    }

    void Update()
    {
        PollOVRInputs();
        if (enableThumbstickDirectional) PollThumbsticksAsDpad();
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

    // ─────────────────────────────────────────────────────────────────────
    // Microgesture callback (both hands) → directional + double-tap
    // ─────────────────────────────────────────────────────────────────────
    private void OnGestureRecognized(OVRPlugin.Hand hand, OVRHand.MicrogestureType gesture)
    {
        // Directional swipes fire events regardless of hand
        switch (gesture)
        {
            case OVRHand.MicrogestureType.SwipeForward:
                OnUp?.Invoke();
                return;
            case OVRHand.MicrogestureType.SwipeBackward:
                OnDown?.Invoke();
                return;
            case OVRHand.MicrogestureType.SwipeLeft:
                OnLeft?.Invoke();
                return;
            case OVRHand.MicrogestureType.SwipeRight:
                OnRight?.Invoke();
                return;
        }

        // ThumbTap double-tap -> ActivateVoiceCommand (existing behavior)
        if (!enableThumbTapDoubleTap) return;
        if (gesture != OVRHand.MicrogestureType.ThumbTap) return;

        float now = Time.time;

        if (hand == OVRPlugin.Hand.HandLeft)
        {
            if (now - _lastLeftThumbTapTime <= thumbTapDoubleWindow) _leftThumbTapCount++;
            else _leftThumbTapCount = 1;

            _lastLeftThumbTapTime = now;
            if (currentlySelectedAnchor != null) currentlySelectedAnchor.GetComponent<SpatialLabel>()?.OnClick?.Invoke();

            if (_leftThumbTapCount >= 2)
            {
                _leftThumbTapCount = 0;
                ActivateVoiceCommand();
            }
        }
        else if (hand == OVRPlugin.Hand.HandRight)
        {
            if (now - _lastRightThumbTapTime <= thumbTapDoubleWindow) _rightThumbTapCount++;
            else _rightThumbTapCount = 1;

            _lastRightThumbTapTime = now;
            if (currentlySelectedAnchor != null) currentlySelectedAnchor.GetComponent<SpatialLabel>()?.OnClick?.Invoke();

            if (_rightThumbTapCount >= 2)
            {
                _rightThumbTapCount = 0;
                ActivateVoiceCommand();
            }
        }
    }

    private void PollOVRInputs()
    {
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

    // Treat either thumbstick as a 4-way D-pad, debounced
    private void PollThumbsticksAsDpad()
    {
        Vector2 left = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        Vector2 right = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        // Merge: whichever has the larger magnitude wins
        Vector2 v = left.sqrMagnitude >= right.sqrMagnitude ? left : right;

        bool up = v.y >= thumbstickThreshold && Mathf.Abs(v.y) >= Mathf.Abs(v.x);
        bool down = v.y <= -thumbstickThreshold && Mathf.Abs(v.y) >= Mathf.Abs(v.x);
        bool rightD = v.x >= thumbstickThreshold && Mathf.Abs(v.x) > Mathf.Abs(v.y);
        bool leftD = v.x <= -thumbstickThreshold && Mathf.Abs(v.x) > Mathf.Abs(v.y);

        // Rising edges
        if (up && !_stickUpActive) { _stickUpActive = true; OnUp?.Invoke(); }
        if (down && !_stickDownActive) { _stickDownActive = true; OnDown?.Invoke(); }
        if (leftD && !_stickLeftActive) { _stickLeftActive = true; OnLeft?.Invoke(); }
        if (rightD && !_stickRightActive) { _stickRightActive = true; OnRight?.Invoke(); }

        // Reset when released or direction changes
        if (!up) _stickUpActive = false;
        if (!down) _stickDownActive = false;
        if (!leftD) _stickLeftActive = false;
        if (!rightD) _stickRightActive = false;
    }

    private void ActivateVoiceCommand()
    {
        if (voiceActionHandler != null)
        {
            voiceActionHandler.ActivateVoiceCommand();
        }
        else
        {
            Debug.LogWarning("[XRInputManager] VoiceActionHandler not assigned.");
        }
    }

    private void PollKeyboardFallbacks()
    {
        if (!useKeyboardNotSimulator) return;

        // A => Detect
        if (Input.GetKeyDown(KeyCode.A)) Detect();

        // C => Clear/Unsave All
        if (Input.GetKeyDown(KeyCode.C)) ClearAll();

        // L => Load
        if (Input.GetKeyDown(KeyCode.L)) LoadAll();

        // N => Quick Anchor
        if (enableQuickAnchor && Input.GetKeyDown(KeyCode.N)) QuickAnchorAtRightController();

        if (Input.GetKeyDown(KeyCode.K) && anchorManager)
            anchorManager.DeleteAnchorsInSceneOnly();

        // Directional testing (either arrows or WASD)
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) OnUp?.Invoke();
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) OnDown?.Invoke();
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) OnLeft?.Invoke();
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) OnRight?.Invoke();
    }

    // ---- Actions -------------------------------------------------------------
    private void Detect()
    {
        OnDetectRequested?.Invoke();
        if (florenceController)
        {
            florenceController.task = Florence2Task.DenseRegionCaption;
            florenceController.SendRequest();
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
        if (anchorManager)
        {
            anchorManager.LoadSavedAnchors();
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
        if (anchorManager)
        {
            anchorManager.UnsaveAllAnchors();
        }
        else
        {
            Debug.LogWarning("[XRInputManager] SpatialAnchorManager not found.");
        }
    }

    private void QuickAnchorAtRightController()
    {
        OnQuickAnchor?.Invoke();

        if (!anchorManager)
        {
            Debug.LogWarning("[XRInputManager] SpatialAnchorManager not found.");
            return;
        }

        var pos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        var rot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
        anchorManager.CreateSpatialAnchor();
    }
}
