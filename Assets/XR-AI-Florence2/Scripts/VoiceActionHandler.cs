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
using System.Collections.Generic;
public class VoiceActionHandler : MonoBehaviour
{
    [Header("References")]
    public AppVoiceExperience appVoiceExperience;
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

    public List<OVRSpatialAnchor> lastFoundAnchors = new List<OVRSpatialAnchor>();

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
            if(lastFoundAnchors.Count > 0) SpatialAnchorFinder.Instance.MakeAnchorsPresenceAwareByLabelName(lastFoundAnchors, false);
            
            List<OVRSpatialAnchor> anchors = SpatialAnchorFinder.Instance.GetAnchorsBySpatialLabelName(stringArray[1]);

            foreach (OVRSpatialAnchor anchor in anchors)
            {
                SpatialLabel spatialAnchor = anchor.GetComponent<SpatialLabel>();
                if (spatialAnchor.isHidden) spatialAnchor.Hide(false);
            }

            lastFoundAnchors = anchors;
            SpatialAnchorFinder.Instance.MakeAnchorsPresenceAwareByLabelName(anchors, true);
        }

        else if (stringArray[0] == "Find" || stringArray[0] == "find" && stringArray[1] == String.Empty)
        {
            UICommandThrown.Play("No object named: " + transcription.Replace("Find ", "").Replace("find ", ""));
            return;
        }

        else if (stringArray[0] == "Scan" || stringArray[0] == "scan")
        {
            florence2Controller.task = Florence2Task.CaptionToPhraseGrounding;
            transcription.Replace("Scan", "").Replace("scan", "").Replace("the", "").Replace("a", "");
            florence2Controller.textPrompt = transcription;

            florence2Controller.SendRequest();

            Debug.Log("Current trans: " + transcription);

            Debug.Log("This is the trans: " + transcription);
            //TODO Add send response later
            //voiceInputController.newHandleFullTranscription(transcription);
            //StartCoroutine(voiceInputController.newHandleFullTranscription(appVoiceExperience));
        }

        /*
        else if (stringArray[0] == "Scan" || stringArray[0] == "scan" && stringArray[1] == String.Empty)
        {
            florence2Controller.task = Florence2Task.CaptionToPhraseGrounding;
            florence2Controller.textPrompt = transcription;
            florence2Controller.SendRequest();
            //TODO Add send response later
            //voiceInputController.newHandleFullTranscription(transcription);
            //StartCoroutine(voiceInputController.newHandleFullTranscription(appVoiceExperience));
        }*/


        //TODO Make this with a 3. variable with the name
        /*
        else if (stringArray[0] == "Change name" || stringArray[0] == "change name" && stringArray[1] != String.Empty)
        {
            OVRSpatialAnchor anchor = SpatialAnchorFinder.Instance.GetAnchorsBySpatialLabelNameFirstFound(stringArray[1]);
            string transcriptionCopy = transcription;
            string[] parts = transcriptionCopy.Split(new string[] { " to " }, StringSplitOptions.None);

            if (parts.Length == 2)
            {
                string prompt = parts[0].Replace("Change name of", "").Trim();
                string newName = parts[1].Trim();

                Debug.Log($"Old: {prompt}, New: {newName}");
                anchor.GetComponent<SpatialLabel>().ObjectName = newName;
            }
        }*/

        else if (stringArray[0] == "Rename" || stringArray[0] == "rename" /*&& stringArray[1] == String.Empty*/)
        {
            OVRSpatialAnchor anchor = XRInputManager.Instance.currentlySelectedAnchor;
            string transcriptionCopy = transcription;
            string[] parts = transcriptionCopy.Split(new string[] { " to " }, StringSplitOptions.None);

            if (parts.Length == 2)
            {
                string prompt = parts[0].Replace("Rename the", "").Trim();
                string newName = parts[1].Trim();

                Debug.Log($"Old: {prompt}, New: {newName}");
                anchor.GetComponent<SpatialLabel>().ObjectName = newName;
            }
        }

        //TODO Handle already existing
        /*
        else if (stringArray[0] == "Rename" || stringArray[0] == "rename" && stringArray[1] != String.Empty) {
            UICommandThrown.Play("'" + stringArray[1] + "'" + " is aldready defined");
            return;
        }*/


        else if (stringArray[0] == "Delete" || stringArray[0] == "delete" && stringArray[1] == String.Empty)
        {
            OVRSpatialAnchor anchor = XRInputManager.Instance.currentlySelectedAnchor;
            SpatialLabel spatialLabel = anchor.GetComponent<SpatialLabel>();
            UICommandThrown.Play("Delete : " + "'" + spatialLabel.ObjectName + "'");
            spatialLabel.Remove();
            return;
        }


        // TODO - Make 
        /*
        else if (stringArray[0] == "Hide" || stringArray[0] == "hide")
        {
            OVRSpatialAnchor anchor = XRInputManager.Instance.currentlySelectedAnchor;
            SpatialAnchorManager.Instance.HideAnchor(anchor,true);
        }*/

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

        else if (stringArray[0] == String.Empty)
        {
            UICommandThrown.Play("'" + transcription + "'" + "\n" + " is not a command");
            return;
        }

        UICommandThrown.Play(voicePromptWithFormat.text);

    }



}
