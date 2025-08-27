using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace PresentFutures.XRAI.UI
{
    public class OVRInputPromptUI : MonoBehaviour
    {
        [Header("Pick input (matches ControllerUI)")]
        [SerializeField] private ControllerUI.ButtonType input = ControllerUI.ButtonType.A;

        [Header("Hookups")]
        [SerializeField] private ControllerUI controllerUI;   // controller diagram to highlight
        [SerializeField] private TMP_Text label;              // e.g. "Press A to do Remove"

        [Header("Behavior")]
        [Tooltip("Text that fills the second blank: 'Press <INPUT> to do <THIS>'")]
        [SerializeField] private string actionDisplayName = "do action";
        [Tooltip("Invoke on GetDown (true) or GetUp (false)")]
        [SerializeField] private bool useGetDownInvoke = true;

        [Header("Action")]
        public UnityEvent onPerformed;

        void Reset()
        {
            if (!label) label = GetComponentInChildren<TMP_Text>();
            if (!controllerUI)
            {
                controllerUI = GetComponentInParent<ControllerUI>();
                if (!controllerUI) controllerUI = FindObjectOfType<ControllerUI>();
            }
        }

        void Awake()
        {
            if (!label) label = GetComponentInChildren<TMP_Text>();
            if (!controllerUI)
            {
                controllerUI = GetComponentInParent<ControllerUI>();
                if (!controllerUI) controllerUI = FindObjectOfType<ControllerUI>();
            }

            // Initial UI
            ApplyHighlight();
            UpdateLabel();
        }

        void OnValidate()
        {
            ApplyHighlight();
            UpdateLabel();
        }

        void Update()
        {
            // Keep diagram in sync (handles runtime enum changes)
            ApplyHighlight();

            // Input check
            var map = Map(input);
            if (map.valid)
            {
                bool fired = useGetDownInvoke
                    ? OVRInput.GetDown(map.btn, map.ctrl)
                    : OVRInput.GetUp(map.btn, map.ctrl);

                if (fired) SafeInvoke();
            }

            // Keep label fresh (cheap)
            UpdateLabel();
        }

        void SafeInvoke()
        {
            try { onPerformed?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void ApplyHighlight()
        {
            if (controllerUI) controllerUI.SetHighlight(input);
        }

        void UpdateLabel()
        {
            if (!label) return;

            var map = Map(input);
            string inputName = map.humanName;
            if (string.IsNullOrWhiteSpace(inputName)) inputName = "Input";

            string actionText = string.IsNullOrWhiteSpace(actionDisplayName) ? "do action" : actionDisplayName;
            label.text = $"Press {inputName} to do {actionText}";
        }

        // ---------- Mapping ----------
        struct InputMap
        {
            public bool valid;
            public OVRInput.Button btn;
            public OVRInput.Controller ctrl;
            public string humanName;
        }

        static InputMap Map(ControllerUI.ButtonType t)
        {
            switch (t)
            {
                // LEFT
                case ControllerUI.ButtonType.X:
                    return New(OVRInput.Button.One, OVRInput.Controller.LTouch, "X");
                case ControllerUI.ButtonType.Y:
                    return New(OVRInput.Button.Two, OVRInput.Controller.LTouch, "Y");
                case ControllerUI.ButtonType.TriggerL:
                    return New(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch, "Left Trigger");
                case ControllerUI.ButtonType.SecondaryL:
                    return New(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch, "Left Grip");
                case ControllerUI.ButtonType.ThumbstickL:
                    return New(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch, "Left Stick");
                case ControllerUI.ButtonType.BurgerMenu:
                    return New(OVRInput.Button.Start, OVRInput.Controller.LTouch, "Menu");

                // RIGHT
                case ControllerUI.ButtonType.A:
                    return New(OVRInput.Button.One, OVRInput.Controller.RTouch, "A");
                case ControllerUI.ButtonType.B:
                    return New(OVRInput.Button.Two, OVRInput.Controller.RTouch, "B");
                case ControllerUI.ButtonType.TriggerR:
                    return New(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.RTouch, "Right Trigger");
                case ControllerUI.ButtonType.SecondaryR:
                    return New(OVRInput.Button.SecondaryHandTrigger, OVRInput.Controller.RTouch, "Right Grip");
                case ControllerUI.ButtonType.ThumbstickR:
                    return New(OVRInput.Button.SecondaryThumbstick, OVRInput.Controller.RTouch, "Right Stick");
                case ControllerUI.ButtonType.MetaButton:
                    return New(OVRInput.Button.Back, OVRInput.Controller.RTouch, "Oculus");

                // None / unsupported
                default:
                    return default;
            }

            static InputMap New(OVRInput.Button b, OVRInput.Controller c, string human)
                => new InputMap { valid = true, btn = b, ctrl = c, humanName = human };
        }
    }
}
