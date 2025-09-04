using System.Collections;
using UnityEngine;

public class TranscriptionUI : MonoBehaviour
{
    public GameObject Transcription;
    public float secondsUntillHidden;



    public void HideCaller()
    {
        StartCoroutine(Hide());
    }

    public IEnumerator Hide()
    {
        yield return new WaitForSeconds(secondsUntillHidden);
        Transcription.SetActive(false);
    }


}
