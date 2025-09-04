using JetBrains.Annotations;
using Meta.WitAi.Dictation;
using Oculus.Voice;
using PresentFutures.XRAI.Florence;
using TMPro;
using UnityEngine;

public class VoiceActionHandler : MonoBehaviour
{
    public string transcription;
    
    //TODO Add later
    //public VoiceInputController voiceInputController;
    AppVoiceExperience appVoiceExperience;
    public TMP_Text text;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    private void OnEnable()
    {
        if (!appVoiceExperience) return;

        // Listen for simulated/editor and mic input equally
        appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFinalTranscription);
    }

    private void OnDisable()
    {
        if (!appVoiceExperience) return;
        appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFinalTranscription);
    }

    public void OnFinalTranscription(string transcription)
    {
        this.transcription = transcription;
        Debug.Log($"[EditorTest] Final Transcription: {transcription}");
        // If you typed "This is the transcription:" in inspector ? this will log it
    }


    public void SetTranscription(string transcription)
    {
        this.transcription = transcription;
        Debug.Log($"[EditorTest] Final Transcription: {transcription}");
        // If you typed "This is the transcription:" in inspector ? this will log it
    }

    void Start()
    {
        appVoiceExperience = GetComponent<AppVoiceExperience>();
    }

    void Update()
    {


        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            Debug.Log("Right Trigger Pressed - Activating Voice Experience!");
            // Start listening
            appVoiceExperience.Activate();
        }

        else if (OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            Debug.Log("Right Trigger Pressed - Activating Voice Experience!");
            // Start listening
            appVoiceExperience.Deactivate();
        }

    }

    public Florence2Controller florence2Controller;
    public TMP_Text voicePrompt;
    public MultiRequestTranscription multiRequestTranscription;
    public void HandleAction(string[] stringArray)
    {
        text.text = "HandleAction";
        Debug.LogWarning("Calling Handle Action");
        if (stringArray[0] == "add note")
        {
            text.text = "Adding a note to " + stringArray[1] + "Transcript: " + "//TODO ADD HERE";
            Debug.LogWarning("Adding a note to" + stringArray[1] + "Transcript: " + "//TODO ADD HERE" );
            SpatialAnchorFinder.Instance.MakeAnchorsPresenceAwareByLabelName(stringArray[1]);
        }
        else if (stringArray[0] == "Find object") { Debug.LogFormat("ok"); }



        else if (stringArray[0] == "track")
        {
            florence2Controller.task = Florence2Task.OpenVocabularyDetection;
            florence2Controller.textPrompt = multiRequestTranscription.currentTranscription;

            florence2Controller.SendRequest();

            Debug.Log("Current trans: " + transcription);

            Debug.Log("This is the trans: " + transcription);
            //TODO Add send response later
            //voiceInputController.newHandleFullTranscription(transcription);
            //StartCoroutine(voiceInputController.newHandleFullTranscription(appVoiceExperience));
        }

    }
}
