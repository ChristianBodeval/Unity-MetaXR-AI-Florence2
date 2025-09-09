using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;

public class TranscriptionUI : MonoBehaviour
{
    [Header("Behavior")]
    [MinValue(0f)]
    public float secondsUntilHidden = 0f;

    [Tooltip("Also SetActive(false) on the root when hidden.")]
    public bool disableRootOnHide = false;

    [SerializeField] private bool isHidden;

    // Saved original alphas captured on Awake
    private float _textAlpha = 1f;
    private float _micAlpha = 1f;
    private float _bgAlpha = 1f;

    [Header("References")]
    [SerializeField] TMP_Text promptText;
    [SerializeField] Image micImage;
    [SerializeField] Image background;
    [SerializeField] TMP_Text commandThrownText;
    [SerializeField] RectTransform commandThrownRoot; // the parent that should resize
    [SerializeField] RectTransform promptRoot; // the parent that should resize
    private bool hasUpdated;

    public bool IsHidden
    {
        get => isHidden;
        set
        {
            if (isHidden == value) return;
            isHidden = value;
            ApplyHiddenState(isHidden);
            ForceUIUpdate();
        }
    }

    public void Update()
    {
        if(!hasUpdated)
        {
            Invoke("ForceUIUpdate",0.1f);
            hasUpdated = false;
        }
    }

    public void ForceUIUpdate()
    {
        // Make sure TMP has updated its metrics
        commandThrownText.ForceMeshUpdate();
        // Force the layout to recompute immediately (child first, then parent)
        LayoutRebuilder.ForceRebuildLayoutImmediate(commandThrownText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(commandThrownRoot);
    }


    private void Awake()
    {
        // Save original alphas
        if (promptText) _textAlpha = promptText.color.a;
        if (micImage) _micAlpha = micImage.color.a;
        if (background) _bgAlpha = background.color.a;

        // Apply initial state based on isHidden
        ApplyHiddenState(isHidden);
    }

    [Button]
    public void HideTest() => IsHidden = true;

    [Button]
    public void ShowTest() => IsHidden = false;

    /// <summary>Hide after the configured delay, preserving alpha logic.</summary>
    public void HideAfterDelay() => StartCoroutine(HideAfterDelayRoutine());

    private IEnumerator HideAfterDelayRoutine()
    {
        if (secondsUntilHidden > 0f)
            yield return new WaitForSeconds(secondsUntilHidden);

        IsHidden = true;
    }

    /// <summary>Apply alpha 0 when hidden, restore saved alpha when shown.</summary>
    private void ApplyHiddenState(bool hide)
    {
        if (promptText)
        {
            var c = promptText.color;
            c.a = hide ? 0f : _textAlpha;
            promptText.color = c;
        }

        if (micImage)
        {
            var c = micImage.color;
            c.a = hide ? 0f : _micAlpha;
            micImage.color = c;
        }

        if (background)
        {
            var c = background.color;
            c.a = hide ? 0f : _bgAlpha;
            background.color = c;
        }

    }
}
