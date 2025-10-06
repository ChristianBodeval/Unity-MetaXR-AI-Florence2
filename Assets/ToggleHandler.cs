using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using NaughtyAttributes;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
#endif

[ExecuteAlways]
public class ToggleHandler : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // Modes
    // ─────────────────────────────────────────────────────────────────────
    public enum InteractionMode
    {
        Toggle,
        Trigger
    }

    public ContentSizeFitter contentSizeFitter;

    [Header("Mode")]
    [SerializeField] private InteractionMode interactionMode = InteractionMode.Toggle;
    public InteractionMode Mode
    {
        get => interactionMode;
        set
        {
            if (interactionMode == value) return;
            interactionMode = value;
            ApplyAll();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // References
    // ─────────────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private Toggle toggle;
    public Toggle Toggle
    {
        get => toggle;
        set { toggle = value; ApplyAll(); }
    }

    [SerializeField] private TMP_Text label;
    public TMP_Text Label
    {
        get => label;
        set { label = value; ApplyAll(); }
    }

    [SerializeField] private Image iconImage;
    public Image IconImage
    {
        get => iconImage;
        set { iconImage = value; ApplyAll(); }
    }

    [SerializeField] private Image panelImage;
    public Image PanelImage
    {
        get => panelImage;
        set { panelImage = value; ApplyAll(); }
    }

    [Header("Group References (Optional)")]
    [Tooltip("Assign your 'Text' parent GameObject here (the container that holds Label + Subtitle).")]
    [SerializeField] private GameObject textGroup; // e.g., the "Text" object in your hierarchy
    public GameObject TextGroup
    {
        get => textGroup;
        set { textGroup = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Layout
    // ─────────────────────────────────────────────────────────────────────
    [Header("Layout (Optional)")]
    [Tooltip("Any LayoutGroup (Horizontal/Vertical/Grid). Padding uses (L,T,R,B).")]
    [SerializeField] private LayoutGroup layoutGroup;
    public LayoutGroup LayoutGroup
    {
        get => layoutGroup;
        set { layoutGroup = value; ApplyLayoutSettings(); }
    }

    [Tooltip("Padding: X=Left, Y=Top, Z=Right, W=Bottom")]
    [SerializeField] private Vector4 layoutPaddingLTBR = Vector4.zero;
    public Vector4 LayoutPaddingLTBR
    {
        get => layoutPaddingLTBR;
        set { layoutPaddingLTBR = value; ApplyLayoutSettings(); }
    }

    [Tooltip("Spacing for Horizontal/Vertical LayoutGroup. For Grid, applies to both axes.")]
    [SerializeField] private float layoutSpacing = 0f;
    public float LayoutSpacing
    {
        get => layoutSpacing;
        set { layoutSpacing = value; ApplyLayoutSettings(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Rounded Corners
    // ─────────────────────────────────────────────────────────────────────
    [Header("Rounded Corners (Optional)")]
    [SerializeField] private RoundedCornerUI roundedCorner;
    public RoundedCornerUI RoundedCorner
    {
        get => roundedCorner;
        set { roundedCorner = value; ApplyRoundedCorners(); }
    }

    [Tooltip("Sets RoundedCornerUI.GlobalBorder (clamped by element size).")]
    [Min(0f)]
    [SerializeField] private float roundedGlobalBorder = 8f;
    public float RoundedGlobalBorder
    {
        get => roundedGlobalBorder;
        set
        {
            float v = Mathf.Max(0f, value);
            if (Mathf.Approximately(roundedGlobalBorder, v)) return;
            roundedGlobalBorder = v;
            ApplyRoundedCorners();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Visibility
    // ─────────────────────────────────────────────────────────────────────
    public enum VisibleContent
    {
        TextOnly,
        IconOnly,
        Both,
        None
    }

    [Header("Visibility")]
    [SerializeField] private VisibleContent visibleContent = VisibleContent.Both;
    public VisibleContent ContentVisibility
    {
        get => visibleContent;
        set
        {
            if (visibleContent == value) return;
            visibleContent = value;
            ApplyVisuals(CurrentIsOnForVisuals());
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Texts
    // ─────────────────────────────────────────────────────────────────────
    [Header("Texts")]
    [SerializeField] private string textWhenOn = "On";
    public string TextWhenOn
    {
        get => textWhenOn;
        set
        {
            if (textWhenOn == value) return;
            textWhenOn = value;
            if (interactionMode == InteractionMode.Toggle && (toggle != null && toggle.isOn))
                ApplyVisuals(true);
        }
    }

    [SerializeField] private string textWhenOff = "Off";
    public string TextWhenOff
    {
        get => textWhenOff;
        set
        {
            if (textWhenOff == value) return;
            textWhenOff = value;
            ApplyVisuals(false);
        }
    }

    [Tooltip("If true, disables the label (or Text group) when the resolved text is empty.")]
    [SerializeField] private bool hideLabelWhenEmpty = true;
    public bool HideLabelWhenEmpty
    {
        get => hideLabelWhenEmpty;
        set
        {
            if (hideLabelWhenEmpty == value) return;
            hideLabelWhenEmpty = value;
            ApplyVisuals(CurrentIsOnForVisuals());
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Icons
    // ─────────────────────────────────────────────────────────────────────
    [Header("Icons (Sprites)")]
    [SerializeField] private Sprite iconWhenOn;
    public Sprite IconWhenOn
    {
        get => iconWhenOn;
        set { iconWhenOn = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [SerializeField] private Sprite iconWhenOff;
    public Sprite IconWhenOff
    {
        get => iconWhenOff;
        set { iconWhenOff = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [SerializeField] private GameObject selectBorder;
    public GameObject SelectBorder
    {
        get => selectBorder;
        set
        {
            selectBorder = value;
            if (selectBorder != null) selectBorder.SetActive(isSelected);
        }
    }

    [Header("Icon Colors (Tint)")]
    [SerializeField] private Color iconColorWhenOn = Color.white;
    public Color IconColorWhenOn
    {
        get => iconColorWhenOn;
        set { iconColorWhenOn = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [SerializeField] private Color iconColorWhenOff = new Color(1f, 1f, 1f, 0.8f);
    public Color IconColorWhenOff
    {
        get => iconColorWhenOff;
        set { iconColorWhenOff = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [Tooltip("Keep the current icon alpha and only change RGB when tinting.")]
    [SerializeField] private bool preserveIconAlpha = true;
    public bool PreserveIconAlpha
    {
        get => preserveIconAlpha;
        set { preserveIconAlpha = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Text Colors
    // ─────────────────────────────────────────────────────────────────────
    [Header("Text Colors")]
    [SerializeField] private Color textColorWhenOn = Color.white;
    public Color TextColorWhenOn
    {
        get => textColorWhenOn;
        set { textColorWhenOn = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [SerializeField] private Color textColorWhenOff = new Color(1f, 1f, 1f, 0.8f);
    public Color TextColorWhenOff
    {
        get => textColorWhenOff;
        set { textColorWhenOff = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [Tooltip("Keep the current text alpha and only change RGB when tinting.")]
    [SerializeField] private bool preserveTextAlpha = false;
    public bool PreserveTextAlpha
    {
        get => preserveTextAlpha;
        set { preserveTextAlpha = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [SerializeField] private bool isSelected = false;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            if (selectBorder != null) selectBorder.SetActive(isSelected);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Panel Colors
    // ─────────────────────────────────────────────────────────────────────
    [Header("Panel Colors")]
    [SerializeField] private Color colorWhenOn = Color.green;
    public Color ColorWhenOn
    {
        get => colorWhenOn;
        set { colorWhenOn = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    [SerializeField] private Color colorWhenOff = Color.red;
    public Color ColorWhenOff
    {
        get => colorWhenOff;
        set { colorWhenOff = value; ApplyVisuals(CurrentIsOnForVisuals()); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────────────────────────────
    [System.Serializable] public class BoolEvent : UnityEvent<bool> { }

    [Header("Events")]
    [SerializeField] private BoolEvent onToggleValueChanged;
    public BoolEvent OnToggleValueChanged => onToggleValueChanged;

    [SerializeField] private UnityEvent onToggledOn;
    public UnityEvent OnToggledOn => onToggledOn;

    [SerializeField] private UnityEvent onToggledOff;
    public UnityEvent OnToggledOff => onToggledOff;

    [SerializeField] private UnityEvent onTriggered; // Used in Trigger mode
    public UnityEvent OnTriggered => onTriggered;

    // Cache the last known state so Trigger mode can restore it
    private bool lastIsOn = false;

    // A convenient property to control state (Toggle mode).
    public bool Value
    {
        get => toggle ? toggle.isOn : lastIsOn;
        set
        {
            if (interactionMode != InteractionMode.Toggle)
            {
                // In Trigger mode we do not persist visual "on".
                ApplyVisuals(false);
                return;
            }

            lastIsOn = value;
            if (toggle) toggle.SetIsOnWithoutNotify(value);
            ApplyState(value);
        }
    }

    private void Reset()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        if (!label) label = GetComponentInChildren<TMP_Text>(true);

        if (!iconImage)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            if (imgs != null && imgs.Length > 0) iconImage = imgs[imgs.Length - 1];
        }

        if (!panelImage) panelImage = GetComponent<Image>();

        if (!textGroup && label) textGroup = label.transform.parent ? label.transform.parent.gameObject : null;
    }

    private void OnEnable()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        lastIsOn = toggle ? toggle.isOn : false;

        ApplyAll();

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
            toggle.onValueChanged.AddListener(OnToggleChanged);
        }

        contentSizeFitter = GetComponent<ContentSizeFitter>();
    }

    private void OnDisable()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
        if (!panelImage) panelImage = GetComponent<Image>();
        if (!iconImage)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            if (imgs != null && imgs.Length > 0) iconImage = imgs[imgs.Length - 1];
        }
        if (!textGroup && label) textGroup = label.transform.parent ? label.transform.parent.gameObject : null;

        lastIsOn = toggle ? toggle.isOn : false;

        ApplyAll();
        ForceUIRefresh();
    }
#endif

    public bool hasUpdated;

    private void LateUpdate()
    {
        ForceUIRefresh();
        if (!hasUpdated)
        {
        }
    }

    private void OnToggleChanged(bool newValue)
    {
        hasUpdated = false;

        if (interactionMode == InteractionMode.Toggle)
        {
            lastIsOn = newValue;
            ApplyState(newValue);
        }
        else // Trigger
        {
            if (Application.isPlaying)
            {
                onTriggered?.Invoke();
            }

            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(lastIsOn);
            }

            ApplyVisuals(false);
        }
    }

    /// <summary>
    /// Runtime state change: updates visuals and invokes events.
    /// </summary>
    public void ApplyState(bool isOn)
    {
        ApplyVisuals(isOn);
        if (Application.isPlaying && interactionMode == InteractionMode.Toggle)
        {
            FireUnityEvents(isOn);
        }
    }

    /// <summary>Applies visuals, layout, and rounded corners.</summary>
    private void ApplyAll()
    {
        bool current = toggle ? toggle.isOn : false;

        // In Trigger mode, visuals should not flip; we render with "Off" styling.
        bool visualsOn = interactionMode == InteractionMode.Toggle && current;
        ApplyVisuals(visualsOn);

        ApplyLayoutSettings();
        ApplyRoundedCorners();

        // Keep selection border synced
        if (selectBorder != null) selectBorder.SetActive(isSelected);
    }

    public void SetIsSelected(bool b)
    {
        IsSelected = b;
    }

    /// <summary>
    /// Editor-safe visual update (no side effects besides visuals).
    /// </summary>
    private void ApplyVisuals(bool isOn)
    {
        // If in Trigger mode, force to "off" look
        if (interactionMode == InteractionMode.Trigger) isOn = false;

        // Determine content mode
        bool showTextByMode = visibleContent == VisibleContent.TextOnly || visibleContent == VisibleContent.Both;
        bool showIconByMode = visibleContent == VisibleContent.IconOnly || visibleContent == VisibleContent.Both;
        bool noneMode = visibleContent == VisibleContent.None;

        // TEXT
        if (label != null)
        {
            string nextText = isOn ? textWhenOn : textWhenOff;
            bool shouldShowText = showTextByMode && !(hideLabelWhenEmpty && string.IsNullOrEmpty(nextText));
            if (noneMode) shouldShowText = false;

            if (textGroup)
            {
                if (textGroup.activeSelf != shouldShowText)
                    textGroup.SetActive(shouldShowText);
            }
            else
            {
                if (label.gameObject.activeSelf != shouldShowText)
                    label.gameObject.SetActive(shouldShowText);
            }

            if (shouldShowText)
            {
                if (label.text != nextText) label.text = nextText;

                var targetTextColor = isOn ? textColorWhenOn : textColorWhenOff;
                if (preserveTextAlpha) targetTextColor.a = label.color.a;
                if (label.color != targetTextColor) label.color = targetTextColor;
            }
        }

        // ICON
        if (iconImage)
        {
            bool shouldShowIcon = showIconByMode && !noneMode;
            if (iconImage.gameObject.activeSelf != shouldShowIcon)
                iconImage.gameObject.SetActive(shouldShowIcon);

            if (shouldShowIcon)
            {
                var nextSprite = isOn ? iconWhenOn : iconWhenOff;
                if (iconImage.sprite != nextSprite) iconImage.sprite = nextSprite;

                var targetTint = isOn ? iconColorWhenOn : iconColorWhenOff;
                if (preserveIconAlpha) targetTint.a = iconImage.color.a;
                if (iconImage.color != targetTint) iconImage.color = targetTint;
            }
        }

        // PANEL
        if (panelImage)
        {
            var nextColor = isOn ? colorWhenOn : colorWhenOff;
            if (panelImage.color != nextColor) panelImage.color = nextColor;
        }
    }

    private void ApplyLayoutSettings()
    {
        if (!layoutGroup) return;

        var pad = new RectOffset(
            Mathf.Max(0, Mathf.RoundToInt(layoutPaddingLTBR.x)),
            Mathf.Max(0, Mathf.RoundToInt(layoutPaddingLTBR.z)),
            Mathf.Max(0, Mathf.RoundToInt(layoutPaddingLTBR.y)),
            Mathf.Max(0, Mathf.RoundToInt(layoutPaddingLTBR.w))
        );

        if (layoutGroup.padding == null)
            layoutGroup.padding = new RectOffset();

        layoutGroup.padding.left = pad.left;
        layoutGroup.padding.right = pad.right;
        layoutGroup.padding.top = pad.top;
        layoutGroup.padding.bottom = pad.bottom;

        if (layoutGroup is HorizontalLayoutGroup hlg)
        {
            if (!Mathf.Approximately(hlg.spacing, layoutSpacing)) hlg.spacing = layoutSpacing;
        }
        else if (layoutGroup is VerticalLayoutGroup vlg)
        {
            if (!Mathf.Approximately(vlg.spacing, layoutSpacing)) vlg.spacing = layoutSpacing;
        }
        else if (layoutGroup is GridLayoutGroup glg)
        {
            var sp = glg.spacing;
            if (!Mathf.Approximately(sp.x, layoutSpacing) || !Mathf.Approximately(sp.y, layoutSpacing))
                glg.spacing = new Vector2(layoutSpacing, layoutSpacing);
        }

#if UNITY_EDITOR
        LayoutRebuilder.MarkLayoutForRebuild(layoutGroup.transform as RectTransform);
#endif
    }

    private void ApplyRoundedCorners()
    {
        if (!roundedCorner) return;
        roundedCorner.GlobalBorder = roundedGlobalBorder;

#if UNITY_EDITOR
        roundedCorner.ForceUIUpdate2();
#endif
    }

    private void FireUnityEvents(bool isOn)
    {
        onToggleValueChanged?.Invoke(isOn);
        if (isOn) onToggledOn?.Invoke();
        else onToggledOff?.Invoke();
    }

    public void ForceUIRefresh()
    {
        if (label)
        {
            label.ForceMeshUpdate();
            label.SetVerticesDirty();
            var rtText = label.rectTransform;
            if (rtText) LayoutRebuilder.MarkLayoutForRebuild(rtText);
        }
        if (iconImage)
        {
            iconImage.SetAllDirty();
            var rtIcon = iconImage.rectTransform;
            if (rtIcon) LayoutRebuilder.MarkLayoutForRebuild(rtIcon);
        }
        if (panelImage)
        {
            panelImage.SetAllDirty();
            var rtPanel = panelImage.rectTransform;
            if (rtPanel) LayoutRebuilder.MarkLayoutForRebuild(rtPanel);
        }
        if (layoutGroup)
        {
            var rtLayout = layoutGroup.transform as RectTransform;
            if (rtLayout) LayoutRebuilder.MarkLayoutForRebuild(rtLayout);
        }

#if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this) EditorUtility.SetDirty(this);
            if (label) EditorUtility.SetDirty(label);
            if (iconImage) EditorUtility.SetDirty(iconImage);
            if (panelImage) EditorUtility.SetDirty(panelImage);
            if (layoutGroup) EditorUtility.SetDirty(layoutGroup);
            if (roundedCorner) EditorUtility.SetDirty(roundedCorner);
            if (textGroup) EditorUtility.SetDirty(textGroup);
        };
#endif
        EnableContentSizeFitters();
        hasUpdated = true;
    }

#if UNITY_EDITOR
    // Editor-only helpers for the custom inspector preview buttons
    public void EditorPreviewSetState(bool isOn)
    {
        if (!toggle) return;
        toggle.SetIsOnWithoutNotify(isOn);
        lastIsOn = isOn;
        ApplyAll();
        ForceUIRefresh();
    }

    public void EditorPreviewTriggerOnce()
    {
        // mimic a trigger without changing state, no runtime events in edit mode
        ApplyVisuals(false);
        ForceUIRefresh();
    }
#endif

    public void EnableContentSizeFitters()
    {
        if (contentSizeFitter) contentSizeFitter.enabled = true;
    }

    // ---------- Public setters for scripting (alt to properties) ----------
    public void SetLayoutPadding(Vector4 leftTopRightBottom) => LayoutPaddingLTBR = leftTopRightBottom;
    public void SetLayoutSpacing(float spacing) => LayoutSpacing = spacing;
    public void SetVisibleContent(VisibleContent content) => ContentVisibility = content;
    public void SetRoundedGlobal(float value) => RoundedGlobalBorder = value;
    public void SetTextGroup(GameObject go) => TextGroup = go;
    public void SetMode(InteractionMode mode) => Mode = mode;

    public void Trigger() // optional manual trigger
    {
        if (interactionMode != InteractionMode.Trigger) return;
        if (Application.isPlaying) onTriggered?.Invoke();
        ApplyVisuals(false);
        if (toggle) toggle.SetIsOnWithoutNotify(lastIsOn);
    }

    private bool CurrentIsOnForVisuals()
    {
        // Visuals follow 'on' only in Toggle mode; otherwise off-look.
        return interactionMode == InteractionMode.Toggle && (toggle ? toggle.isOn : lastIsOn);
    }
}

#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// Custom Editor with Preview/Test controls
// ─────────────────────────────────────────────────────────────────────────────
[CustomEditor(typeof(ToggleHandler))]
public class ToggleHandlerEditor : Editor
{
    private SerializedProperty pMode;

    private SerializedProperty pToggle, pLabel, pIconImage, pPanelImage, pTextGroup;
    private SerializedProperty pLayoutGroup, pLayoutPadding, pLayoutSpacing;
    private SerializedProperty pRoundedCorner, pRoundedGlobalBorder;

    private SerializedProperty selectionBorder;

    private SerializedProperty pIsSelectedBool;

    private SerializedProperty pVisibleContent;

    private SerializedProperty pTextWhenOn, pTextWhenOff, pHideLabelWhenEmpty;

    private SerializedProperty pIconWhenOn, pIconWhenOff;
    private SerializedProperty pIconColorWhenOn, pIconColorWhenOff, pPreserveIconAlpha;

    private SerializedProperty pTextColorWhenOn, pTextColorWhenOff, pPreserveTextAlpha;

    private SerializedProperty pColorWhenOn, pColorWhenOff;

    private SerializedProperty pOnToggleValueChanged, pOnToggledOn, pOnToggledOff, pOnTriggered;

    private void OnEnable()
    {
        selectionBorder = serializedObject.FindProperty("selectBorder");

        pMode = serializedObject.FindProperty("interactionMode");

        pToggle = serializedObject.FindProperty("toggle");
        pLabel = serializedObject.FindProperty("label");
        pIconImage = serializedObject.FindProperty("iconImage");
        pPanelImage = serializedObject.FindProperty("panelImage");
        pTextGroup = serializedObject.FindProperty("textGroup");

        pIsSelectedBool = serializedObject.FindProperty("isSelected");

        pLayoutGroup = serializedObject.FindProperty("layoutGroup");
        pLayoutPadding = serializedObject.FindProperty("layoutPaddingLTBR");
        pLayoutSpacing = serializedObject.FindProperty("layoutSpacing");

        pRoundedCorner = serializedObject.FindProperty("roundedCorner");
        pRoundedGlobalBorder = serializedObject.FindProperty("roundedGlobalBorder");

        pVisibleContent = serializedObject.FindProperty("visibleContent");

        pTextWhenOn = serializedObject.FindProperty("textWhenOn");
        pTextWhenOff = serializedObject.FindProperty("textWhenOff");
        pHideLabelWhenEmpty = serializedObject.FindProperty("hideLabelWhenEmpty");

        pIconWhenOn = serializedObject.FindProperty("iconWhenOn");
        pIconWhenOff = serializedObject.FindProperty("iconWhenOff");
        pIconColorWhenOn = serializedObject.FindProperty("iconColorWhenOn");
        pIconColorWhenOff = serializedObject.FindProperty("iconColorWhenOff");
        pPreserveIconAlpha = serializedObject.FindProperty("preserveIconAlpha");

        pTextColorWhenOn = serializedObject.FindProperty("textColorWhenOn");
        pTextColorWhenOff = serializedObject.FindProperty("textColorWhenOff");
        pPreserveTextAlpha = serializedObject.FindProperty("preserveTextAlpha");

        pColorWhenOn = serializedObject.FindProperty("colorWhenOn");
        pColorWhenOff = serializedObject.FindProperty("colorWhenOff");

        pOnToggleValueChanged = serializedObject.FindProperty("onToggleValueChanged");
        pOnToggledOn = serializedObject.FindProperty("onToggledOn");
        pOnToggledOff = serializedObject.FindProperty("onToggledOff");
        pOnTriggered = serializedObject.FindProperty("onTriggered");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pMode);

        var isTrigger = (ToggleHandler.InteractionMode)pMode.enumValueIndex == ToggleHandler.InteractionMode.Trigger;
        if (isTrigger)
        {
            EditorGUILayout.HelpBox(
                "Trigger Mode: clicks invoke OnTriggered and DO NOT change state/visuals. 'On' visuals are ignored and hidden below.",
                MessageType.Info);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(selectionBorder);
        EditorGUILayout.PropertyField(pIsSelectedBool);

        EditorGUILayout.PropertyField(pToggle);
        EditorGUILayout.PropertyField(pLabel);
        EditorGUILayout.PropertyField(pIconImage);
        EditorGUILayout.PropertyField(pPanelImage);
        EditorGUILayout.PropertyField(pTextGroup);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Layout (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pLayoutGroup);
        EditorGUILayout.PropertyField(pLayoutPadding, new GUIContent("Padding (L,T,R,B)"));
        EditorGUILayout.PropertyField(pLayoutSpacing);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Rounded Corners (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pRoundedCorner);
        EditorGUILayout.PropertyField(pRoundedGlobalBorder);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Visibility", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVisibleContent);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Texts", EditorStyles.boldLabel);
        if (!isTrigger)
        {
            EditorGUILayout.PropertyField(pTextWhenOn);
        }
        EditorGUILayout.PropertyField(pTextWhenOff);
        EditorGUILayout.PropertyField(pHideLabelWhenEmpty);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Icons", EditorStyles.boldLabel);
        if (!isTrigger)
        {
            EditorGUILayout.PropertyField(pIconWhenOn);
            EditorGUILayout.PropertyField(pIconColorWhenOn);
        }
        EditorGUILayout.PropertyField(pIconWhenOff);
        EditorGUILayout.PropertyField(pIconColorWhenOff);
        EditorGUILayout.PropertyField(pPreserveIconAlpha);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Text Colors", EditorStyles.boldLabel);
        if (!isTrigger)
        {
            EditorGUILayout.PropertyField(pTextColorWhenOn);
        }
        EditorGUILayout.PropertyField(pTextColorWhenOff);
        EditorGUILayout.PropertyField(pPreserveTextAlpha);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Panel Colors", EditorStyles.boldLabel);
        if (!isTrigger)
        {
            EditorGUILayout.PropertyField(pColorWhenOn);
        }
        EditorGUILayout.PropertyField(pColorWhenOff);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        if (isTrigger)
        {
            EditorGUILayout.PropertyField(pOnTriggered);
        }
        else
        {
            EditorGUILayout.PropertyField(pOnToggleValueChanged);
            EditorGUILayout.PropertyField(pOnToggledOn);
            EditorGUILayout.PropertyField(pOnToggledOff);
        }

        // ──────────────────────────────────────────────────────────────
        // Editor Preview / Test Controls
        // ──────────────────────────────────────────────────────────────
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Test (Editor Preview)", EditorStyles.boldLabel);

            if (!isTrigger)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set ON"))
                        ForEachHandler(h =>
                        {
                            if (h == null) return;
                            Undo.RecordObject(h, "Preview Set ON");
                            if (h.TryGetComponent<Toggle>(out var t)) Undo.RecordObject(t, "Preview Set ON");
                            h.EditorPreviewSetState(true);
                            MarkDirty(h);
                        });

                    if (GUILayout.Button("Set OFF"))
                        ForEachHandler(h =>
                        {
                            if (h == null) return;
                            Undo.RecordObject(h, "Preview Set OFF");
                            if (h.TryGetComponent<Toggle>(out var t)) Undo.RecordObject(t, "Preview Set OFF");
                            h.EditorPreviewSetState(false);
                            MarkDirty(h);
                        });
                }
            }
            else
            {
                if (GUILayout.Button("Trigger Once"))
                {
                    ForEachHandler(h =>
                    {
                        if (h == null) return;
                        Undo.RecordObject(h, "Preview Trigger Once");
                        h.EditorPreviewTriggerOnce();
                        MarkDirty(h);
                    });
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ForEachHandler(System.Action<ToggleHandler> action)
    {
        foreach (var obj in targets)
        {
            var h = obj as ToggleHandler;
            if (h == null) continue;
            action(h);
        }
    }

    private void MarkDirty(ToggleHandler h)
    {
        EditorUtility.SetDirty(h);
        if (h && h.gameObject && h.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
#endif
