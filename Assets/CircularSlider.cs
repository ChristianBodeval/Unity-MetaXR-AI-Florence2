using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CircularSlider : MonoBehaviour
{
    [Range(0f, 1f)]
    public float value = 0.75f;

    public Sprite ringSprite;
    public Sprite iconSprite;

    private GameObject root;
    private Image backgroundRing;
    private Image fillRing;
    private Image centerIcon;

    private const float size = 80f;

    void Awake()
    {
        EnsureSetup();
        UpdateVisuals();
    }

    void OnValidate()
    {
        UpdateVisuals();
    }

    void Update()
    {
        // Just for live preview in editor
        UpdateVisuals();
    }

    void EnsureSetup()
    {
        // Prevent duplicates
        if (root != null) return;

        // Create a parent Canvas if needed
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // Create root
        root = new GameObject("CircularSliderRoot", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // Background Ring
        GameObject bgGO = new GameObject("BackgroundRing", typeof(Image));
        bgGO.transform.SetParent(root.transform, false);
        backgroundRing = bgGO.GetComponent<Image>();
        backgroundRing.color = new Color(1f, 1f, 1f, 0.2f);
        backgroundRing.sprite = ringSprite;
        backgroundRing.type = Image.Type.Filled;
        backgroundRing.fillMethod = Image.FillMethod.Radial360;
        backgroundRing.fillAmount = 1f;

        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(size, size);

        // Fill Ring
        GameObject fillGO = new GameObject("FillRing", typeof(Image));
        fillGO.transform.SetParent(root.transform, false);
        fillRing = fillGO.GetComponent<Image>();
        fillRing.color = Color.white;
        fillRing.sprite = ringSprite;
        fillRing.type = Image.Type.Filled;
        fillRing.fillMethod = Image.FillMethod.Radial360;
        fillRing.fillOrigin = (int)Image.Origin360.Top;
        fillRing.fillClockwise = true;
        fillRing.fillAmount = value;

        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.sizeDelta = new Vector2(size, size);

        // Center Icon
        GameObject iconGO = new GameObject("CenterIcon", typeof(Image));
        iconGO.transform.SetParent(root.transform, false);
        centerIcon = iconGO.GetComponent<Image>();
        centerIcon.sprite = iconSprite;
        centerIcon.color = Color.white;
        centerIcon.preserveAspect = true;

        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(size * 0.45f, size * 0.45f);
    }

    void UpdateVisuals()
    {
        if (fillRing != null)
        {
            fillRing.fillAmount = Mathf.Clamp01(value);
        }
    }
}
