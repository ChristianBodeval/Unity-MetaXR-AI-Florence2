using System.Threading.Tasks;
using System.Collections;
using OpenAI;
using OpenAI.Audio;
using System;
using UnityEngine;

public class TTSOpenAI
{
    private static OpenAIClient _openAIClient;

    private static OpenAIClient GetOpenAIClient()
    {
        if (_openAIClient != null)
        {
            return _openAIClient;
        }
        return new OpenAIClient(Resources.Load<OpenAIConfiguration>("OpenAIConfiguration"));
    }

    public static IEnumerator ExecuteCoroutine(string text, string model, string voice, string instructions, Action<AudioClip> onComplete)
    {
        AudioClip result = null;

        SpeechRequest request = new(text, model, voice, instructions);
        Task<SpeechClip> speechClip = GetOpenAIClient().AudioEndpoint.GetSpeechAsync(request);

        while (!speechClip.IsCompleted) yield return null;

        if (speechClip.Exception != null)
        {
            Debug.LogError($"Error in TTSOpenAI.ExecuteCoroutine: {speechClip.Exception.Message}");
        }
        else
        {
            result = speechClip.Result.AudioClip;
        }

        onComplete?.Invoke(result);
    }
}
