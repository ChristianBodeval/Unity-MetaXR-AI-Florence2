using JetBrains.Annotations;
using Meta.WitAi.Dictation;
using Oculus.Voice;
using PresentFutures.XRAI.Florence;
using PresentFutures.XRAI.Spatial;
using System;
using TMPro;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.UI;
public class VoiceActionHandler : MonoBehaviour
{
    [Header("References")]
    public AppVoiceExperience appVoiceExperience;
    public MultiRequestTranscription multiRequestTranscription;
    public TranscriptionUI transcriptionUIScript;
    public Florence2Controller florence2Controller;
    public TextFadeOut UICommandThrown;
    public TMP_Text florenceSettingText;
    public TMP_Text voicePromptWithFormat;
    public RectTransform voicePromptRoot;

    public LayoutElement voicePromptLayoutElement;
    [SerializeField] private WitEntityTextHighlighter textHighlighter;

    [Header("Runtime Values")]
    public OVRSpatialAnchor currentAnchor;
    public string transcription;



    void Start()
    {
        appVoiceExperience = GetComponent<AppVoiceExperience>();
    }

    private void OnEnable()
    {
        if (!appVoiceExperience) return;

        // Listen for simulated/editor and mic input equally
        appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFinalTranscription);
        appVoiceExperience.VoiceEvents.OnMicStoppedListening.AddListener(OnMicStoppedListening);
        appVoiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
    }

    private void OnDisable()
    {
        if (!appVoiceExperience) return;
        appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFinalTranscription);
    }


    public void OnMicStoppedListening()
    {
        DeactivateVoiceCommand();
    }

    public void OnFinalTranscription(string transcription)
    {
        Debug.Log("On final");
        this.transcription = transcription;
        textHighlighter.HighlightText();
        //UICommandThrown.Play(transcription);
    }


    public void OnPartialTranscription(string transcription)
    {
        voicePromptWithFormat.text = transcription;
        if (transcription != String.Empty) voicePromptLayoutElement.ignoreLayout = false;


        LayoutRebuilder.ForceRebuildLayoutImmediate(voicePromptWithFormat.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(voicePromptRoot);
    }



    [Button]
    public void ActivateVoiceCommand()
    {
        appVoiceExperience.Activate();   // ✅ no assignment
        voicePromptWithFormat.text = "";
        voicePromptLayoutElement.ignoreLayout = true;
        transcriptionUIScript.IsHidden = false;
    }

    public void DeactivateVoiceCommand()
    {
        transcriptionUIScript.IsHidden = true;
        appVoiceExperience.Deactivate();   // ✅ no assignment
    }

    public void HandleAction(string[] stringArray)
    {
        florenceSettingText.text = "HandleAction";
        Debug.LogWarning("Calling Handle Action");
        if (stringArray[0] == "add note")
        {
            florenceSettingText.text = "Adding a note to " + stringArray[1] + "Transcript: " + "//TODO ADD HERE";
            Debug.LogWarning("Adding a note to" + stringArray[1] + "Transcript: " + "//TODO ADD HERE" );
        }

        else if (stringArray[0] == "Find" || stringArray[0] == "find") {
            SpatialAnchorFinder.Instance.MakeAnchorsPresenceAwareByLabelName(stringArray[1]);
        }



        else if (stringArray[0] == "track" || stringArray[0] == "Track")
        {
            florence2Controller.task = Florence2Task.CaptionToPhraseGrounding;
            multiRequestTranscription.currentTranscription.Replace("Track", "").Replace("track", "");
            florence2Controller.textPrompt = multiRequestTranscription.currentTranscription;

            florence2Controller.SendRequest();

            Debug.Log("Current trans: " + transcription);

            Debug.Log("This is the trans: " + transcription);
            //TODO Add send response later
            //voiceInputController.newHandleFullTranscription(transcription);
            //StartCoroutine(voiceInputController.newHandleFullTranscription(appVoiceExperience));
        }

        

        else if (stringArray[0] == "Change name" || stringArray[0] == "change name")
        {
            OVRSpatialAnchor anchor = XRInputManager.Instance.currentlySelectedAnchor;
            

            string transcriptionCopy = multiRequestTranscription.currentTranscription;

            //anchor.GetComponent<SpatialLabel>().ObjectName = "NEWWWWWW NAME";
            
            // Split on " to "
            string[] parts = transcriptionCopy.Split(new string[] { " to " }, StringSplitOptions.None);

            if (parts.Length == 2)
            {
                string prompt = parts[0].Replace("Change name of", "").Trim();
                string newName = parts[1].Trim();

                Debug.Log($"Old: {prompt}, New: {newName}");
                anchor.GetComponent<SpatialLabel>().ObjectName = newName;
            }
        }


        else if (stringArray[0] == "Hide objects" || stringArray[0] == "hide objects")
        {
            OVRSpatialAnchor anchor = XRInputManager.Instance.currentlySelectedAnchor;


            SpatialAnchorManager.Instance.HideAllAnchors(true);
        }

        else if (stringArray[0] == "Show objects" || stringArray[0] == "show objects")
        {
            OVRSpatialAnchor anchor = XRInputManager.Instance.currentlySelectedAnchor;


            SpatialAnchorManager.Instance.HideAllAnchors(false);
        }


        else
        {
            UICommandThrown.Play("'" + voicePromptWithFormat.text + "'" + "\n" + " was not recognized");

            return;
        }
        UICommandThrown.Play(voicePromptWithFormat.text);

    }





    public void SetTranscription(string transcription)
    {
        this.transcription = transcription;
    }
}
