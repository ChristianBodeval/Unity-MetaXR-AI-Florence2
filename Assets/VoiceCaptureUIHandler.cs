using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using Oculus.Voice;
using TMPro;
using UnityEngine;

public class VoiceCaptureUIHandler : MonoBehaviour
{
    
    [SerializeField] private AppVoiceExperience voice;   // drag your AppVoiceExperience here
    [SerializeField] private TMP_Text partialText;       // optional UI for live text
    [SerializeField] private TMP_Text finalText;         // optional UI for final text
    [SerializeField] private TextFadeOut UICommandThrown;
    [SerializeField] private WitEntityTextHighlighter textHighlighter;


    public string LastPartial { get; private set; }
    public string LastFinal { get; private set; }

    private void Reset()
    {
        // Auto-find in the same GameObject or scene
        if (!voice) voice = GetComponentInChildren<AppVoiceExperience>() ?? FindObjectOfType<AppVoiceExperience>();
    }

    private void OnEnable()
    {
        if (!voice) return;

        // microphone level (optional, for VU meters)
        voice.VoiceEvents.OnMicLevelChanged.AddListener(OnMicLevel);

        // transcription events you care about:
        voice.VoiceEvents.OnPartialTranscription.AddListener(OnPartial);
        voice.VoiceEvents.OnFullTranscription.AddListener(OnFinal);

        // full NLP response (if you also want intents/entities)
        voice.VoiceEvents.OnResponse.AddListener(OnWitResponse);
        voice.VoiceEvents.OnError.AddListener((code, msg) => Debug.LogError($"Wit Error {code}: {msg}"));
    }

    private void OnDisable()
    {
        if (!voice) return;
        voice.VoiceEvents.OnMicLevelChanged.RemoveListener(OnMicLevel);
        voice.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartial);
        voice.VoiceEvents.OnFullTranscription.RemoveListener(OnFinal);
        voice.VoiceEvents.OnResponse.RemoveListener(OnWitResponse);
    }

    // --- Transcription callbacks ---
    private void OnPartial(string text)
    {
        LastPartial = text;
        if (partialText) partialText.text = text;
        // Debug.Log($"Partial: {text}");
    }

    private void OnFinal(string text)
    {
        LastFinal = text;
        if (finalText) finalText.text = text;
        
        textHighlighter.HighlightText();
        // Debug.Log($"Final: {text}");
    }

    // --- Optional: trigger listening from UI ---
    // --- Optional: inspect NLP result (intents/entities) ---
    private void OnWitResponse(WitResponseNode response)
    {
        // Example: grab top intent & value of an entity named "name"
        var intent = response.GetFirstIntent();
        var entityValue = response.GetFirstEntityValue("name");
        // Debug.Log($"Intent: {intent?.name} ({intent?.confidence}), name={entityValue}");
    }

    private void OnMicLevel(float level)
    {
        // Implement mic volume UI if you want (e.g., scale a bar)
    }
}
