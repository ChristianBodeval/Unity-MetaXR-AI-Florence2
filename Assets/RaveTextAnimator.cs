using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class RaveTextAnimator : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI tmp;
    public Image background;

    [Header("Beat / Timing")]
    public float bpm = 128f;
    public float timeScale = 1f;

    [Header("Rainbow Text")]
    public float hueSpeed = 0.6f;
    public float hueSpread = 0.15f;
    [Range(0f, 1.2f)] public float textValue = 1f;
    [Range(0f, 1f)] public float textSaturation = 1f;

    [Header("Character Wiggle")]
    public float jitterAmplitude = 2.5f;
    public float jitterFrequency = 9f;
    public float jitterCharPhase = 0.35f;

    [Header("Pulse (Scale)")]
    public float pulseAmount = 0.08f;
    public float pulseSharpness = 7f;

    [Header("Outline / Glow")]
    [Range(0f, 1f)] public float outlinePulse = 0.35f;
    [Range(0f, 1f)] public float glowPulse = 0.35f;

    [Header("Background Strobe")]
    public float bgHueSpeed = 0.3f;
    [Range(0f, 1f)] public float bgStrobeIntensity = 0.75f;
    public float bgPulseAmount = 0.06f;

    [Header("Performance")]
    public bool lightMode = false;

    [SerializeField] private int seed = 1337;

    // Internals
    private float _t;
    private float _beatPhase;
    private float _beat;
    private float _beatVelocity;
    private string _lastText;
    private System.Random _rng;
    private Material _matInstance;

    // IMPORTANT: do NOT touch ShaderUtilities at field init time.
    private int _idOutlineWidth;
    private int _idOutlineColor;
    private int _idGlowPower;
    private int _idGlowColor;
    private bool _hasOutlineWidth, _hasOutlineColor, _hasGlowPower, _hasGlowColor;

    [ContextMenu("Randomize Seed")]
    void RandomizeSeed()
    {
        seed = Random.Range(0, int.MaxValue);
        _rng = new System.Random(seed);
    }

    void Awake()
    {
        if (!tmp) tmp = GetComponent<TextMeshProUGUI>();

        // Make a unique material instance so changing outline/glow won’t affect other texts.
        if (tmp)
        {
            var inst = new Material(tmp.fontMaterial);
            tmp.fontMaterial = inst;
            _matInstance = inst;

            // SAFE: Cache ShaderUtilities property IDs now (in Awake, not at field init).
            _idOutlineWidth = ShaderUtilities.ID_OutlineWidth;
            _idOutlineColor = ShaderUtilities.ID_OutlineColor;
            _idGlowPower = ShaderUtilities.ID_GlowPower;
            _idGlowColor = ShaderUtilities.ID_GlowColor;

            // Remember which props exist on this material
            _hasOutlineWidth = _matInstance.HasProperty(_idOutlineWidth);
            _hasOutlineColor = _matInstance.HasProperty(_idOutlineColor);
            _hasGlowPower = _matInstance.HasProperty(_idGlowPower);
            _hasGlowColor = _matInstance.HasProperty(_idGlowColor);
        }

        _rng = new System.Random(seed);
        _lastText = tmp ? tmp.text : "";
    }

    void Update()
    {
        if (!tmp) return;

        _t += Time.deltaTime * Mathf.Max(0.0001f, timeScale);

        float beatsPerSec = bpm / 60f;
        _beatPhase = Mathf.Repeat(_t * beatsPerSec, 1f);
        float rawPulse = Mathf.Exp(-pulseSharpness * _beatPhase);
        _beat = Mathf.SmoothDamp(_beat, rawPulse, ref _beatVelocity, 0.03f);

        bool textChanged = _lastText != tmp.text;
        if (!lightMode || textChanged)
        {
            ApplyPerCharacterEffects();
            _lastText = tmp.text;
        }
        else if (_beat > 0.95f)
        {
            ApplyPerCharacterColorOnly();
        }

        // Global pulse
        float scalePulse = 1f + (pulseAmount * _beat);
        tmp.rectTransform.localScale = Vector3.one * scalePulse;

        // Outline / Glow
        if (_matInstance)
        {
            if (_hasOutlineWidth)
            {
                float baseOutline = 0.15f;
                _matInstance.SetFloat(_idOutlineWidth, baseOutline + outlinePulse * _beat);
            }
            if (_hasOutlineColor)
            {
                Color oc = Color.HSVToRGB(Mathf.Repeat(_t * 0.1f, 1f), 1f, 1f);
                _matInstance.SetColor(_idOutlineColor, oc);
            }
            if (_hasGlowPower)
            {
                float baseGlow = 0.2f;
                _matInstance.SetFloat(_idGlowPower, baseGlow + glowPulse * _beat);
            }
            if (_hasGlowColor)
            {
                Color gc = Color.HSVToRGB(Mathf.Repeat(_t * 0.08f, 1f), 0.9f, 1f);
                _matInstance.SetColor(_idGlowColor, gc);
            }
        }

        // Background strobe
        if (background)
        {
            float bgHue = Mathf.Repeat(_t * bgHueSpeed, 1f);
            float strobe = Mathf.Clamp01(bgStrobeIntensity * _beat);
            Color bg = Color.HSVToRGB(bgHue, 0.9f, Mathf.Lerp(0.15f, 1f, strobe));
            bg.a = 1f;
            background.color = bg;

            float bgScale = 1f + bgPulseAmount * _beat;
            background.rectTransform.localScale = new Vector3(bgScale, bgScale, 1f);
        }
    }

    void ApplyPerCharacterEffects()
    {
        tmp.ForceMeshUpdate();

        var textInfo = tmp.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0) return;

        float baseHue = Mathf.Repeat(_t * hueSpeed, 1f);

        for (int i = 0; i < charCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int matIndex = charInfo.materialReferenceIndex;
            int vertIndex = charInfo.vertexIndex;

            var vertices = textInfo.meshInfo[matIndex].vertices;
            var colors32 = textInfo.meshInfo[matIndex].colors32;

            float h = Mathf.Repeat(baseHue + i * hueSpread, 1f);
            Color32 c32 = (Color32)Color.HSVToRGB(h, textSaturation, textValue);

            colors32[vertIndex + 0] = c32;
            colors32[vertIndex + 1] = c32;
            colors32[vertIndex + 2] = c32;
            colors32[vertIndex + 3] = c32;

            float t = _t * jitterFrequency + i * jitterCharPhase;
            float ox = (Mathf.PerlinNoise(i * 0.173f, t) - 0.5f) * 2f * jitterAmplitude;
            float oy = (Mathf.PerlinNoise(i * 0.719f, t + 100f) - 0.5f) * 2f * jitterAmplitude;

            Vector3 offset = new Vector3(ox, oy, 0f);
            vertices[vertIndex + 0] += offset;
            vertices[vertIndex + 1] += offset;
            vertices[vertIndex + 2] += offset;
            vertices[vertIndex + 3] += offset;
        }

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            var meshInfo = textInfo.meshInfo[m];
            meshInfo.mesh.vertices = meshInfo.vertices;
            meshInfo.mesh.colors32 = meshInfo.colors32;
            tmp.UpdateGeometry(meshInfo.mesh, m);
        }
    }

    void ApplyPerCharacterColorOnly()
    {
        tmp.ForceMeshUpdate();

        var textInfo = tmp.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0) return;

        float baseHue = Mathf.Repeat(_t * hueSpeed, 1f);

        for (int i = 0; i < charCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int matIndex = charInfo.materialReferenceIndex;
            int vertIndex = charInfo.vertexIndex;
            var colors32 = textInfo.meshInfo[matIndex].colors32;

            float h = Mathf.Repeat(baseHue + i * hueSpread, 1f);
            Color32 c32 = (Color32)Color.HSVToRGB(h, textSaturation, textValue);

            colors32[vertIndex + 0] = c32;
            colors32[vertIndex + 1] = c32;
            colors32[vertIndex + 2] = c32;
            colors32[vertIndex + 3] = c32;
        }

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            var meshInfo = textInfo.meshInfo[m];
            meshInfo.mesh.colors32 = meshInfo.colors32;
            tmp.UpdateGeometry(meshInfo.mesh, m);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test Beat Pulse")]
    void TestBeatPulse() { _beat = 1f; }
#endif
}
