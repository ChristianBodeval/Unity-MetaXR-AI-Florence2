using System.Collections.Generic;
using OpenAI;
using OpenAI.Audio;
using UnityEngine;
using NaughtyAttributes;
// Wit/Voice SDK
using Oculus.Voice;                 // AppVoiceExperience
using Meta.WitAi.Data;              // AudioBuffer
using Meta.WitAi.Lib;               // Mic

public class OpenAITranscriber : MonoBehaviour
{
    public static OpenAITranscriber Instance;

    [Header("Hook (optional)")]
    [SerializeField] private AppVoiceExperience voice; // assign in Inspector or auto-find


    private void Awake()
    {
        Instance = this;
    }
    /*
    private void OnEnable()
    {
        if (!voice) voice = FindAnyObjectByType<AppVoiceExperience>();
        if (voice)
        {
            voice.VoiceEvents.OnStoppedListeningDueToDeactivation.AddListener(OnStoppedListening);
            voice.VoiceEvents.OnStoppedListeningDueToTimeout.AddListener(OnStoppedListening);
            voice.VoiceEvents.OnStoppedListeningDueToInactivity.AddListener(OnStoppedListening);
        }

    }

    private void OnDisable()
    {
        if (voice)
        {
            voice.VoiceEvents.OnStoppedListeningDueToDeactivation.RemoveListener(OnStoppedListening);
            voice.VoiceEvents.OnStoppedListeningDueToTimeout.RemoveListener(OnStoppedListening);
            voice.VoiceEvents.OnStoppedListeningDueToInactivity.RemoveListener(OnStoppedListening);
        }

    }*/

    public Mic mic;
    private void OnStoppedListening()
    {
        if (mic != null && mic.Clip != null)
        {
            var clip = mic.Clip;
            if (clip != null)
            {
                GenerateText(clip);
                return;
            }
        }
    }

    [Button]
    public async void GenerateText(AudioClip audioClip)
    {
        var api = new OpenAIClient();
        var request = new AudioTranscriptionRequest(audioClip, language: "en");
        var result = await api.AudioEndpoint.CreateTranscriptionTextAsync(request);
        Debug.Log("OpenAI: " + result);
    }

}
