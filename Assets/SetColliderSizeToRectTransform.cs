using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class SetColliderSizeToRectTransform : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform targetRectTransform;
    [SerializeField] private BoxCollider targetCollider;


    [SerializeField] float zValue;
    void Reset()
    {
        // Auto-assign if possible
        targetRectTransform = GetComponent<RectTransform>();
        targetCollider = GetComponent<BoxCollider>();
    }

    void Update()
    {
        if (targetRectTransform == null || targetCollider == null)
            return;

        // Convert rect size from local space
        Vector2 size = targetRectTransform.rect.size;

        // Apply size to collider (z stays unchanged)
        targetCollider.size = new Vector3(size.x, size.y, zValue);

        // Keep collider centered to rect
        targetCollider.center = new Vector3(
            targetRectTransform.rect.center.x,
            targetRectTransform.rect.center.y,
            targetCollider.center.z
        );
    }
}
