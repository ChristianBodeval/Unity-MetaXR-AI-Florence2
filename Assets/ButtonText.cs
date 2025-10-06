using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonText : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text label; // or Text if you use legacy UI

    [Header("Texts")]
    [SerializeField] private string textWhenOn = "Enabled";
    [SerializeField] private string textWhenOff = "Disabled";

    private void Awake()
    {
        if (toggle != null)
        {
            // Ensure initial state is applied
            UpdateLabel(toggle.isOn);

            // Subscribe to toggle events
            toggle.onValueChanged.AddListener(UpdateLabel);
        }
    }

    private void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(UpdateLabel);
        }
    }

    private void UpdateLabel(bool isOn)
    {
        if (label != null)
        {
            label.text = isOn ? textWhenOn : textWhenOff;
        }
    }
}
