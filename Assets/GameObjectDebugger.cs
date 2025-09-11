using System.Text;
using System.Linq;
using TMPro;
using UnityEngine;
using NaughtyAttributes; // for EnumFlags, OnValueChanged, Button

[RequireComponent(typeof(TMP_Text))]
public class GameObjectDebugger : MonoBehaviour
{
    [System.Flags]
    public enum InfoSections
    {
        None = 0,
        Name = 1 << 0,
        Transform = 1 << 1,
        Hierarchy = 1 << 2,
        Components = 1 << 3,
        Bounds = 1 << 4,
        All = ~0
    }

    [Header("Debug Settings")]
    [EnumFlags, OnValueChanged(nameof(UpdateDebugText))]
    [SerializeField]
    private InfoSections infoToShow =
        InfoSections.Name | InfoSections.Transform | InfoSections.Hierarchy;

    [Tooltip("World-space (true) vs Local-space (false) transform values.")]
    [OnValueChanged(nameof(UpdateDebugText))]
    [SerializeField] private bool useWorldSpace = true;

    [Tooltip("Decimal places for numbers.")]
    [OnValueChanged(nameof(UpdateDebugText))]
    [Range(0, 5)]
    [SerializeField] private int decimals = 2;

    [Tooltip("Update every frame at runtime.")]
    [SerializeField] private bool updateEveryFrame = false;

    [Button("Refresh Now")]
    private void RefreshNow() => UpdateDebugText();

    [Header("References")]
    [Tooltip("Leave empty to use this GameObject's Transform.")]
    [SerializeField] private Transform targetTransform;

    [SerializeField] private TMP_Text text;

    [SerializeField] private string objectName;

    private void Awake()
    {
        if (!text) text = GetComponent<TMP_Text>();
        if (!targetTransform) targetTransform = transform;
        UpdateDebugText();
    }

    private void Update()
    {
        if (updateEveryFrame) UpdateDebugText();
    }

    private void OnValidate()
    {
        // Keep references stable in the editor
        if (!text) text = GetComponent<TMP_Text>();
        if (!targetTransform) targetTransform = transform;
        UpdateDebugText();
    }

    private void UpdateDebugText()
    {
        if (!text || !targetTransform) return;

        var go = targetTransform.gameObject;
        text.richText = true;
        var sb = new StringBuilder();

        string f(float v) => v.ToString($"F{decimals}");
        string fVec(Vector3 v) => $"({f(v.x)}, {f(v.y)}, {f(v.z)})";

        // NAME / BASIC
        if (infoToShow.HasFlag(InfoSections.Name))
        {
            string layerName = LayerMask.LayerToName(go.layer);
            if (string.IsNullOrEmpty(layerName)) layerName = $"Layer {go.layer}";
            sb.AppendLine($"<b><size=120%>{go.name}</size></b>");
            sb.AppendLine($"• Tag: {go.tag}");
            sb.AppendLine($"• Layer: {layerName}");
            sb.AppendLine($"• ActiveSelf / InHierarchy: {go.activeSelf} / {go.activeInHierarchy}");
            sb.AppendLine($"• InstanceID: {go.GetInstanceID()}");
            sb.AppendLine();
        }

        // TRANSFORM
        if (infoToShow.HasFlag(InfoSections.Transform))
        {
            var pos = useWorldSpace ? targetTransform.position : targetTransform.localPosition;
            var rot = useWorldSpace ? targetTransform.rotation.eulerAngles : targetTransform.localEulerAngles;
            var scl = useWorldSpace ? targetTransform.lossyScale : targetTransform.localScale;

            sb.AppendLine("<b>Transform</b>");
            sb.AppendLine($"• Space: {(useWorldSpace ? "World" : "Local")}");
            sb.AppendLine($"• Position: {fVec(pos)}");
            sb.AppendLine($"• Rotation: {fVec(rot)}");
            sb.AppendLine($"• Scale:   {fVec(scl)}");
            sb.AppendLine();
        }

        // HIERARCHY
        if (infoToShow.HasFlag(InfoSections.Hierarchy))
        {
            var parent = targetTransform.parent ? targetTransform.parent.name : "(none)";
            int childCount = targetTransform.childCount;
            sb.AppendLine("<b>Hierarchy</b>");
            sb.AppendLine($"• Parent: {parent}");
            sb.AppendLine($"• Children: {childCount}");
            sb.AppendLine($"• Path: {GetHierarchyPath(targetTransform)}");
            sb.AppendLine();
        }

        // COMPONENTS
        if (infoToShow.HasFlag(InfoSections.Components))
        {
            var comps = go.GetComponents<Component>();
            sb.AppendLine("<b>Components</b>");

            // Show up to 12, then ellipsis
            const int maxToShow = 12;
            for (int i = 0; i < comps.Length && i < maxToShow; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                string compName = c.GetType().Name;

                // If it's a MonoBehaviour, show enabled state if available
                string enabledStr = "";
                if (c is Behaviour b)
                    enabledStr = b.enabled ? " (enabled)" : " (disabled)";

                sb.AppendLine($"• {compName}{enabledStr}");
            }
            if (comps.Length > maxToShow)
                sb.AppendLine($"• ... (+{comps.Length - maxToShow} more)");

            sb.AppendLine();
        }

        // BOUNDS (from Renderers / Colliders)
        if (infoToShow.HasFlag(InfoSections.Bounds))
        {
            bool anyBounds = TryGetWorldAABB(go, out Bounds worldBounds);
            sb.AppendLine("<b>Bounds</b>");
            if (anyBounds)
            {
                sb.AppendLine($"• Center: {fVec(worldBounds.center)}");
                sb.AppendLine($"• Size:   {fVec(worldBounds.size)}");
                sb.AppendLine($"• Extents:{fVec(worldBounds.extents)}");
            }
            else
            {
                sb.AppendLine("• (no Renderer/Collider bounds found)");
            }
            sb.AppendLine();
        }

        text.text = sb.ToString().TrimEnd();
    }

    private static string GetHierarchyPath(Transform t)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        var cur = t;
        while (cur != null)
        {
            stack.Push(cur.name);
            cur = cur.parent;
        }
        return string.Join("/", stack.ToArray());
    }

    /// <summary>
    /// Computes a world-space AABB from all Renderers and Colliders on the GameObject and its children.
    /// </summary>
    private static bool TryGetWorldAABB(GameObject root, out Bounds combined)
    {
        combined = new Bounds();
        bool hasAny = false;

        // Renderers
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in renderers)
        {
            if (!hasAny)
            {
                combined = r.bounds;
                hasAny = true;
            }
            else
            {
                combined.Encapsulate(r.bounds);
            }
        }

        // Colliders
        var colliders = root.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var c in colliders)
        {
            if (!hasAny)
            {
                combined = c.bounds;
                hasAny = true;
            }
            else
            {
                combined.Encapsulate(c.bounds);
            }
        }

        return hasAny;
    }
}
