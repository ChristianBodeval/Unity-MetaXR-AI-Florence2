using Meta.XR.ImmersiveDebugger.UserInterface;
using UnityEngine;

// Add this if you use OVR types directly
using Oculus; // Optional depending on your assemblies
using System;

public class MenuSelector : MonoBehaviour
{
    [Header("Buttons (assign in Inspector)")]
    [SerializeField] private ToggleHandler closeButton;
    [SerializeField] private ToggleHandler gptButton;
    [SerializeField] private ToggleHandler recordButton;
    [SerializeField] private ToggleHandler deleteButton;

    [Header("State (read-only at runtime)")]
    [SerializeField] private ToggleHandler currentButton;

    [Header("Microgestures (optional)")]
    [Tooltip("Enable to accept microgesture input alongside keyboard.")]
    [SerializeField] private bool enableGestures = true;

    [Tooltip("Left hand OVRMicrogestureEventSource (optional).")]
    private OVRMicrogestureEventSource leftGestureSource;

    [Tooltip("Right hand OVRMicrogestureEventSource (optional).")]
    private OVRMicrogestureEventSource rightGestureSource;

    [Tooltip("Minimum seconds between handling two gesture events (debounce).")]
    [SerializeField] private float gestureDebounceSeconds = 0.15f;

    private float _lastGestureHandledTime = -999f;

    void Start()
    {
        leftGestureSource = XRInputManager.Instance.leftMicrogestures;
        rightGestureSource = XRInputManager.Instance.rightMicrogestures;


        // Default selection is the record button (if assigned)
        if (recordButton != null)
            SetCurrentButton(recordButton);
        else
            Debug.LogWarning("[MenuSelector] 'recordButton' is not assigned.");

        // Hook microgesture events if enabled and sources exist
        if (enableGestures)
        {
            if (leftGestureSource != null)
            {
                leftGestureSource.GestureRecognizedEvent.AddListener(
                    gesture => OnGestureRecognized(OVRPlugin.Hand.HandLeft, gesture));
            }
            if (rightGestureSource != null)
            {
                rightGestureSource.GestureRecognizedEvent.AddListener(
                    gesture => OnGestureRecognized(OVRPlugin.Hand.HandRight, gesture));
            }
        }
    }

    void Update()
    {
        // If something cleared the current selection, fall back to record
        if (currentButton == null && recordButton != null)
            SetCurrentButton(recordButton);

        // --- Keyboard / Gamepad style nav (unchanged) ---
        bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        bool select = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);

        if (currentButton == null) return;

        if (up) NavigateUp();
        else if (down) NavigateDown();
        else if (left) NavigateLeft();
        else if (right) NavigateRight();

        if (select) SelectCurrent();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gesture Handling
    // ─────────────────────────────────────────────────────────────────────
    private void OnGestureRecognized(OVRPlugin.Hand hand, OVRHand.MicrogestureType gesture)
    {
        if (!enableGestures) return;
        if (Time.time - _lastGestureHandledTime < gestureDebounceSeconds) return;

        // Map gestures to the same navigation used by keyboard
        switch (gesture)
        {
            case OVRHand.MicrogestureType.SwipeLeft:
                NavigateLeft();
                break;
            case OVRHand.MicrogestureType.SwipeRight:
                NavigateRight();
                break;
            case OVRHand.MicrogestureType.SwipeForward:
                NavigateUp();
                break;
            case OVRHand.MicrogestureType.SwipeBackward:
                NavigateDown();
                break;
            case OVRHand.MicrogestureType.ThumbTap:
                SelectCurrent();
                break;
            default:
                // Ignore other microgestures
                return;
        }

        _lastGestureHandledTime = Time.time;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Navigation rules (exactly as described in your pseudocode)
    // From Record:  Up -> Close   | Left -> GPT     | Right -> Delete
    // From GPT:     Right -> Record
    // From Delete:  Left  -> Record
    // From Close:   Down  -> Record
    // ─────────────────────────────────────────────────────────────────────
    private void NavigateUp()
    {
        if (currentButton == recordButton && closeButton != null)
            SetCurrentButton(closeButton);
    }

    private void NavigateDown()
    {
        if (currentButton == closeButton && recordButton != null)
            SetCurrentButton(recordButton);
    }

    private void NavigateLeft()
    {
        if (currentButton == recordButton && gptButton != null)
            SetCurrentButton(gptButton);
        else if (currentButton == deleteButton && recordButton != null)
            SetCurrentButton(recordButton);
    }

    private void NavigateRight()
    {
        if (currentButton == recordButton && deleteButton != null)
            SetCurrentButton(deleteButton);
        else if (currentButton == gptButton && recordButton != null)
            SetCurrentButton(recordButton);
    }

    private void SelectCurrent()
    {
        if (currentButton == null) return;
        // Assumes ToggleHandler exposes a bool property 'Value'
        currentButton.Value = !currentButton.Value;
    }

    private void SetCurrentButton(ToggleHandler next)
    {
        if (next == null) return;

        // Clear previous selection
        if (currentButton != null)
            currentButton.IsSelected = false;

        // Apply new selection
        next.IsSelected = true;
        currentButton = next;
    }
}
