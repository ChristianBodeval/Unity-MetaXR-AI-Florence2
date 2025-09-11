using System.Text;
using TMPro;
using UnityEngine;
using NaughtyAttributes; // for EnumFlags & Buttons

[RequireComponent(typeof(TMP_Text))]
public class SpacialLabelDebugger : MonoBehaviour
{
    [System.Flags]
    public enum InfoSections
    {
        None = 0,
        Name = 1 << 0,
        Transform = 1 << 1,
        Aim = 1 << 2,
        Removal = 1 << 3,
        Border = 1 << 4,
        Anchor = 1 << 5,
        LayerMask = 1 << 6,
        All = ~0
    }

    [Header("Debug Settings")]
    [EnumFlags, OnValueChanged(nameof(UpdateDebugText))]
    [SerializeField] private InfoSections infoToShow = InfoSections.Name | InfoSections.Transform | InfoSections.Aim;

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

    // references
    [SerializeField] private Transform labelTransform;
    [SerializeField] private TMP_Text text;
    [SerializeField] private PresentFutures.XRAI.Spatial.SpatialLabel spatialLabel;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        spatialLabel = GetComponentInParent<PresentFutures.XRAI.Spatial.SpatialLabel>();
        if (spatialLabel) labelTransform = spatialLabel.transform;
    }

    //private void OnValidate() => UpdateDebugText();

    private void Update()
    {
        if (updateEveryFrame) UpdateDebugText();
    }

    private void UpdateDebugText()
    {
        if (!text || !labelTransform) return;

        text.richText = true;
        var sb = new StringBuilder();

        string fVec(Vector3 v) => $"({v.x.ToString($"F{decimals}")}, {v.y.ToString($"F{decimals}")}, {v.z.ToString($"F{decimals}")})";

        if (infoToShow.HasFlag(InfoSections.Name) && spatialLabel != null)
        {
            sb.AppendLine($"<b><size=120%>{spatialLabel.ObjectName}</size></b>");
        }

        if (infoToShow.HasFlag(InfoSections.Transform))
        {
            var pos = useWorldSpace ? labelTransform.position : labelTransform.localPosition;
            var rot = useWorldSpace ? labelTransform.rotation.eulerAngles : labelTransform.localEulerAngles;
            var scl = useWorldSpace ? labelTransform.lossyScale : labelTransform.localScale;

            sb.AppendLine("<b>Transform</b>");
            sb.AppendLine($"• Space: {(useWorldSpace ? "World" : "Local")}");
            sb.AppendLine($"• Position: {fVec(pos)}");
            sb.AppendLine($"• Rotation: {fVec(rot)}");
            sb.AppendLine($"• Scale: {fVec(scl)}");
            sb.AppendLine();
        }

        if (infoToShow.HasFlag(InfoSections.Aim))
        {
            sb.AppendLine("<b>Aim</b>");
            sb.AppendLine($"• Sources: {GetPrivateField("aimFrom")}");
            sb.AppendLine($"• Max Distance: {GetPrivateField("maxAimDistance")}");
            sb.AppendLine();
        }

        if (infoToShow.HasFlag(InfoSections.LayerMask))
        {
            var mask = (LayerMask)GetPrivateField("aimLayerMask");
            sb.AppendLine("<b>Layer Mask</b>");
            sb.AppendLine($"• Aim Layers: {LayerMaskToNames(mask)}");
            sb.AppendLine();
        }

        if (infoToShow.HasFlag(InfoSections.Removal))
        {
            sb.AppendLine("<b>Removal</b>");
            sb.AppendLine($"• Left Btn: {GetPrivateField("removeButtonLeft")}");
            sb.AppendLine($"• Right Btn: {GetPrivateField("removeButtonRight")}");
            sb.AppendLine();
        }

        if (infoToShow.HasFlag(InfoSections.Border) && spatialLabel != null)
        {
            sb.AppendLine("<b>Border</b>");
            sb.AppendLine($"• X Border: {spatialLabel.XBorderValue.ToString($"F{decimals}")}");
            sb.AppendLine($"• Y Border: {spatialLabel.YBorderValue.ToString($"F{decimals}")}");
            sb.AppendLine();
        }

        if (infoToShow.HasFlag(InfoSections.Anchor) && spatialLabel != null)
        {
            sb.AppendLine("<b>Anchor</b>");
            if (spatialLabel.Anchor != null)
            {
                sb.AppendLine($"• UUID: {spatialLabel.Anchor.Uuid}");
            }
            else sb.AppendLine("• (no OVRSpatialAnchor)");
            sb.AppendLine();
        }

        text.text = sb.ToString().TrimEnd();
    }

    private object GetPrivateField(string fieldName)
    {
        if(spatialLabel != null) { return null; };
        var t = spatialLabel.GetType();
        var f = t.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(spatialLabel) : "(n/a)";
    }

    private static string LayerMaskToNames(LayerMask mask)
    {
        if (mask.value == ~0) return "Everything";
        if (mask.value == 0) return "Nothing";

        var sb = new StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            int bit = 1 << i;
            if ((mask.value & bit) != 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(LayerMask.LayerToName(i));
            }
        }
        return sb.Length == 0 ? "(Unnamed Layers)" : sb.ToString();
    }
}
