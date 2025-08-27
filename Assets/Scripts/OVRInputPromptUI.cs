using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace PresentFutures.XRAI.UI
{
    [RequireComponent(typeof(ControllerIconImage))]
    public class OVRInputPromptUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private ControllerIconImage iconImage;   // uses your existing script
        [SerializeField] private TMP_Text label;                  // "Press A to Remove"
        [SerializeField] private bool showPressedVariantWhileHeld = true;

        [Header("Input")]
        public OVRInput.Button button = OVRInput.Button.PrimaryHandTrigger;
        public OVRInput.Controller controller = OVRInput.Controller.RTouch;
        public bool useGetDownInvoke = true; // true: invoke on GetDown, false: invoke on GetUp

        [Header("Action")]
        [Tooltip("Optional override. If empty, uses the first UnityEvent method name.")]
        public string actionDisplayName = "";
        public UnityEvent onPerformed; // hook your Remove method here

        void Reset()
        {
            iconImage = GetComponent<ControllerIconImage>();
            if (!label) label = GetComponentInChildren<TMP_Text>();
        }

        void Awake()
        {
            if (!iconImage) iconImage = GetComponent<ControllerIconImage>();
            UpdateUI(pressed: false); // initial
        }

        void Update()
        {
            // 1) Update icon (normal/pressed)
            bool isPressed = OVRInput.Get(button, controller);
            if (showPressedVariantWhileHeld) UpdateUI(isPressed);

            // 2) Update label text (kept lightweight)
            UpdateLabel();

            // 3) Invoke the mapped action
            if (useGetDownInvoke)
            {
                if (OVRInput.GetDown(button, controller)) SafeInvoke();
            }
            else
            {
                if (OVRInput.GetUp(button, controller)) SafeInvoke();
            }
        }

        void SafeInvoke()
        {
            try { onPerformed?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void UpdateUI(bool pressed)
        {
            var baseName = SpriteBaseNameFor(button, controller);
            if (string.IsNullOrEmpty(baseName)) return;

            // Your ControllerIconImage picks a sprite by name from Resources/ControllerInputIcon
            iconImage.selectedSpriteName = pressed ? (baseName + "-pressed") : baseName;
            iconImage.ApplySelected();
        }

        void UpdateLabel()
        {
            if (!label) return;

            string inputName = HumanNameFor(button, controller);
            string actName = !string.IsNullOrEmpty(actionDisplayName) ? actionDisplayName : EventMethodName();

            // fallback if no event wired
            if (string.IsNullOrEmpty(actName)) actName = "Action";

            label.text = $"Press {inputName} to {actName}";
        }

        string EventMethodName()
        {
            if (onPerformed == null) return "";
            int n = onPerformed.GetPersistentEventCount();
            if (n <= 0) return "";
            // Use first bound method name as a human-ish label
            string method = onPerformed.GetPersistentMethodName(0);
            if (string.IsNullOrEmpty(method)) return "";

            // Small prettifier: Remove suffix "Method" and split CamelCase
            if (method.EndsWith("Method")) method = method.Substring(0, method.Length - "Method".Length);
            return SplitCamel(method);
        }

        static string SplitCamel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        // === Mapping helpers ===

        public static string SpriteBaseNameFor(OVRInput.Button btn, OVRInput.Controller ctrl)
        {
            // A/B/X/Y
            if ((btn & OVRInput.Button.One) != 0)
                return (IsRight(ctrl) ? "button-a" : "button-x");
            if ((btn & OVRInput.Button.Two) != 0)
                return (IsRight(ctrl) ? "button-b" : "button-y");

            // Menu / Oculus
            if ((btn & OVRInput.Button.Start) != 0) return "button-menu";
            if ((btn & OVRInput.Button.Back) != 0) return "button-oculus";

            // Index triggers
            if ((btn & OVRInput.Button.PrimaryIndexTrigger) != 0) return "trigger-left";
            if ((btn & OVRInput.Button.SecondaryIndexTrigger) != 0) return "trigger-right";

            // Hand triggers (grips)
            if ((btn & OVRInput.Button.PrimaryHandTrigger) != 0) return "grip-left";
            if ((btn & OVRInput.Button.SecondaryHandTrigger) != 0) return "grip-right";

            // Thumbsticks (click)
            if ((btn & OVRInput.Button.PrimaryThumbstick) != 0) return "joystick-left";
            if ((btn & OVRInput.Button.SecondaryThumbstick) != 0) return "joystick-right";

            // Add more mappings as needed (dpad, etc.)
            return null;
        }

        public static string HumanNameFor(OVRInput.Button btn, OVRInput.Controller ctrl)
        {
            if ((btn & OVRInput.Button.One) != 0) return IsRight(ctrl) ? "A" : "X";
            if ((btn & OVRInput.Button.Two) != 0) return IsRight(ctrl) ? "B" : "Y";
            if ((btn & OVRInput.Button.Start) != 0) return "Menu";
            if ((btn & OVRInput.Button.Back) != 0) return "Oculus";
            if ((btn & OVRInput.Button.PrimaryIndexTrigger) != 0) return "Left Trigger";
            if ((btn & OVRInput.Button.SecondaryIndexTrigger) != 0) return "Right Trigger";
            if ((btn & OVRInput.Button.PrimaryHandTrigger) != 0) return "Left Grip";
            if ((btn & OVRInput.Button.SecondaryHandTrigger) != 0) return "Right Grip";
            if ((btn & OVRInput.Button.PrimaryThumbstick) != 0) return "Left Stick";
            if ((btn & OVRInput.Button.SecondaryThumbstick) != 0) return "Right Stick";
            return btn.ToString();
        }

        static bool IsRight(OVRInput.Controller ctrl) => (ctrl & OVRInput.Controller.RTouch) != 0;
    }
}
