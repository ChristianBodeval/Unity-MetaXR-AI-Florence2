using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using Meta.WitAi.Dictation;
using JetBrains.Annotations;
using PresentFutures.XRAI.Spatial;

public class ChatGPTSummarizer : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Source text to summarize")]
    public TMP_Text sourceText;

    [Tooltip("Where the summary will be written")]
    public TMP_Text outputText;

    public MultiRequestTranscription transcription;

    [Header("Behavior")]
    [Tooltip("If true, updates outputText as tokens stream in")]
    public bool stream = true;

    [Tooltip("Only summarize when toggle is turned ON (true). If false, summarize on both on/off.")]
    public bool onlyWhenOn = true;

    [Tooltip("Maximum characters sent to the model (rough truncation safeguard)")]
    [Min(200)]
    public int maxChars = 8000;

    [Tooltip("Add a short style hint for the summary")]
    public SummaryStyle style = SummaryStyle.ThreeBullets;

    [Tooltip("Optional: Model to use (default GPT-4o)")]
    public string modelOverride = ""; // leave empty to use Model.GPT4o

    public enum SummaryStyle
    {
        OneSentence,
        ThreeBullets,
        KeyPoints,
        CustomPrompt
    }

    private OpenAIClient _client;

    public SpatialLabel spatialLabel;

    private void Awake()
    {
        _client = new OpenAIClient(); // Uses env/.openai/ScriptableObject per package docs
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HOOKABLE METHODS (Inspector)
    // Wire a Toggle.onValueChanged(bool) → OnToggleChanged(bool)
    public void OnToggleChanged(bool isOn)
    {
        if (!onlyWhenOn || isOn)
            _ = SummarizeAsync();
    }

    // If you *really* have an event that passes a string, wire that here.
    // Example: pass "Summarize" to trigger, anything else ignored.
    public void OnToggleChangedString(string command)
    {
        if (string.Equals(command, "Summarize", StringComparison.OrdinalIgnoreCase))
            _ = SummarizeAsync();
    }

    // Generic button/voice hook: wire any Button.onClick → TriggerSummarize()
    public void TriggerSummarize()
    {
        _ = SummarizeAsync();
    }
    // ─────────────────────────────────────────────────────────────────────────────
    public TMP_Text text;

    private async Task SummarizeAsync()
    {
        if (sourceText == null)
        {
            Debug.LogWarning("[ChatGPTSummarizer] No sourceText assigned.");
            return;
        }

        string input = sourceText.text + "Object name: " + spatialLabel.ObjectName ?? "";
        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.LogWarning("[ChatGPTSummarizer] Source text is empty.");
            return;
        }

        if (input.Length > maxChars)
            input = input.Substring(0, maxChars);

        if (outputText != null)
            outputText.text = stream ? "Updating…" : "Thinking…";


        var instruction = style switch
        {
            SummaryStyle.OneSentence => "Summarize the text in one concise sentence. Keep key facts. The text should only be describing the object",
            SummaryStyle.ThreeBullets => "Summarize the text in exactly three short bullet points.",
            SummaryStyle.KeyPoints => "Summarize the text as bullet points of key insights and outcomes.",
            SummaryStyle.CustomPrompt => text.text,
            _ => "Summarize the text briefly."
        };

        var messages = new List<Message>
        {
            new(Role.System, "You are a helpful assistant that produces short desciption for guests visiting a home for the first time. The text should be very short and related to the object" +
                             "Do not invent facts. Preserve names, numbers, and dates. If the input looks like code or logs, summarize the intent and outcome."),
            new(Role.User, $"{instruction}\n\n---\n{input}\n---")
        };

        try
        {
            var model = string.IsNullOrWhiteSpace(modelOverride) ? Model.GPT4o : new Model(modelOverride);

            if (!stream)
            {
                var req = new ChatRequest(messages, model: model);
                var resp = await _client.ChatEndpoint.GetCompletionAsync(req);
                var text = resp?.FirstChoice?.Message?.ToString() ?? "(no response)";
                if (outputText) outputText.text = text;
                Debug.Log($"[ChatGPTSummarizer] {text}");
            }
            else
            {
                var req = new ChatRequest(messages, model: model);
                string assembled = "";
                var resp = await _client.ChatEndpoint.StreamCompletionAsync(req, async partial =>
                {
                    var delta = partial.FirstChoice?.Delta?.ToString();
                    if (!string.IsNullOrEmpty(delta))
                    {
                        assembled += delta;
                        if (outputText) outputText.text = assembled;
                    }
                    await Task.CompletedTask;
                });

                var finalText = resp?.FirstChoice?.Message?.ToString() ?? assembled;
                if (outputText) outputText.text = string.IsNullOrEmpty(finalText) ? "(no response)" : finalText;
                Debug.Log($"[ChatGPTSummarizer] {finalText}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ChatGPTSummarizer] Error: {e.Message}\n{e}");
            if (outputText) outputText.text = $"Error: {e.Message}";
        }

        Debug.Log("Chat called");
        transcription.UpdateCurrentTranscription(outputText.text);
    }
}
