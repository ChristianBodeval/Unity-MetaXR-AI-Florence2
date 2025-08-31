using UnityEngine;
using Oculus.Voice;
using Meta.WitAi;
using NaughtyAttributes;
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance;
    [Header("References")]
    public AppVoiceExperience appVoice;

    private string latestPartial = string.Empty;
    private string latestFull = string.Empty;


    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        

        // Hook transcription events
        //appVoice.VoiceEvents.OnPartialTranscription.AddListener(OnPartial);
        //appVoice.VoiceEvents.OnFullTranscription.AddListener(OnFull);
    }


    [Button]
    public void ActivateVoiceCommand()
    {
            appVoice.Activate();   // ✅ no assignment
    }
    /*
    void Update()
    {
        // Start listening on button down
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            Debug.Log("[VoiceManager] Hold detected: Activating voice capture.");
            appVoice.Activate();   // ✅ no assignment
        }

        // Stop listening on button up
        if (OVRInput.GetUp(OVRInput.Button.Three))
        {
            Debug.Log("[VoiceManager] Release detected: Deactivating voice capture.");
            appVoice.Deactivate();

            // Choose best transcript
            string finalTranscript = !string.IsNullOrEmpty(latestFull) ? latestFull : latestPartial;

            if (!string.IsNullOrWhiteSpace(finalTranscript))
            {
                Debug.Log("[VoiceManager] Final Transcript: " + finalTranscript);

                // Send as text request
                appVoice.Activate(finalTranscript);
            }

            // Reset
            latestPartial = string.Empty;
            latestFull = string.Empty;
        }

        // While holding, keep logging partial
        if (OVRInput.Get(OVRInput.Button.Three) && !string.IsNullOrEmpty(latestPartial))
        {
            Debug.Log("[VoiceManager] Live Transcription: " + latestPartial);
        }
    }
    */
    private void OnPartial(string transcription)
    {
        latestPartial = transcription;
    }

    private void OnFull(string transcription)
    {
        latestFull = transcription;
    }
}
