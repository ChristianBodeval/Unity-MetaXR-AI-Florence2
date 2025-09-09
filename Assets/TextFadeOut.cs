using NaughtyAttributes;
using System.Collections;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextFadeOut : MonoBehaviour
{
    [Header("Targets (optional)")]
    [SerializeField] private TMP_Text tmpText;          // TextMeshProUGUI or TMP_Text
    [SerializeField] private Image uiImage;             // UGUI Image
    [SerializeField] private RectTransform moveRect;    // Rect to move

    [Header("Fade Settings")]
    [MinValue(0f)]
    [SerializeField] private float fadeDuration = 1f;

    [CurveRange(0, 0, 1, 1, EColor.Orange)]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Move Settings")]
    [MinValue(0f)]
    [SerializeField] private float moveDuration = 1f;

    [SerializeField] private float moveDistance = 100f;

    [CurveRange(0, 0, 1, 1, EColor.Green)]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private float startDelay = 0f;

    [Header("Lifecycle")]
    [Tooltip("Seconds after animation finishes before destroying this GameObject. Set to 0 to never destroy.")]
    [SerializeField] private float destroyAfterSeconds = 0f;

    // cached initial state
    private Color _startTextColor;
    private Color _startImageColor;
    private Vector2 _startAnchoredPos;

    private Coroutine _routine;

    private void Awake()
    {
        if (tmpText) _startTextColor = tmpText.color;
        if (uiImage) _startImageColor = uiImage.color;
        if (moveRect) _startAnchoredPos = moveRect.anchoredPosition;


        tmpText.color = new Color(tmpText.color.r, tmpText.color.g, tmpText.color.b, 0f);

        uiImage.color = new Color(uiImage.color.r, uiImage.color.g, uiImage.color.b, 0f);

    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play(tmpText.text);
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    [Button("▶ Play Animation")]
    public void TestPlay()
    {
        Play("<color=#F28C33>find</color> <color=#F28C33>add note</color> to <color=#40B2FF>oven</color>");
    }

    public void Play(string commandText)
    {
        // Re-enable visuals so they’re visible when starting again

        tmpText.color = _startTextColor;
        uiImage.color = _startImageColor;

        tmpText.text = commandText;


        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeAndMoveRoutine());
    }



    private IEnumerator FadeAndMoveRoutine()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float time = 0f;
        float total = Mathf.Max(fadeDuration, moveDuration, 0.0001f);

        float startTextA = tmpText ? tmpText.color.a : 1f;
        float startImageA = uiImage ? uiImage.color.a : 1f;

        while (time < total)
        {
            float tFade = fadeDuration > 0f ? Mathf.Clamp01(time / fadeDuration) : 1f;
            float tMove = moveDuration > 0f ? Mathf.Clamp01(time / moveDuration) : 1f;

            // Fade
            if (tmpText && fadeDuration > 0f)
            {
                float fadeProgress = Mathf.Clamp01(fadeCurve.Evaluate(tFade));
                float a = Mathf.Lerp(startTextA, 0f, fadeProgress);
                var c = tmpText.color; c.a = a; tmpText.color = c;
            }
            if (uiImage && fadeDuration > 0f)
            {
                float fadeProgress = Mathf.Clamp01(fadeCurve.Evaluate(tFade));
                float a = Mathf.Lerp(startImageA, 0f, fadeProgress);
                var c2 = uiImage.color; c2.a = a; uiImage.color = c2;
            }

            // Move
            if (moveRect && moveDuration > 0f)
            {
                float moveProgress = Mathf.Clamp01(moveCurve.Evaluate(tMove));
                moveRect.anchoredPosition = _startAnchoredPos + Vector2.up * (moveDistance * moveProgress);
            }

            time += Time.deltaTime;
            yield return null;
        }

        // Snap to final state
        if (tmpText) { var c = tmpText.color; c.a = 0f; tmpText.color = c; }
        if (uiImage) { var c2 = uiImage.color; c2.a = 0f; uiImage.color = c2; }
        if (moveRect) moveRect.anchoredPosition = _startAnchoredPos + Vector2.up * moveDistance;

        // Reset back to initial (so next Play starts from fresh state)

        // Hide by disabling components so they’re not visible until next Play

        // Optional destroy
        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);

        _routine = null;
    }
}
