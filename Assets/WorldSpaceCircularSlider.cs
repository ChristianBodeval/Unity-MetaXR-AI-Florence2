using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor; // for AssetDatabase.LoadAssetAtPath + scene saves in Edit Mode
#endif

[ExecuteAlways] // build/bind in Edit Mode so it persists in the scene
[DisallowMultipleComponent]
public class WorldSpaceCircularSliderUGUI : MonoBehaviour
{
    // ---------- Defaults (match your screenshot) ----------
    [Header("World-Space Canvas")]
    public int sizePx = 420;
    public float canvasScale = 0.002f;
    public Camera worldCamera; // assign your Main Camera if you like

    [Header("Dial")]
    [Range(0, 1)] public float Value = 0.466f;          // normalized
    [Range(0.05f, 1f)] public float arcFraction = 0.75f; // 75% arc (gap)
    public int ringThicknessPx = 68;
    public Color backgroundRingColor = Color.white;
    public Color fillRingColor = Color.black;
    public float startAngleOffsetDeg = 225f;            // start like your mock

    [Header("Center Icon")]
    public string iconAssetPath = "Assets/UI/Icons/soundIcon.png";
    public Sprite centerIcon;                           // auto-loaded in Editor if null
    public Color centerIconTint = new Color(0.25f, 0.28f, 0.35f, 1f);
    public float iconRotationDeg = 0f;                  // keep facing right

    [Header("Labels")]
    public string titleText = "Volume";
    public Color titleColor = new Color(0.38f, 0.43f, 0.51f, 1f);
    public Color percentColor = Color.black;

    [Header("Background Panel")]
    public bool showBackground = false;
    public Color backgroundColor = new Color(1, 1, 1, 0f);

    [Header("Events")]
    public UnityEngine.Events.UnityEvent<float> OnValueChanged;

    // runtime refs (rebound if already in scene)
    Canvas _canvas;
    RectTransform _rootRT, _dialRT, _arcRT;
    Image _panelBG, _bgRing, _fillRing, _iconBg, _iconImg;
    TextMeshProUGUI _titleTMP, _percentTMP;
    Sprite _donutSprite, _discSprite;
    bool _built;

    const string ROOT_NAME = "WS_CircularSlider_Root";

