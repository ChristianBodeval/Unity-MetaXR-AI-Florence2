using UnityEngine;

[ExecuteInEditMode]
public class YOffsetFollower : MonoBehaviour
{
    [Tooltip("Y offset from the parent, in world space.")]
    [SerializeField] private float yOffset = 0.05f;

    private Transform anchor;

    private void OnEnable()
    {
        anchor = transform.parent; // assume parent is the anchor
    }

    private void Update()
    {
        if (!anchor) return;

        // Follow parent's world position with global Y offset
        Vector3 pos = anchor.position;
        pos.y += yOffset;
        transform.position = pos;
    }
}
