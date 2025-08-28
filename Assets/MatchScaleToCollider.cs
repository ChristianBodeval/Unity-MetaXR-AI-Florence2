using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MatchScaleToCollider : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Collider whose world-space size we want to match.")]
    public Collider sourceCollider;

    [Header("Options")]
    [Tooltip("Run every editor frame (edit mode). Turn off if you only want manual 'Fit Now'.")]
    public bool liveInEditMode = true;

    [Tooltip("Also update while the game is running.")]
    public bool updateInPlayMode = false;

    [Tooltip("Extra size added (meters) on each axis after matching the collider bounds.")]
    public Vector3 padding = Vector3.zero;

    [Tooltip("Multiply the final size (after padding). Useful for quick tweaks.")]
    public float scaleMultiplier = 1f;

    [Tooltip("Match individual axes. Uncheck to leave an axis unchanged.")]
    public bool matchX = true, matchY = true, matchZ = true;

    void Reset()
    {
        // Best guess: use a collider on this object if present
        if (!sourceCollider) sourceCollider = GetComponent<Collider>();
    }

    void OnValidate()
    {
        // Runs when values change in the inspector
        FitNow();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && liveInEditMode)
            FitNow();
#endif
        if (Application.isPlaying && updateInPlayMode)
            FitNow();
    }

    [ContextMenu("Fit Now")]
    public void FitNow()
    {
        if (!sourceCollider) return;

        // World-space size of the collider’s AABB
        Vector3 worldSize = sourceCollider.bounds.size + padding;
        worldSize *= Mathf.Max(0f, scaleMultiplier);

        // Convert world-size to this object's local scale (account for parent scale)
        Vector3 parentScale = Vector3.one;
        if (transform.parent)
            parentScale = transform.parent.lossyScale;

        // Avoid division by zero
        parentScale.x = Mathf.Approximately(parentScale.x, 0f) ? 1e-4f : parentScale.x;
        parentScale.y = Mathf.Approximately(parentScale.y, 0f) ? 1e-4f : parentScale.y;
        parentScale.z = Mathf.Approximately(parentScale.z, 0f) ? 1e-4f : parentScale.z;

        Vector3 targetLocalScale = new Vector3(
            worldSize.x / parentScale.x,
            worldSize.y / parentScale.y,
            worldSize.z / parentScale.z
        );

        // Only apply selected axes
        Vector3 ls = transform.localScale;
        if (matchX) ls.x = targetLocalScale.x;
        if (matchY) ls.y = targetLocalScale.y;
        if (matchZ) ls.z = targetLocalScale.z;

        transform.localScale = ls;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!sourceCollider) return;
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.15f);
        Gizmos.DrawCube(sourceCollider.bounds.center, sourceCollider.bounds.size);
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.7f);
        Gizmos.DrawWireCube(sourceCollider.bounds.center, sourceCollider.bounds.size);
    }
#endif
}
