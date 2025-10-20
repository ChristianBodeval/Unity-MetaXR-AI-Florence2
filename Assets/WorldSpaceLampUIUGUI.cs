using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    // ===== Runtime refs =====
    const string ROOT_NAME = "WS_LampUI_Root";

    Canvas _canvas;
    RectTransform _root;
    Image _rootBg;

    // Top lamp
    RectTransform _lampGroup;
    Image _lampDisc;
    TextMeshProUGUI _lampBulbSymbol; // simple glyph via TMP

    // Carousel
    RectTransform _carouselRoot;
    Button _leftBtn, _rightBtn;
    RectTransform _chipLeft, _chipCenter, _chipRight;
    Image _chipLeftImg, _chipCenterImg, _chipRightImg;
    TextMeshProUGUI _colorName, _colorHex;

    // Action cards
    ActionCard _cardPower;
    ActionCard _cardName;
    ActionCard _cardRave;

    // Rave card (collapsed state)
    ActionCard _cardOffCollapsed;   // OFF: “Lamp is off • Tap to turn on”
    ActionCard _cardRaveCollapsed;  // RAVE_PARTY: “Rave Party active • Tap to stop”

    // Toast
    RectTransform _toastRoot;
    TextMeshProUGUI _toastText;
    Coroutine _toastCo;

    // Data
    int _index;
    bool _built;

    // ========================= Unity lifecycle =========================
    void OnEnable()
    {
        if (!Application.isPlaying && !buildInEditMode) return;
        BindOrBuild();
        ApplyAll(firstTime: true);
    }

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        BindOrBuild();
        ApplyAll(firstTime: true);
    }

    void OnValidate()
    {
        if (!_built) return;
        ApplyAll(firstTime: false);
    }

    void Update()
    {
        if (!Application.isPlaying || !_built) return;

        // Keyboard controls
        if (Input.GetKeyDown(KeyCode.UpArrow)) FocusUp();
        if (Input.GetKeyDown(KeyCode.DownArrow)) FocusDown();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) ColorLeft();
        if (Input.GetKeyDown(KeyCode.RightArrow)) ColorRight();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Tap();
    }

    // ========================= Public gestures =========================
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
    }

    public void ColorLeft()
    {
        if (lampState != LampState.ON) return;
        _index = (_index - 1 + defaultColors.Count) % defaultColors.Count;
        ApplyCarouselVisuals();
    }

    public void ColorRight()
    {
        if (lampState != LampState.ON) return;
        _index = (_index + 1) % defaultColors.Count;
        ApplyCarouselVisuals();
    }

    public void Tap()
    {
        switch (lampState)
        {
            case LampState.OFF:
                lampState = LampState.ON;
                focused = FocusedAction.name;
                ShowToast("Tap to turn on"); // quick feedback
                ApplyAll(false);
                break;

            case LampState.RAVE_PARTY:
                lampState = LampState.ON;
                ApplyAll(false);
                break;

            case LampState.ON:
                if (focused == FocusedAction.power)
                {
                    lampState = LampState.OFF;
                    ApplyAll(false);
                }
                else if (focused == FocusedAction.rave)
                {
                    lampState = LampState.RAVE_PARTY;
                    ApplyAll(false);
                }
                else
                {
                    StartCoroutine(CoNameColor());
                }
                break;
        }
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

        // Build
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

        _lampBulbSymbol = CreateTMP("💡", _lampGroup, 64, new Color(0.33f, 0.2f, 0.2f), TextAlignmentOptions.Center);
        _lampBulbSymbol.rectTransform.sizeDelta = new Vector2(96, 96);

        // ===== Carousel =====
        _carouselRoot = CreateRT("Carousel", _root);
        _carouselRoot.sizeDelta = new Vector2(canvasWidthPx - 120, 180);
        _carouselRoot.anchoredPosition = new Vector2(0, 150);

        _leftBtn = CreateArrowButton("LeftBtn", _carouselRoot, "<");
        _leftBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-(_carouselRoot.sizeDelta.x * 0.5f - 30), 0);
        _leftBtn.onClick.AddListener(ColorLeft);

        _rightBtn = CreateArrowButton("RightBtn", _carouselRoot, ">");
        _rightBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2((_carouselRoot.sizeDelta.x * 0.5f - 30), 0);
        _rightBtn.onClick.AddListener(ColorRight);

        // Three chips
        _chipLeft = CreateRT("ChipLeft", _carouselRoot); _chipLeft.sizeDelta = new Vector2(100, 100);
        _chipCenter = CreateRT("ChipCenter", _carouselRoot); _chipCenter.sizeDelta = new Vector2(140, 140);
        _chipRight = CreateRT("ChipRight", _carouselRoot); _chipRight.sizeDelta = new Vector2(100, 100);

        _chipLeftImg = CreateImage("Img", _chipLeft, CreateDiscSprite(100), Color.gray, Image.Type.Simple);
        _chipCenterImg = CreateImage("Img", _chipCenter, CreateDiscSprite(140), Color.gray, Image.Type.Simple);
        _chipRightImg = CreateImage("Img", _chipRight, CreateDiscSprite(100), Color.gray, Image.Type.Simple);

        SetShadow(_chipLeftImg); SetShadow(_chipCenterImg); SetShadow(_chipRightImg);

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
        _cardPower.root.GetComponent<Button>().onClick.AddListener(() => { focused = FocusedAction.power; Tap(); });

        _cardName = BuildActionCard(listRoot, "Name a color", "🎤");
        _cardName.root.GetComponent<Button>().onClick.AddListener(() => { focused = FocusedAction.name; Tap(); });

        _cardRave = BuildActionCard(listRoot, "Rave Party", "✨");
        _cardRave.root.GetComponent<Button>().onClick.AddListener(() => { focused = FocusedAction.rave; Tap(); });

        LayoutActionList(listRoot);

        // ===== Collapsed cards =====
        // OFF
        _cardOffCollapsed = BuildSingleCard(_root, "Lamp is off • Tap to turn on", "⏻");
        _cardOffCollapsed.rt.anchoredPosition = new Vector2(0, -100);
        _cardOffCollapsed.root.GetComponent<Button>().onClick.AddListener(() => { lampState = LampState.ON; focused = FocusedAction.name; ApplyAll(false); });

        // RAVE PARTY
        _cardRaveCollapsed = BuildSingleCard(_root, "Rave Party active • Tap to stop", "✨");
        _cardRaveCollapsed.rt.anchoredPosition = new Vector2(0, -100);
        _cardRaveCollapsed.root.GetComponent<Button>().onClick.AddListener(() => { lampState = LampState.ON; ApplyAll(false); });

        // ===== Toast =====
        _toastRoot = CreateRT("Toast", _root);
        _toastRoot.sizeDelta = new Vector2(canvasWidthPx, 40);
        _toastRoot.anchoredPosition = new Vector2(0, -canvasHeightPx * 0.5f + 24);
        _toastText = CreateTMP("", _toastRoot, 16, new Color(0.2f, 0.2f, 0.25f), TextAlignmentOptions.Center);
        _toastText.enableWordWrapping = false;

        // ===== Init data =====
        _index = Mathf.Clamp(initialColorIndex, 0, defaultColors.Count - 1);

        _built = true;
    }

    void CacheRefsFrom(Transform root)
    {
        // When rebinding an already-built hierarchy (no duplicates)
        _root = root.GetComponent<RectTransform>();
        _canvas = root.GetComponent<Canvas>();
        _rootBg = root.Find("PageBG")?.GetComponent<Image>();

        _lampGroup = root.Find("LampGroup") as RectTransform;
        _lampDisc = root.Find("LampGroup/LampDisc")?.GetComponent<Image>();
        _lampBulbSymbol = root.Find("LampGroup/💡")?.GetComponent<TextMeshProUGUI>();

        _carouselRoot = root.Find("Carousel") as RectTransform;
        _leftBtn = root.Find("Carousel/LeftBtn")?.GetComponent<Button>();
        _rightBtn = root.Find("Carousel/RightBtn")?.GetComponent<Button>();
        _chipLeft = root.Find("Carousel/ChipLeft") as RectTransform;
        _chipCenter = root.Find("Carousel/ChipCenter") as RectTransform;
        _chipRight = root.Find("Carousel/ChipRight") as RectTransform;
        _chipLeftImg = root.Find("Carousel/ChipLeft/Img")?.GetComponent<Image>();
        _chipCenterImg = root.Find("Carousel/ChipCenter/Img")?.GetComponent<Image>();
        _chipRightImg = root.Find("Carousel/ChipRight/Img")?.GetComponent<Image>();

        _colorName = root.Find("ColorName")?.GetComponent<TextMeshProUGUI>();
        _colorHex = root.Find("ColorHex")?.GetComponent<TextMeshProUGUI>();

        var listRoot = root.Find("ActionList") as RectTransform;
        if (listRoot != null)
        {
            _cardPower = BindActionCard(listRoot, 0);
            _cardName = BindActionCard(listRoot, 1);
            _cardRave = BindActionCard(listRoot, 2);
        }

        _cardOffCollapsed = BindSingleCard(root, "OffCollapsed");
        _cardRaveCollapsed = BindSingleCard(root, "RaveCollapsed");

        _toastRoot = root.Find("Toast") as RectTransform;
        _toastText = root.Find("Toast/")?.GetComponent<TextMeshProUGUI>(); // optional
        if (_toastText == null) _toastText = root.Find("Toast/ColorName")?.GetComponent<TextMeshProUGUI>(); // fallback
    }

    // ========================= Apply Visuals =========================
    void ApplyAll(bool firstTime)
    {
        if (!_built) return;

        if (_canvas != null && _canvas.worldCamera == null && worldCamera != null)
            _canvas.worldCamera = worldCamera;

        // Background color
        if (_rootBg) _rootBg.color = pageBg;

        // Lamp color = current chip color (ON) / animated (RAVE)
        ApplyLampColor();

        // Carousel visuals (position three chips, color + labels)
        ApplyCarouselVisuals();

        // Section visibility by state
        ApplyVisibleSections();

        // Focus rings for the list
        ApplyFocusVisuals();
    }

    void ApplyVisibleSections()
    {
        if (!_built) return;

        bool on = lampState == LampState.ON;
        bool off = lampState == LampState.OFF;
        bool rave = lampState == LampState.RAVE_PARTY;

        // Carousel & list only in ON
        SetActiveRT(_carouselRoot, on);
        SetActiveRT(_colorName?.rectTransform, on);
        SetActiveRT(_colorHex?.rectTransform, on);
        SetCardActive(_cardPower, on);
        SetCardActive(_cardName, on);
        SetCardActive(_cardRave, on);

        // OFF collapsed card
        SetCardActive(_cardOffCollapsed, off);
        if (off) _cardOffCollapsed.SetLabel("Lamp is off • Tap to turn on");

        // RAVE collapsed card
        SetCardActive(_cardRaveCollapsed, rave);
        if (rave) _cardRaveCollapsed.SetLabel("Rave Party active • Tap to stop");
    }

    void ApplyLampColor()
    {
        if (_lampDisc == null) return;

        if (lampState == LampState.ON)
        {
            var c = ColorFromHex(defaultColors[_index].hex);
            _lampDisc.color = c;
        }
        else if (lampState == LampState.OFF)
        {
            _lampDisc.color = new Color(0.9f, 0.9f, 0.9f);
        }
        else // RAVE_PARTY — flash via coroutine-ish Update? here just set vivid color
        {
            _lampDisc.color = ColorFromHex("#FFD54F");
        }
    }

    void ApplyCarouselVisuals()
    {
        if (_chipCenterImg == null) return;

        // current, left, right
        int leftIdx = (_index - 1 + defaultColors.Count) % defaultColors.Count;
        int rightIdx = (_index + 1) % defaultColors.Count;

        var leftC = ColorFromHex(defaultColors[leftIdx].hex);
        var centC = ColorFromHex(defaultColors[_index].hex);
        var rightC = ColorFromHex(defaultColors[rightIdx].hex);

        _chipLeftImg.color = leftC;
        _chipCenterImg.color = centC;
        _chipRightImg.color = rightC;

        // positions & sizes
        if (_chipLeft) _chipLeft.anchoredPosition = new Vector2(-120, 0);
        if (_chipCenter) _chipCenter.anchoredPosition = Vector2.zero;
        if (_chipRight) _chipRight.anchoredPosition = new Vector2(120, 0);

        if (_chipLeft) _chipLeft.localScale = Vector3.one * 0.6f;
        if (_chipCenter) _chipCenter.localScale = Vector3.one * 1.0f;
        if (_chipRight) _chipRight.localScale = Vector3.one * 0.6f;

        if (_colorName) _colorName.text = defaultColors[_index].name;
        if (_colorHex) _colorHex.text = defaultColors[_index].hex;
    }

    void ApplyFocusVisuals()
    {
        // focus rings only visible in ON
        bool show = lampState == LampState.ON;

        _cardPower.SetFocus(show && focused == FocusedAction.power, focusRing);
        _cardName.SetFocus(show && focused == FocusedAction.name, focusRing);
        _cardRave.SetFocus(show && focused == FocusedAction.rave, focusRing);

        // Power card label changes with state
        if (lampState == LampState.ON) _cardPower.SetLabel("Tap to turn off");
        else _cardPower.SetLabel("Tap to turn on");
    }

    // ========================= Simulated "Name a color" =========================
    IEnumerator CoNameColor()
    {
        ShowToast("Say a color…");

        // pulse lamp while listening
        float t = 0f;
        while (t < listenTime)
        {
            t += Time.deltaTime;
            float k = 1f + Mathf.Sin(t * 8f) * 0.06f; // subtle pulse
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
        }
        else
        {
            ShowToast("Didn't catch that—try again");
        }
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

    // Shadow (cheap): add an extra Image under the disc
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

    TextMeshProUGUI CreateTMP(string text, RectTransform parent, int size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
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

        var bg = CreateImage("BG", rt, CreateDiscSprite(44), new Color(1, 1, 1, 0.9f), Image.Type.Simple);
        var label = CreateTMP(glyph, rt, 20, new Color(0.15f, 0.15f, 0.2f), TextAlignmentOptions.Center);

        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors; colors.highlightedColor = new Color(1, 1, 1, 0.95f); btn.colors = colors;
        return btn;
    }

    ActionCard BuildActionCard(RectTransform listRoot, string label, string icon)
    {
        var rt = CreateRT("Card", listRoot);
        rt.sizeDelta = new Vector2(listRoot.sizeDelta.x, 74);

        // Button root
        var btn = rt.gameObject.AddComponent<Button>();

        // Card BG (rounded)
        var img = CreateImage("BG", rt, CreateRoundedSprite((int)rt.sizeDelta.x, (int)rt.sizeDelta.y, 22), themeCard, Image.Type.Sliced);
        img.rectTransform.sizeDelta = rt.sizeDelta;

        // Focus ring overlay (disabled by default)
        var focus = CreateImage("Focus", rt, CreateRoundedSprite((int)rt.sizeDelta.x + 10, (int)rt.sizeDelta.y + 10, 28), focusRing, Image.Type.Sliced);
        focus.rectTransform.sizeDelta = rt.sizeDelta + new Vector2(10, 10);
        focus.color = new Color(focusRing.r, focusRing.g, focusRing.b, 0.85f);
        focus.gameObject.SetActive(false);

        // Icon circle
        var iconCircle = CreateImage("IconCircle", rt, CreateDiscSprite(40), new Color(0, 0, 0, 0.1f), Image.Type.Simple);
        iconCircle.rectTransform.sizeDelta = new Vector2(40, 40);
        iconCircle.rectTransform.anchoredPosition = new Vector2(-rt.sizeDelta.x * 0.5f + 48, 0);

        var iconText = CreateTMP(icon, iconCircle.rectTransform, 20, Color.white, TextAlignmentOptions.Center);

        // Label
        var lab = CreateTMP(label, rt, 20, cardText, TextAlignmentOptions.Left);
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
        var iconText = CreateTMP(icon, iconCircle.rectTransform, 20, Color.white, TextAlignmentOptions.Center);

        var lab = CreateTMP(label, rt, 20, cardText, TextAlignmentOptions.Left);
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
            iconGlyph = rt.Find("IconCircle/")?.GetComponent<TextMeshProUGUI>(),
            label = rt.Find("ColorName")?.GetComponent<TextMeshProUGUI>(),
            button = rt.GetComponent<Button>(),
        };
    }

    ActionCard BindSingleCard(Transform parent, string nameContains)
    {
        foreach (Transform t in parent)
        {
            if (t.name.Contains("SingleCard"))
            {
                var rt = t as RectTransform;
                return new ActionCard
                {
                    root = rt.gameObject,
                    rt = rt,
                    bg = rt.Find("BG")?.GetComponent<Image>(),
                    iconCircle = rt.Find("IconCircle")?.GetComponent<Image>(),
                    iconGlyph = rt.Find("IconCircle/")?.GetComponent<TextMeshProUGUI>(),
                    label = rt.Find("ColorName")?.GetComponent<TextMeshProUGUI>(),
                    button = rt.GetComponent<Button>()
                };
            }
        }
        return default;
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

    // ========================= Nested card type =========================
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
