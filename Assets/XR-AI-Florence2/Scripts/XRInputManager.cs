using OVR; // optional; harmless if missing as long as Oculus Integration is installed
using UnityEngine;
using UnityEngine.Events;
using System;
using Meta.WitAi.Dictation;
using Oculus.Voice.Dictation;
using Meta.Voice.Samples.Dictation;
using PresentFutures.XRAI.Florence;




#if UNITY_EDITOR
using UnityEditor;
#endif
// Place in any namespace you prefer

public class XRInputManager : MonoBehaviour
{
    public static XRInputManager Instance;
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


    [Header("RunTime values")]
    public OVRSpatialAnchor currentlySelectedAnchor;

    void Awake()
    {
        Instance = this;
        if (!florenceController) florenceController = FindObjectOfType<PresentFutures.XRAI.Florence.Florence2Controller>();
        if (!anchorManager) anchorManager = FindObjectOfType<SpatialAnchorManager>();
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

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
            ActivateVoiceCommand();

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

    private void ActivateVoiceCommand()
    {
        voiceActionHandler.ActivateVoiceCommand();
    }

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

            if (Input.GetKeyDown(KeyCode.K) && anchorManager)
                anchorManager.DeleteAnchorsInSceneOnly();
        }
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
