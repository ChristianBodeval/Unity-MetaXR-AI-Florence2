using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using NaughtyAttributes;

[ExecuteAlways]
public class IOTLampDimmer : MonoBehaviour
{
    [Header("💡 Shelly Configuration")]
    [InfoBox("Enter your Shelly Duo IP address (e.g., 192.168.1.50)")]
    [SerializeField] private string shellyIP = "192.168.1.50";

    [Header("💡 Brightness Control")]
    [Range(1, 100)]
    [OnValueChanged("OnBrightnessChanged")]
    public int brightness = 50;

    [Header("🎛️ Status")]
    [ReadOnly] public bool isOn = false;

    [Header("🎨 UI Reference (Optional)")]
    [Tooltip("Optional reference to a circular slider UI to sync with brightness.")]
    public CircularDimmerUI circularUI; // custom script we’ll define next

    private string BaseUrl => $"http://{shellyIP}/light/0";

    // -------------------------------
    // 🔘 ON / OFF CONTROLS
    // -------------------------------

    [Button("Turn On")]
    public void TurnOn()
    {
        isOn = true;
        StartCoroutine(SendShellyCommand("?turn=on"));
    }

    [Button("Turn Off")]
    public void TurnOff()
    {
        isOn = false;
        StartCoroutine(SendShellyCommand("?turn=off"));
    }

    // -------------------------------
    // 💡 BRIGHTNESS CONTROL
    // -------------------------------

    [Button("Apply Brightness")]
    public void ApplyBrightness()
    {
        StartCoroutine(SendShellyCommand($"?brightness={brightness}"));
    }

    private void OnBrightnessChanged()
    {
        if (Application.isPlaying)
        {
            ApplyBrightness();
            if (circularUI)
                circularUI.SetValue(brightness / 100f);
        }
    }

    // -------------------------------
    // 🌐 HTTP REQUEST
    // -------------------------------

    private IEnumerator SendShellyCommand(string command)
    {
        if (string.IsNullOrEmpty(shellyIP))
        {
            Debug.LogWarning("Shelly IP not set.");
            yield break;
        }

        string url = $"{BaseUrl}{command}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError($"Shelly request failed: {req.error}");
            else
                Debug.Log($"Shelly command sent successfully: {url}");
        }
    }

    // -------------------------------
    // 🔁 Optional Sync
    // -------------------------------

    private void Update()
    {
        if (circularUI && !Application.isPlaying)
            circularUI.SetValue(brightness / 100f);
    }
}
