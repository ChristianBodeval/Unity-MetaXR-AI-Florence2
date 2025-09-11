using UnityEngine;


[ExecuteInEditMode]
public class SelectionArrow : MonoBehaviour
{
    public enum Placement
    {
        Above,  // CenterBottom anchor → label appears above target
        Below   // CenterTop anchor → label appears below target
    }

    [Header("Placement Settings")]
    [SerializeField] private Placement placement = Placement.Above;

    [Tooltip("Y offset from anchor (positive = away from target, world-space).")]
    [SerializeField] private float yOffset = 0.05f;

    private Transform cameraTransform;
    private RectTransform rectTransform;
    private Transform anchor; // Parent/anchor object we are following

    private void OnEnable()
    {
        rectTransform = GetComponent<RectTransform>();
        cameraTransform = Camera.main.transform;
        anchor = transform.parent; // assume the parent is the target anchor
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!rectTransform) rectTransform = GetComponent<RectTransform>();
    }
#endif

    void SetPlacement()
    {
        if (cameraTransform == null || rectTransform == null) return;

        if (cameraTransform.position.y > rectTransform.position.y + yOffset * 1.1f)
        {
            placement = Placement.Above;
        }
        else if (cameraTransform.position.y < rectTransform.position.y - yOffset * 1.1f)
        {
            placement = Placement.Below;
        }
    }

    private void Update()
    {
        ApplyPlacement();
    }

    public bool useDynamicPlacement;

    private void ApplyPlacement()
    {
        if (!rectTransform || !anchor) return;

        if(useDynamicPlacement) SetPlacement();

        Vector3 worldOffset = Vector3.up * yOffset;

        switch (placement)
        {
            case Placement.Above:
                transform.localScale = new Vector3(transform.localScale.x, Mathf.Abs(transform.localScale.y), transform.localScale.z);
                rectTransform.position = anchor.position + worldOffset; // global Y offset
                break;

            case Placement.Below:

                transform.localScale = new Vector3(transform.localScale.x, -Mathf.Abs(transform.localScale.y), transform.localScale.z);
                rectTransform.position = anchor.position - worldOffset; // global Y offset
                break;
        }
    }
}
