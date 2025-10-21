using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;           // for optional OnTap reflection
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WorldSpaceLampUIUGUI : MonoBehaviour
{
    // ===== World-space Canvas & Theme =====
    [Header("World-Space Canvas")]
    public int canvasWidthPx = 800;
    public int canvasHeightPx = 760;
    public float canvasScale = 0.0022f;
    public Camera worldCamera;

    [Header("Theme")]
    public Color pageBg = new Color32(0xF7, 0xF8, 0xFA, 255);
    public Color themeCard = new Color32(0xA6, 0x4D, 0x4D, 255);         // maroon
    public Color cardText = Color.white;
    public Color focusRing = new Color32(0x22, 0xD3, 0xEE, 255);        // cyan
    public Color chipShadow = new Color(0, 0, 0, 0.15f);

    // ===== State =====
    public enum LampState { ON, OFF, RAVE_PARTY }
    public enum FocusedAction { name, power, rave }

    [Header("State")]
    public LampState lampState = LampState.ON;
    public FocusedAction focused = FocusedAction.name;

    [Header("Carousel")]
    public float carouselAnimTime = 0.25f;
    public int initialColorIndex = 1;

    [Header("Voice Sim")]
    public float listenTime = 2.0f;
    [Range(0, 1)] public float listenSuccessChance = 0.7f;

    [Header("Build / Persist")]
    public bool buildInEditMode = true;

    // ===== Action / Listener UI =====
    [Header("Action / Listener UI")]
    public TextMeshProUGUI actionText;
    public GameObject ListenerGO;

    [Header("Action Text Values")]
    public string raveActionText = "Ravin'";
    public string offActionText = "Tap to turn on";

    // ===== Exposed triggers & events =====
    [Header("Input Triggers (call these from UI)")]
    public UnityEvent onUp;
    public UnityEvent onDown;
    public UnityEvent onLeft;
    public UnityEvent onRight;
    public UnityEvent onTap;

    [Header("Output Events")]
    public UnityEvent onFocusChanged;
    public UnityEvent onCarouselChanged;   // fired when center color changes
    public UnityEvent onStateChanged;
    [Serializable] public class IntEvent : UnityEvent<int> { }   // payload: dir (+1 or -1)
    public IntEvent onSlideStarted;
    public IntEvent onSlideCompleted;

    // ===== Color data =====
    [Serializable] public struct ColorEntry { public string name; public string hex; }
    public List<ColorEntry> defaultColors = new()
    {
        new ColorEntry { name="Crimson",    hex="#A64D4D"},
        new ColorEntry { name="Lime",       hex="#8BC34A"},
        new ColorEntry { name="Royal Blue", hex="#5C7CFA"},
        new ColorEntry { name="Sunshine",   hex="#F4E879"},
        new ColorEntry { name="Coral",      hex="#FF6B6B"},
        new ColorEntry { name="Mint",       hex="#51CF66"},
        new ColorEntry { name="Lavender",   hex="#B197FC"},
        new ColorEntry { name="Peach",      hex="#FFB38A"},
    };

    // ===== XR Input hookup =====
    [Header("XR Input Hookup")]
    public bool autoSubscribeXRInput = true;       // auto-hook XRInputManager events
    public bool trySubscribeTapEvent = true;       // attempt optional OnTap subscription if present

    const string ROOT_NAME = "WS_LampUI_Root";

    Canvas _canvas;
    RectTransform _root;
    Image _rootBg;

    // Top lamp
    RectTransform _lampGroup;
    Image _lampDisc;
    TextMeshProUGUI _lampBulbSymbol;

    // Carousel (5 chips)
    RectTransform _carouselRoot;
    Button _leftBtn, _rightBtn;

    RectTransform _chipFarLeft, _chipLeft, _chipCenter, _chipRight, _chipFarRight;
    Image _chipFarLeftImg, _chipLeftImg, _chipCenterImg, _chipRightImg, _chipFarRightImg, _chipFarRightShadow, _chipFarLeftShadow;

    TextMeshProUGUI _colorName, _colorHex;

    // Action cards
    ActionCard _cardPower;
    ActionCard _cardName;
    ActionCard _cardRave;

    // Collapsed-state cards
    ActionCard _cardOffCollapsed;   // OFF
    ActionCard _cardRaveCollapsed;  // RAVE_PARTY

    // Toast
    RectTransform _toastRoot;
    TextMeshProUGUI _toastText;
    Coroutine _toastCo;

    // Data
    int _index;
    bool _built;

    // --- Carousel animation state ---
    Coroutine _carouselAnim;
    bool _carouselBusy;

    // Listening flag (for choose-a-color mode)
    bool _isListening;

    // Five-slot layout constants
    const float X_FAR_LEFT = -240f;
    const float X_LEFT = -120f;
    const float X_CENTER = 0f;
    const float X_RIGHT = 120f;
    const float X_FAR_RIGHT = 240f;

    const float SCALE_FAR = 0.5f;
    const float SCALE_NEAR = 0.6f;
    const float SCALE_CENTER = 1.0f;

    // XRInputManager ref & reflection cache for optional OnTap
    XRInputManager _xri;
    FieldInfo _xriOnTapField; // optional
    UnityEvent _xriOnTapCached; // optional

    // ========================= Unity lifecycle =========================
    void OnEnable()
    {
        if (!Application.isPlaying && !buildInEditMode) return;
        BindOrBuild();
        EnsureFiveChips();
        ApplyAll(firstTime: true);

        if (Application.isPlaying && autoSubscribeXRInput)
            TryHookXRInput();
    }

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        BindOrBuild();
        EnsureFiveChips();
        ApplyAll(firstTime: true);
    }

    void OnValidate()
    {
        if (!_built) return;
        EnsureFiveChips();
        ApplyAll(firstTime: false);
    }

    void OnDisable()
    {
        UnhookXRInput();
    }

    void OnDestroy()
    {
        UnhookXRInput();
    }

    void Update()
    {
        if (!Application.isPlaying || !_built) return;

        // Keep trying to hook if manager appears later
        if (autoSubscribeXRInput && _xri == null)
            TryHookXRInput();

        // Keyboard controls for quick testing
        if (Input.GetKeyDown(KeyCode.UpArrow)) PressUp();
        if (Input.GetKeyDown(KeyCode.DownArrow)) PressDown();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) PressLeft();
        if (Input.GetKeyDown(KeyCode.RightArrow)) PressRight();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) PressTap();
    }

    // ========================= XR Input subscription =========================
    [ContextMenu("Rehook XR Input Now")]
    void TryHookXRInput()
    {
        if (_xri != null) return;

        _xri = XRInputManager.Instance != null ? XRInputManager.Instance : FindObjectOfType<XRInputManager>();
        if (_xri == null) return;

        // Subscribe to directional events
        _xri.OnUp.AddListener(PressUp);
        _xri.OnDown.AddListener(PressDown);
        _xri.OnLeft.AddListener(PressLeft);
        _xri.OnRight.AddListener(PressRight);

        // Optionally subscribe to tap if XRInputManager exposes a public UnityEvent OnTap
        if (trySubscribeTapEvent)
        {
            _xriOnTapField = typeof(XRInputManager).GetField("OnTap", BindingFlags.Public | BindingFlags.Instance);
            if (_xriOnTapField != null)
            {
                _xriOnTapCached = _xriOnTapField.GetValue(_xri) as UnityEvent;
                if (_xriOnTapCached != null)
                    _xriOnTapCached.AddListener(PressTap);
            }
        }
    }

    void UnhookXRInput()
    {
        if (_xri == null) return;

        _xri.OnUp.RemoveListener(PressUp);
        _xri.OnDown.RemoveListener(PressDown);
        _xri.OnLeft.RemoveListener(PressLeft);
        _xri.OnRight.RemoveListener(PressRight);

        if (_xriOnTapCached != null)
        {
            _xriOnTapCached.RemoveListener(PressTap);
            _xriOnTapCached = null;
        }

        _xriOnTapField = null;
        _xri = null;
    }

    // ========================= Public triggers (callable from UI/other scripts) =========================
    public void PressUp() { onUp?.Invoke(); FocusUp(); }
    public void PressDown() { onDown?.Invoke(); FocusDown(); }
    public void PressLeft() { onLeft?.Invoke(); ColorLeft(); }
    public void PressRight() { onRight?.Invoke(); ColorRight(); }
    public void PressTap() { onTap?.Invoke(); Tap(); }

    // ========================= Public gestures (core) =========================
    public void FocusUp()
    {
        if (lampState != LampState.ON) return;
        focused = focused switch
        {
            FocusedAction.name => FocusedAction.rave,
            FocusedAction.power => FocusedAction.name,
            _ => FocusedAction.power,
        };
        ApplyFocusVisuals();
        UpdateActionTextUI();
        onFocusChanged?.Invoke();
    }

    public void FocusDown()
    {
        if (lampState != LampState.ON) return;
        focused = focused switch
        {
            FocusedAction.name => FocusedAction.power,
            FocusedAction.power => FocusedAction.rave,
            _ => FocusedAction.name,
        };
        ApplyFocusVisuals();
        UpdateActionTextUI();
        onFocusChanged?.Invoke();
    }

    public void ColorLeft()
    {
        if (lampState != LampState.ON || _carouselBusy) return;
        int nextIndex = (_index - 1 + defaultColors.Count) % defaultColors.Count;
        if (_carouselAnim != null) StopCoroutine(_carouselAnim);
        onSlideStarted?.Invoke(-1);
        _carouselAnim = StartCoroutine(CoCarouselSlide(dir: -1, nextIndex: nextIndex));
    }

    public void ColorRight()
    {
        if (lampState != LampState.ON || _carouselBusy) return;
        int nextIndex = (_index + 1) % defaultColors.Count;
        if (_carouselAnim != null) StopCoroutine(_carouselAnim);
        onSlideStarted?.Invoke(+1);
        _carouselAnim = StartCoroutine(CoCarouselSlide(dir: +1, nextIndex: nextIndex));
    }

    public void Tap()
    {
        switch (lampState)
        {
            case LampState.OFF:
                lampState = LampState.ON;
                focused = FocusedAction.name;
                _isListening = false;
                ShowToast("Tap to turn on");
                ApplyAll(false);
                break;

            case LampState.RAVE_PARTY:
                lampState = LampState.ON;
                _isListening = false;
                ApplyAll(false);
                break;

            case LampState.ON:
                if (focused == FocusedAction.power)
                {
                    lampState = LampState.OFF;
                    _isListening = false;
                    ApplyAll(false);
                }
                else if (focused == FocusedAction.rave)
                {
                    lampState = LampState.RAVE_PARTY;
                    _isListening = false;
                    ApplyAll(false);
                }
                else // FocusedAction.name → listening
                {
                    if (_carouselAnim != null) StopCoroutine(_carouselAnim);
                    StartCoroutine(CoNameColor());
                }
                break;
        }
        onStateChanged?.Invoke();
    }

    // ========================= Build / Bind =========================
    void BindOrBuild()
    {
        if (_built) return;

        var root = transform.Find(ROOT_NAME);
        if (root != null)
        {
            CacheRefsFrom(root);
            _built = true;
            return;
        }

        // Build (kept from your version)
        var canvasGO = new GameObject(ROOT_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = worldCamera;
        _canvas.sortingOrder = 0;

        _root = canvasGO.GetComponent<RectTransform>();
        _root.sizeDelta = new Vector2(canvasWidthPx, canvasHeightPx);
        _canvas.transform.localScale = Vector3.one * canvasScale;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100;

        // Page background
        _rootBg = CreateImage("PageBG", _root, CreateRoundedSprite(canvasWidthPx, canvasHeightPx, 24), pageBg, Image.Type.Sliced);
        _rootBg.rectTransform.anchorMin = Vector2.zero;
        _rootBg.rectTransform.anchorMax = Vector2.one;
        _rootBg.rectTransform.offsetMin = Vector2.zero;
        _rootBg.rectTransform.offsetMax = Vector2.zero;

        // ===== Top Lamp =====
        _lampGroup = CreateRT("LampGroup", _root);
        _lampGroup.sizeDelta = new Vector2(120, 120);
        _lampGroup.anchoredPosition = new Vector2(0, canvasHeightPx * 0.5f - 140);

        _lampDisc = CreateImage("LampDisc", _lampGroup, CreateDiscSprite(96), Color.white, Image.Type.Simple);
        _lampDisc.rectTransform.sizeDelta = new Vector2(96, 96);

        _lampBulbSymbol = CreateTMP("BulbGlyph", _lampGroup, 64, new Color(0.33f, 0.2f, 0.2f), TextAlignmentOptions.Center);
        _lampBulbSymbol.text = "💡";
        _lampBulbSymbol.rectTransform.sizeDelta = new Vector2(96, 96);

        // ===== Carousel root =====
        _carouselRoot = CreateRT("Carousel", _root);
        _carouselRoot.sizeDelta = new Vector2(canvasWidthPx - 120, 180);
        _carouselRoot.anchoredPosition = new Vector2(0, 150);

        _leftBtn = CreateArrowButton("LeftBtn", _carouselRoot, "<");
        _leftBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-(_carouselRoot.sizeDelta.x * 0.5f - 30), 0);
        _leftBtn.onClick.AddListener(PressLeft);

        _rightBtn = CreateArrowButton("RightBtn", _carouselRoot, ">");
        _rightBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2((_carouselRoot.sizeDelta.x * 0.5f - 30), 0);
        _rightBtn.onClick.AddListener(PressRight);

        // Color name/hex
        _colorName = CreateTMP("ColorName", _root, 22, new Color(0.12f, 0.12f, 0.12f), TextAlignmentOptions.Center);
        _colorName.rectTransform.anchoredPosition = new Vector2(0, 55);
        _colorHex = CreateTMP("ColorHex", _root, 16, new Color(0.4f, 0.4f, 0.45f), TextAlignmentOptions.Center);
        _colorHex.rectTransform.anchoredPosition = new Vector2(0, 30);

        // ===== Action list (ON state) =====
        var listRoot = CreateRT("ActionList", _root);
        listRoot.sizeDelta = new Vector2(canvasWidthPx - 120, 260);
        listRoot.anchoredPosition = new Vector2(0, -120);

        _cardPower = BuildActionCard(listRoot, "Tap to turn off", "⏻");
        _cardPower.root.GetComponent<Button>().onClick.AddListener(() => { focused = FocusedAction.power; PressTap(); });

        _cardName = BuildActionCard(listRoot, "Name a color", "🎤");
        _cardName.root.GetComponent<Button>().onClick.AddListener(() => { focused = FocusedAction.name; PressTap(); });

        _cardRave = BuildActionCard(listRoot, "Rave Party", "✨");
        _cardRave.root.GetComponent<Button>().onClick.AddListener(() => { focused = FocusedAction.rave; PressTap(); });

        LayoutActionList(listRoot);

        // ===== Collapsed cards =====
        _cardOffCollapsed = BuildSingleCard(_root, "Lamp is off • Tap to turn on", "⏻");
        _cardOffCollapsed.rt.anchoredPosition = new Vector2(0, -100);
        _cardOffCollapsed.root.GetComponent<Button>().onClick.AddListener(() => { lampState = LampState.ON; focused = FocusedAction.name; ApplyAll(false); });

        _cardRaveCollapsed = BuildSingleCard(_root, "Rave Party active • Tap to stop", "✨");
        _cardRaveCollapsed.rt.anchoredPosition = new Vector2(0, -100);
        _cardRaveCollapsed.root.GetComponent<Button>().onClick.AddListener(() => { lampState = LampState.ON; ApplyAll(false); });

        // ===== Toast =====
        _toastRoot = CreateRT("Toast", _root);
        _toastRoot.sizeDelta = new Vector2(canvasWidthPx, 40);
        _toastRoot.anchoredPosition = new Vector2(0, -canvasHeightPx * 0.5f + 24);
        _toastText = CreateTMP("ToastText", _toastRoot, 16, new Color(0.2f, 0.2f, 0.25f), TextAlignmentOptions.Center);
        _toastText.text = "";
        _toastText.enableWordWrapping = false;

        _index = Mathf.Clamp(initialColorIndex, 0, Mathf.Max(1, defaultColors.Count) - 1);
        _built = true;
    }

    void CacheRefsFrom(Transform root)
    {
        _root = root.GetComponent<RectTransform>();
        _canvas = root.GetComponent<Canvas>();
        _rootBg = root.Find("PageBG")?.GetComponent<Image>();

        _lampGroup = root.Find("LampGroup") as RectTransform;
        _lampDisc = root.Find("LampGroup/LampDisc")?.GetComponent<Image>();
        _lampBulbSymbol = root.Find("LampGroup/BulbGlyph")?.GetComponent<TextMeshProUGUI>();

        _carouselRoot = root.Find("Carousel") as RectTransform;
        _leftBtn = root.Find("Carousel/LeftBtn")?.GetComponent<Button>();
        _rightBtn = root.Find("Carousel/RightBtn")?.GetComponent<Button>();

        _chipFarLeft = root.Find("Carousel/ChipFarLeft") as RectTransform;
        _chipLeft = root.Find("Carousel/ChipLeft") as RectTransform;
        _chipCenter = root.Find("Carousel/ChipCenter") as RectTransform;
        _chipRight = root.Find("Carousel/ChipRight") as RectTransform;
        _chipFarRight = root.Find("Carousel/ChipFarRight") as RectTransform;

        _chipFarLeftImg = root.Find("Carousel/ChipFarLeft/Img")?.GetComponent<Image>();
        _chipLeftImg = root.Find("Carousel/ChipLeft/Img")?.GetComponent<Image>();
        _chipCenterImg = root.Find("Carousel/ChipCenter/Img")?.GetComponent<Image>();
        _chipRightImg = root.Find("Carousel/ChipRight/Img")?.GetComponent<Image>();
        _chipFarRightImg = root.Find("Carousel/ChipFarRight/Img")?.GetComponent<Image>();
        _chipFarRightShadow = root.Find("Carousel/ChipFarRight/Img/Shadow")?.GetComponent<Image>();
        _chipFarLeftShadow = root.Find("Carousel/ChipFarLeft/Img/Shadow")?.GetComponent<Image>();

        _colorName = root.Find("ColorName")?.GetComponent<TextMeshProUGUI>();
        _colorHex = root.Find("ColorHex")?.GetComponent<TextMeshProUGUI>();

        var listRoot = root.Find("ActionList") as RectTransform;
        if (listRoot != null)
        {
            _cardPower = BindActionCard(listRoot, 0);
            _cardName = BindActionCard(listRoot, 1);
            _cardRave = BindActionCard(listRoot, 2);
        }

        _toastRoot = root.Find("Toast") as RectTransform;
        _toastText = root.Find("Toast/ToastText")?.GetComponent<TextMeshProUGUI>();
    }

    // ======== Ensure/Upgrade to 5-chip layout ========
    void EnsureFiveChips()
    {
        if (_carouselRoot == null) return;

        EnsureChip(ref _chipFarLeft, ref _chipFarLeftImg, "ChipFarLeft", 100);
        EnsureChip(ref _chipLeft, ref _chipLeftImg, "ChipLeft", 100);
        EnsureChip(ref _chipCenter, ref _chipCenterImg, "ChipCenter", 140);
        EnsureChip(ref _chipRight, ref _chipRightImg, "ChipRight", 100);
        EnsureChip(ref _chipFarRight, ref _chipFarRightImg, "ChipFarRight", 100);
    }

    void EnsureChip(ref RectTransform rt, ref Image img, string name, int diameter)
    {
        if (rt == null)
        {
            rt = CreateRT(name, _carouselRoot);
            rt.sizeDelta = new Vector2(diameter, diameter);
        }
        if (img == null)
        {
            img = CreateImage("Img", rt, CreateDiscSprite(diameter), Color.gray, Image.Type.Simple);
            SetShadow(img);
        }
    }

    // ========================= Apply Visuals =========================
    void ApplyAll(bool firstTime)
    {
        if (!_built) return;

        if (_canvas != null && _canvas.worldCamera == null && worldCamera != null)
            _canvas.worldCamera = worldCamera;

        if (_rootBg) _rootBg.color = pageBg;

        EnsureFiveChips();
        ApplyLampColor();
        ApplyCarouselVisuals();
        ApplyVisibleSections();
        ApplyFocusVisuals();
        UpdateActionTextUI();
    }

    void ApplyVisibleSections()
    {
        if (!_built) return;

        bool on = lampState == LampState.ON;
        bool off = lampState == LampState.OFF;
        bool rave = lampState == LampState.RAVE_PARTY;

        SetActiveRT(_carouselRoot, on);
        SetActiveRT(_colorName?.rectTransform, on);
        SetActiveRT(_colorHex?.rectTransform, on);
        SetCardActive(_cardPower, on);
        SetCardActive(_cardName, on);
        SetCardActive(_cardRave, on);

        SetCardActive(_cardOffCollapsed, off);
        if (off) _cardOffCollapsed.SetLabel("Lamp is off • Tap to turn on");

        SetCardActive(_cardRaveCollapsed, rave);
        if (rave) _cardRaveCollapsed.SetLabel("Rave Party active • Tap to stop");
    }

    void ApplyLampColor()
    {
        if (_lampDisc == null || defaultColors == null || defaultColors.Count == 0) return;
        if (lampState == LampState.ON) _lampDisc.color = ColorFromHex(defaultColors[_index].hex);
        else if (lampState == LampState.OFF) _lampDisc.color = new Color(0.9f, 0.9f, 0.9f);
        else _lampDisc.color = ColorFromHex("#FFD54F");
    }

    void ApplyCarouselVisuals()
    {
        if (_chipCenterImg == null || defaultColors == null || defaultColors.Count == 0) return;

        int n = defaultColors.Count;
        int idxFarLeft = (_index - 2 + n) % n;
        int idxLeft = (_index - 1 + n) % n;
        int idxCenter = _index;
        int idxRight = (_index + 1) % n;
        int idxFarRight = (_index + 2) % n;

        if (_chipFarLeftImg) _chipFarLeftImg.color = ColorFromHex(defaultColors[idxFarLeft].hex);
        if (_chipLeftImg) _chipLeftImg.color = ColorFromHex(defaultColors[idxLeft].hex);
        if (_chipCenterImg) _chipCenterImg.color = ColorFromHex(defaultColors[idxCenter].hex);
        if (_chipRightImg) _chipRightImg.color = ColorFromHex(defaultColors[idxRight].hex);
        if (_chipFarRightImg) _chipFarRightImg.color = ColorFromHex(defaultColors[idxFarRight].hex);

        SetChip(_chipFarLeft, X_FAR_LEFT, SCALE_FAR);
        SetChip(_chipLeft, X_LEFT, SCALE_NEAR);
        SetChip(_chipCenter, X_CENTER, SCALE_CENTER);
        SetChip(_chipRight, X_RIGHT, SCALE_NEAR);
        SetChip(_chipFarRight, X_FAR_RIGHT, SCALE_FAR);

        if (_colorName) _colorName.text = defaultColors[_index].name;
        if (_colorHex) _colorHex.text = defaultColors[_index].hex;
    }

    void ApplyFocusVisuals()
    {
        bool show = lampState == LampState.ON;

        _cardPower.SetFocus(show && focused == FocusedAction.power, focusRing);
        _cardName.SetFocus(show && focused == FocusedAction.name, focusRing);
        _cardRave.SetFocus(show && focused == FocusedAction.rave, focusRing);

        if (lampState == LampState.ON) _cardPower.SetLabel("Tap to turn off");
        else _cardPower.SetLabel("Tap to turn on");
    }

    // === ActionText / Listener toggling ===
    void UpdateActionTextUI()
    {
        if (actionText != null)
        {
            if (lampState == LampState.RAVE_PARTY) actionText.text = raveActionText;
            else if (lampState == LampState.OFF) actionText.text = offActionText;
            else /* ON */                                actionText.text = _isListening ? "" : "";
        }

        if (ListenerGO != null)
        {
            bool showListener = (lampState == LampState.ON && _isListening);
            if (ListenerGO.activeSelf != showListener)
                ListenerGO.SetActive(showListener);
        }
    }

    // ========================= Smooth carousel =========================
    float EaseOutCubic(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 3f);

    void SetChip(RectTransform rt, float x, float scale)
    {
        if (!rt) return;
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.localScale = Vector3.one * scale;
    }

    Image ShadowFor(Image main, Image cached)
    {
        if (cached != null) return cached;
        if (main == null) return null;
        var t = main.transform.Find("Shadow");
        return t ? t.GetComponent<Image>() : null;
    }

    void SetImageAndShadowEnabled(Image main, Image cachedShadow, bool enabled)
    {
        if (main) main.enabled = enabled;
        var sh = ShadowFor(main, cachedShadow);
        if (sh) sh.enabled = enabled;
    }

    IEnumerator CoCarouselSlide(int dir, int nextIndex)
    {
        _carouselBusy = true;

        // Order: 0:FarLeft, 1:Left, 2:Center, 3:Right, 4:FarRight
        var rts = new RectTransform[] { _chipFarLeft, _chipLeft, _chipCenter, _chipRight, _chipFarRight };
        var imgs = new Image[] { _chipFarLeftImg, _chipLeftImg, _chipCenterImg, _chipRightImg, _chipFarRightImg };

        Vector2[] startPos = new Vector2[5];
        float[] startScale = new float[5];
        for (int i = 0; i < 5; i++)
        {
            startPos[i] = rts[i].anchoredPosition;
            startScale[i] = rts[i].localScale.x;
        }

        // Target slot scales for this sweep (pre-rotation)
        float[] targetScale = new float[5];
        if (dir > 0) // move right (visual shift left)
        {
            targetScale[0] = SCALE_FAR;    // FarLeft -> off-left (keep FAR)
            targetScale[1] = SCALE_FAR;    // Left    -> FarLeft
            targetScale[2] = SCALE_NEAR;   // Center  -> Left
            targetScale[3] = SCALE_CENTER; // Right   -> Center
            targetScale[4] = SCALE_NEAR;   // FarRight-> Right
        }
        else // dir < 0, move left (visual shift right)
        {
            targetScale[4] = SCALE_FAR;    // FarRight -> off-right (keep FAR)
            targetScale[3] = SCALE_FAR;    // Right    -> FarRight
            targetScale[2] = SCALE_NEAR;   // Center   -> Right
            targetScale[1] = SCALE_CENTER; // Left     -> Center
            targetScale[0] = SCALE_NEAR;   // FarLeft  -> Left
        }

        float dur = Mathf.Max(0.01f, carouselAnimTime);
        float t = 0f;
        float delta = 120f * dir;

        // Hide the leaving edge + its shadow
        Image edgeImg = (dir > 0) ? _chipFarLeftImg : _chipFarRightImg;
        Image edgeShadowCached = (dir > 0) ? _chipFarLeftShadow : _chipFarRightShadow;
        SetImageAndShadowEnabled(edgeImg, edgeShadowCached, false);

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = EaseOutCubic(t / dur);

            for (int i = 0; i < 5; i++)
            {
                // Smooth position
                rts[i].anchoredPosition = new Vector2(startPos[i].x - delta * k, 0f);
                // Smooth scale
                float s = Mathf.Lerp(startScale[i], targetScale[i], k);
                rts[i].localScale = Vector3.one * s;
            }
            yield return null;
        }

        // Rotate references by exactly one slot
        if (dir > 0)
        {
            var r0 = _chipFarLeft; var i0 = _chipFarLeftImg; var s0 = _chipFarLeftShadow;
            _chipFarLeft = _chipLeft; _chipFarLeftImg = _chipLeftImg; _chipFarLeftShadow = null;
            _chipLeft = _chipCenter; _chipLeftImg = _chipCenterImg;
            _chipCenter = _chipRight; _chipCenterImg = _chipRightImg;
            _chipRight = _chipFarRight; _chipRightImg = _chipFarRightImg;
            _chipFarRight = r0; _chipFarRightImg = i0; _chipFarRightShadow = s0;
        }
        else
        {
            var r4 = _chipFarRight; var i4 = _chipFarRightImg; var s4 = _chipFarRightShadow;
            _chipFarRight = _chipRight; _chipFarRightImg = _chipRightImg; _chipFarRightShadow = null;
            _chipRight = _chipCenter; _chipRightImg = _chipCenterImg;
            _chipCenter = _chipLeft; _chipCenterImg = _chipLeftImg;
            _chipLeft = _chipFarLeft; _chipLeftImg = _chipFarLeftImg;
            _chipFarLeft = r4; _chipFarLeftImg = i4; _chipFarLeftShadow = s4;
        }

        // Commit center and refresh visuals (also resets canonical positions/scales)
        _index = nextIndex;
        ApplyLampColor();
        ApplyCarouselVisuals();
        onCarouselChanged?.Invoke();

        // Re-enable the edge we hid
        SetImageAndShadowEnabled(edgeImg, edgeShadowCached, true);

        _carouselBusy = false;
        _carouselAnim = null;
        onSlideCompleted?.Invoke(dir);
    }

    // ========================= Simulated "Name a color" =========================
    IEnumerator CoNameColor()
    {
        _isListening = true;
        UpdateActionTextUI(); // show ListenerGO, clear action text
        ShowToast("Say a color…");

        float t = 0f;
        while (t < listenTime)
        {
            t += Time.deltaTime;
            float k = 1f + Mathf.Sin(t * 8f) * 0.06f;
            if (_lampGroup) _lampGroup.localScale = new Vector3(k, k, 1);
            yield return null;
        }
        if (_lampGroup) _lampGroup.localScale = Vector3.one;

        bool success = UnityEngine.Random.value <= listenSuccessChance;
        if (success)
        {
            _index = UnityEngine.Random.Range(0, defaultColors.Count);
            ApplyCarouselVisuals();
            ShowToast($"Set to {defaultColors[_index].name}");
            onCarouselChanged?.Invoke();
        }
        else
        {
            ShowToast("Didn't catch that—try again");
        }

        _isListening = false;
        UpdateActionTextUI();
    }

    // ========================= Small helpers =========================
    void ShowToast(string msg, float duration = 1.5f)
    {
        if (_toastRoot == null) return;
        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(CoToast(msg, duration));
    }

    IEnumerator CoToast(string msg, float duration)
    {
        if (_toastText) _toastText.text = msg;
        CanvasGroup cg = _toastRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = _toastRoot.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        float t = 0f;
        while (t < 0.2f) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(0, 1, t / 0.2f); yield return null; }
        yield return new WaitForSeconds(duration);
        t = 0f;
        while (t < 0.3f) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(1, 0, t / 0.3f); yield return null; }
        cg.alpha = 0;
    }

    static void SetActiveRT(RectTransform rt, bool on) { if (rt != null) rt.gameObject.SetActive(on); }
    void SetCardActive(ActionCard card, bool on) { if (card.root != null) card.root.SetActive(on); }

    void SetShadow(Image img)
    {
        if (img == null) return;
        var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
        shadow.transform.SetParent(img.transform, false);
        var rt = shadow.GetComponent<RectTransform>();
        rt.sizeDelta = img.rectTransform.sizeDelta * 1.02f;
        rt.anchoredPosition = new Vector2(0, -3);
        var im = shadow.GetComponent<Image>();
        im.sprite = img.sprite;
        im.color = chipShadow;
    }

    // ========================= Build helpers =========================
    RectTransform CreateRT(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    TextMeshProUGUI CreateTMP(string name, RectTransform parent, int size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = name;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }

    Image CreateImage(string name, RectTransform parent, Sprite sprite, Color col, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = type;
        img.color = col;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    Button CreateArrowButton(string name, RectTransform parent, string glyph)
    {
        var rt = CreateRT(name, parent);
        rt.sizeDelta = new Vector2(44, 44);

        CreateImage("BG", rt, CreateDiscSprite(44), new Color(1, 1, 1, 0.9f), Image.Type.Simple);
        var label = CreateTMP("Glyph", rt, 20, new Color(0.15f, 0.15f, 0.2f), TextAlignmentOptions.Center);
        label.text = glyph;

        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors; colors.highlightedColor = new Color(1, 1, 1, 0.95f); btn.colors = colors;
        return btn;
    }

    ActionCard BuildActionCard(RectTransform listRoot, string label, string icon)
    {
        var rt = CreateRT("Card", listRoot);
        rt.sizeDelta = new Vector2(listRoot.sizeDelta.x, 74);

        var btn = rt.gameObject.AddComponent<Button>();

        var img = CreateImage("BG", rt, CreateRoundedSprite((int)rt.sizeDelta.x, (int)rt.sizeDelta.y, 22), themeCard, Image.Type.Sliced);
        img.rectTransform.sizeDelta = rt.sizeDelta;

        var focus = CreateImage("Focus", rt, CreateRoundedSprite((int)rt.sizeDelta.x + 10, (int)rt.sizeDelta.y + 10, 28), focusRing, Image.Type.Sliced);
        focus.rectTransform.sizeDelta = rt.sizeDelta + new Vector2(10, 10);
        focus.color = new Color(focusRing.r, focusRing.g, focusRing.b, 0.85f);
        focus.gameObject.SetActive(false);

        var iconCircle = CreateImage("IconCircle", rt, CreateDiscSprite(40), new Color(0, 0, 0, 0.1f), Image.Type.Simple);
        iconCircle.rectTransform.sizeDelta = new Vector2(40, 40);
        iconCircle.rectTransform.anchoredPosition = new Vector2(-rt.sizeDelta.x * 0.5f + 48, 0);

        var iconText = CreateTMP("IconGlyph", iconCircle.rectTransform, 20, Color.white, TextAlignmentOptions.Center);
        iconText.text = icon;

        var lab = CreateTMP("Label", rt, 20, cardText, TextAlignmentOptions.Left);
        lab.text = label;
        lab.rectTransform.anchoredPosition = new Vector2(-rt.sizeDelta.x * 0.5f + 110, 0);

        return new ActionCard
        {
            root = rt.gameObject,
            rt = rt,
            bg = img,
            focus = focus,
            iconCircle = iconCircle,
            iconGlyph = iconText,
            label = lab,
            button = btn
        };
    }

    void LayoutActionList(RectTransform listRoot)
    {
        float spacing = 16f;
        float y = 0f;
        var cards = new[] { _cardPower, _cardName, _cardRave };
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c.rt == null) continue;
            c.rt.anchoredPosition = new Vector2(0, y);
            y -= (c.rt.sizeDelta.y + spacing);
        }
    }

    ActionCard BuildSingleCard(RectTransform parent, string label, string icon)
    {
        var rt = CreateRT("SingleCard", parent);
        rt.sizeDelta = new Vector2(parent.sizeDelta.x - 120, 74);
        var btn = rt.gameObject.AddComponent<Button>();

        var bg = CreateImage("BG", rt, CreateRoundedSprite((int)rt.sizeDelta.x, (int)rt.sizeDelta.y, 22), themeCard, Image.Type.Sliced);
        var iconCircle = CreateImage("IconCircle", rt, CreateDiscSprite(40), new Color(0, 0, 0, 0.1f), Image.Type.Simple);
        iconCircle.rectTransform.sizeDelta = new Vector2(40, 40);
        iconCircle.rectTransform.anchoredPosition = new Vector2(-rt.sizeDelta.x * 0.5f + 48, 0);
        var iconText = CreateTMP("IconGlyph", iconCircle.rectTransform, 20, Color.white, TextAlignmentOptions.Center);
        iconText.text = icon;

        var lab = CreateTMP("Label", rt, 20, cardText, TextAlignmentOptions.Left);
        lab.text = label;
        lab.rectTransform.anchoredPosition = new Vector2(-rt.sizeDelta.x * 0.5f + 110, 0);

        return new ActionCard
        {
            root = rt.gameObject,
            rt = rt,
            bg = bg,
            iconCircle = iconCircle,
            iconGlyph = iconText,
            label = lab,
            button = btn
        };
    }

    ActionCard BindActionCard(Transform listRoot, int childIndex)
    {
        var rt = listRoot.GetChild(childIndex) as RectTransform;
        if (rt == null) return default;
        return new ActionCard
        {
            root = rt.gameObject,
            rt = rt,
            bg = rt.Find("BG")?.GetComponent<Image>(),
            focus = rt.Find("Focus")?.GetComponent<Image>(),
            iconCircle = rt.Find("IconCircle")?.GetComponent<Image>(),
            iconGlyph = rt.Find("IconCircle/IconGlyph")?.GetComponent<TextMeshProUGUI>(),
            label = rt.Find("Label")?.GetComponent<TextMeshProUGUI>(),
            button = rt.GetComponent<Button>(),
        };
    }

    // ========================= Sprites =========================
    Sprite CreateDiscSprite(int diameter)
    {
        int size = Mathf.Max(16, diameter);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var clear = new Color32(0, 0, 0, 0);
        var white = new Color32(255, 255, 255, 255);
        var px = new Color32[size * size];
        float r = (size - 1) * 0.5f, cx = r, cy = r;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                px[y * size + x] = (dx * dx + dy * dy <= r * r) ? white : clear;
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Sprite CreateRoundedSprite(int w, int h, int radius)
    {
        w = Mathf.Max(8, w); h = Mathf.Max(8, h);
        radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
        var clear = new Color32(0, 0, 0, 0); var solid = new Color32(255, 255, 255, 255);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside =
                    (x >= radius && x < w - radius) || (y >= radius && y < h - radius) ||
                    ((x - radius) * (x - radius) + (y - radius) * (y - radius) <= radius * radius) ||
                    ((x - (w - 1 - radius)) * (x - (w - 1 - radius)) + (y - radius) * (y - radius) <= radius * radius) ||
                    ((x - radius) * (x - radius) + (y - (h - 1 - radius)) * (y - (h - 1 - radius)) <= radius * radius) ||
                    ((x - (w - 1 - radius)) * (x - (w - 1 - radius)) + (y - (h - 1 - radius)) * (y - (h - 1 - radius)) <= radius * radius);
                px[y * w + x] = inside ? solid : clear;
            }
        tex.SetPixels32(px); tex.Apply(false, true);

        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(
            tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, border, false
        );
    }

    // ========================= Utility =========================
    static Color ColorFromHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return Color.white;
    }

    struct ActionCard
    {
        public GameObject root;
        public RectTransform rt;
        public Image bg;
        public Image focus;
        public Image iconCircle;
        public TextMeshProUGUI iconGlyph;
        public TextMeshProUGUI label;
        public Button button;

        public void SetFocus(bool on, Color ring)
        {
            if (focus == null) return;
            focus.color = new Color(ring.r, ring.g, ring.b, 0.85f);
            focus.gameObject.SetActive(on);
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }
    }
}