    // ---------- Lifecycle ----------
    void OnEnable()
    {
        // Bind/create in Edit Mode so the hierarchy is saved in the scene asset.
        if (!Application.isPlaying)
        {
            BindExistingOrBuild();
            ApplyAll();
        }
    }

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        BindExistingOrBuild(); // at runtime, bind if already in scene; else build once
        ApplyAll();
    }

    void OnValidate()
    {
        Value = Mathf.Clamp01(Value);
        arcFraction = Mathf.Clamp01(arcFraction);
        if (_built) ApplyAll();
    }

    // ---------- Public API ----------
    public void SetValue(float normalized01)
    {
        normalized01 = Mathf.Clamp01(normalized01);
        if (!Mathf.Approximately(Value, normalized01))
        {
            Value = normalized01;
            UpdateVisuals();
            OnValueChanged?.Invoke(Value);
        }
        else
        {
            UpdateVisuals();
        }
    }

    [ContextMenu("Rebuild Control (safe)")]
    void RebuildNow()
    {
        var existing = transform.Find(ROOT_NAME);
        if (existing != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(existing.gameObject);
            else Destroy(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }
        _built = false;
        BuildFresh();
        ApplyAll();
    }

    // ---------- Build / Bind ----------
    void BindExistingOrBuild()
    {
        // if already created in this scene, just bind to it (no duplicates)
        var root = transform.Find(ROOT_NAME);
        if (root != null)
        {
            CacheRefsFrom(root);
            _built = true;
        }
        else
        {
            BuildFresh();
        }
    }

    void BuildFresh()
    {
        // Root Canvas (world-space)
        var canvasGO = new GameObject(ROOT_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = worldCamera;
        _canvas.sortingOrder = 0;

        _rootRT = canvasGO.GetComponent<RectTransform>();
        _rootRT.sizeDelta = new Vector2(sizePx, sizePx + 160);
        _canvas.transform.localScale = Vector3.one * canvasScale;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100;

        // Background panel
        var panelGO = new GameObject("PanelBG", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        _panelBG = panelGO.GetComponent<Image>();
        var pRT = _panelBG.rectTransform;
        pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.one;
        pRT.offsetMin = Vector2.zero; pRT.offsetMax = Vector2.zero;
        _panelBG.transform.SetAsFirstSibling();

        // Dial (not rotated)
        var dialGO = new GameObject("Dial", typeof(RectTransform));
        dialGO.transform.SetParent(canvasGO.transform, false);
        _dialRT = dialGO.GetComponent<RectTransform>();
        _dialRT.anchorMin = _dialRT.anchorMax = new Vector2(0.5f, 0.7f);
        _dialRT.pivot = new Vector2(0.5f, 0.5f);
        _dialRT.sizeDelta = new Vector2(sizePx, sizePx);

        // Arc rotator (so icon stays upright)
        var arcGO = new GameObject("Arc", typeof(RectTransform));
        arcGO.transform.SetParent(_dialRT, false);
        _arcRT = arcGO.GetComponent<RectTransform>();
        _arcRT.anchorMin = _arcRT.anchorMax = new Vector2(0.5f, 0.5f);
        _arcRT.pivot = new Vector2(0.5f, 0.5f);
        _arcRT.sizeDelta = _dialRT.sizeDelta;

        _donutSprite = CreateDonutSprite(sizePx, ringThicknessPx);
        _discSprite = CreateDiscSprite(sizePx - ringThicknessPx * 2);

        _bgRing = CreateImage("BackgroundRing", _arcRT, _donutSprite, backgroundRingColor, true);
        _fillRing = CreateImage("FillRing", _arcRT, _donutSprite, fillRingColor, true);

        _iconBg = CreateImage("CenterDisc", _dialRT, _discSprite, Color.white, false);
        float inner = sizePx - ringThicknessPx * 2;
        _iconBg.rectTransform.sizeDelta = new Vector2(inner, inner);
        _iconBg.color = new Color(1, 1, 1, 0.98f);

        _iconImg = CreateImage("CenterIcon", _dialRT, centerIcon, centerIconTint, false);
        var iconRT = _iconImg.rectTransform;
        iconRT.sizeDelta = new Vector2(inner * 0.45f, inner * 0.45f);
        _iconImg.preserveAspect = true;

#if UNITY_EDITOR
        if (_iconImg.sprite == null && !string.IsNullOrEmpty(iconAssetPath))
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(iconAssetPath);
            if (s != null) _iconImg.sprite = s;
        }
#endif

        _titleTMP = CreateTMP("Title", _rootRT, 28, titleColor, TextAlignmentOptions.Center);
        var titleRT = _titleTMP.rectTransform;
        titleRT.anchorMin = titleRT.anchorMax = new Vector2(0.5f, 0.07f);
        titleRT.anchoredPosition = new Vector2(0, 18);
        titleRT.sizeDelta = new Vector2(sizePx, 40);

        _percentTMP = CreateTMP("Percent", _rootRT, 52, percentColor, TextAlignmentOptions.TopGeoAligned);
        var pRTxt = _percentTMP.rectTransform;
        pRTxt.anchorMin = pRTxt.anchorMax = new Vector2(0.5f, 0.0f);
        pRTxt.anchoredPosition = new Vector2(0, -18);
        pRTxt.sizeDelta = new Vector2(sizePx, 80);

        _built = true;
    }

    void CacheRefsFrom(Transform root)
    {
        _canvas = root.GetComponent<Canvas>();
        _rootRT = root.GetComponent<RectTransform>();
        _panelBG = root.Find("PanelBG")?.GetComponent<Image>();

        var dial = root.Find("Dial");
        _dialRT = dial ? dial.GetComponent<RectTransform>() : null;

        var arc = root.Find("Dial/Arc");
        _arcRT = arc ? arc.GetComponent<RectTransform>() : null;

        _bgRing = root.Find("Dial/Arc/BackgroundRing")?.GetComponent<Image>();
        _fillRing = root.Find("Dial/Arc/FillRing")?.GetComponent<Image>();
        _iconBg = root.Find("Dial/CenterDisc")?.GetComponent<Image>();
        _iconImg = root.Find("Dial/CenterIcon")?.GetComponent<Image>();

        _titleTMP = root.Find("Title")?.GetComponent<TextMeshProUGUI>();
        _percentTMP = root.Find("Percent")?.GetComponent<TextMeshProUGUI>();
    }

    // ---------- Apply ----------
    void ApplyAll()
    {
        if (!_built) return;

        if (_canvas.worldCamera == null && worldCamera != null)
            _canvas.worldCamera = worldCamera;

        _rootRT.sizeDelta = new Vector2(sizePx, sizePx + 160);
        _canvas.transform.localScale = Vector3.one * canvasScale;
        if (_dialRT != null) _dialRT.sizeDelta = new Vector2(sizePx, sizePx);
        if (_arcRT != null) _arcRT.sizeDelta = new Vector2(sizePx, sizePx);

        // regen sprites if size/thickness changed
        _donutSprite = CreateDonutSprite(sizePx, ringThicknessPx);
        _discSprite = CreateDiscSprite(sizePx - ringThicknessPx * 2);

        if (_bgRing != null) { _bgRing.sprite = _donutSprite; _bgRing.color = backgroundRingColor; }
        if (_fillRing != null) { _fillRing.sprite = _donutSprite; _fillRing.color = fillRingColor; }
        if (_iconBg != null) { _iconBg.sprite = _discSprite; _iconBg.color = new Color(1, 1, 1, 0.98f); }

        if (_iconImg != null)
        {
            if (centerIcon != null) _iconImg.sprite = centerIcon;
            _iconImg.color = centerIconTint;
            _iconImg.rectTransform.localEulerAngles = new Vector3(0, 0, iconRotationDeg);
        }

        if (_titleTMP != null) { _titleTMP.text = titleText; _titleTMP.color = titleColor; }
        if (_percentTMP != null) { _percentTMP.color = percentColor; }

        if (_arcRT != null) _arcRT.localEulerAngles = new Vector3(0, 0, -startAngleOffsetDeg);

        if (_panelBG != null)
        {
            _panelBG.gameObject.SetActive(showBackground);
            _panelBG.color = backgroundColor;
        }

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (_bgRing != null)
        {
            _bgRing.type = Image.Type.Filled;
            _bgRing.fillMethod = Image.FillMethod.Radial360;
            _bgRing.fillOrigin = (int)Image.Origin360.Top;
            _bgRing.fillClockwise = true;
            _bgRing.fillAmount = Mathf.Clamp01(arcFraction);
        }

        if (_fillRing != null)
        {
            _fillRing.type = Image.Type.Filled;
            _fillRing.fillMethod = Image.FillMethod.Radial360;
            _fillRing.fillOrigin = (int)Image.Origin360.Top;
            _fillRing.fillClockwise = true;
            _fillRing.fillAmount = Mathf.Clamp01(Value) * Mathf.Clamp01(arcFraction);
        }

        if (_percentTMP != null)
            _percentTMP.text = Mathf.RoundToInt(Value * 100f) + "%";
    }

    // ---------- Helpers ----------
    Image CreateImage(string name, RectTransform parent, Sprite sp, Color col, bool filledRadial)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.color = col;
        img.type = filledRadial ? Image.Type.Filled : Image.Type.Simple;
        if (filledRadial)
        {
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
        }
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = parent.sizeDelta;
        return img;
    }

    TextMeshProUGUI CreateTMP(string name, RectTransform parent, int fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = name;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;

        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return tmp;
    }

    Sprite CreateDonutSprite(int size, int thickness)
    {
        size = Mathf.Max(64, size);
        thickness = Mathf.Clamp(thickness, 4, size / 3);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var clear = new Color32(0, 0, 0, 0);
        var solid = new Color32(255, 255, 255, 255);
        var pixels = new Color32[size * size];

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rOuter = cx;
        float rInner = rOuter - thickness;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = (r >= rInner && r <= rOuter) ? solid : clear;
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Sprite CreateDiscSprite(int diameter)
    {
        int size = Mathf.Max(32, diameter);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var clear = new Color32(0, 0, 0, 0);
        var solid = new Color32(255, 255, 255, 255);
        var pixels = new Color32[size * size];

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rOuter = cx;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = (r <= rOuter) ? solid : clear;
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
