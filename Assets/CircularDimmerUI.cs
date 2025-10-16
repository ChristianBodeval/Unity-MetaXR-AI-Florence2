using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CircularDimmerUI : MonoBehaviour
{
    [SerializeField] private Image arcFill;
    [SerializeField] private RectTransform leftIcon;
    [SerializeField] private RectTransform rightIcon;

    // sets value between 0–1 (maps to 0–100 brightness)
    public void SetValue(float normalized)
    {
        if (arcFill)
            arcFill.fillAmount = Mathf.Clamp01(normalized);
    }

    private void OnValidate()
    {
        if (arcFill)
            arcFill.fillAmount = Mathf.Clamp01(arcFill.fillAmount);
    }
}
