using UnityEngine;

[ExecuteAlways] // updates in Editor too
[RequireComponent(typeof(RectTransform))]
public class LabelUIPlacer : MonoBehaviour
{
    public enum Placement
    {
        Above,  // CenterBottom anchor → label appears above target
        Below   // CenterTop anchor → label appears below target
    }

    [Header("Placement Settings")]
    [SerializeField] private Placement placement = Placement.Above;

    [Tooltip("Y offset from anchor (positive = away from target).")]
    [SerializeField] private float yOffset = 50f;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplyPlacement();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!rectTransform) rectTransform = GetComponent<RectTransform>();
        ApplyPlacement();
    }
#endif

    private void ApplyPlacement()
    {
        if (!rectTransform) return;

        switch (placement)
        {
            case Placement.Above:
                // CenterBottom anchor → label grows upward from bottom
                rectTransform.anchorMin = new Vector2(0.5f, 0f);
                rectTransform.anchorMax = new Vector2(0.5f, 0f);
                rectTransform.pivot = new Vector2(0.5f, 0f);
                rectTransform.anchoredPosition = new Vector2(0, yOffset);
                break;

            case Placement.Below:
                // CenterTop anchor → label grows downward from top
                rectTransform.anchorMin = new Vector2(0.5f, 1f);
                rectTransform.anchorMax = new Vector2(0.5f, 1f);
                rectTransform.pivot = new Vector2(0.5f, 1f);
                rectTransform.anchoredPosition = new Vector2(0, -yOffset);
                break;
        }
    }
}
