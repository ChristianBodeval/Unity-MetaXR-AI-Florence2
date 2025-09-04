using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using NaughtyAttributes;
public class AddKeywordExample : MonoBehaviour
{

    public static AddKeywordExample Instance;

    private void Awake()
    {
        Instance = this;
    }


    // ⚠️ Use SERVER token only for local testing. Do not ship it in builds.
    [SerializeField] private string serverToken = "BCMF2A5BZYZNQO2RWTJO7N2CSA3CA5J2";
    [SerializeField] private string entityName = "object"; // the entity with tv/keyboard
    [SerializeField] private string apiVersion = "20240304";

    public IEnumerator AddKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) yield break;

        string url = $"https://api.wit.ai/entities/{UnityWebRequest.EscapeURL(entityName)}/keywords?v={apiVersion}";
        var payload = new KeywordPayload { keyword = keyword, synonyms = new[] { keyword } };
        string body = JsonUtility.ToJson(payload);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + serverToken);
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            int code = (int)req.responseCode;
            if (req.result == UnityWebRequest.Result.Success || code == 200 || code == 201)
            {
                Debug.Log($"[AddKeywordExample] Added '{keyword}' to '{entityName}'.");
            }
            else
            {
                string responseText = req.downloadHandler.text;

                if (responseText.Contains("already exists"))
                {
                    Debug.LogWarning($"[AddKeywordExample] Keyword '{keyword}' already exists in '{entityName}', skipping.");
                }
                else
                {
                    Debug.LogError($"[AddKeywordExample] Failed ({code}): {req.error}\n{responseText}");
                }
            }
        }
    }

    public string TestStringToAdd;
    [Button]
    public void AddKeywordTest()
    {
        Debug.Log("[AddKeywordExample] Starting AddKeyword coroutine…");
        StartCoroutine(AddKeyword(TestStringToAdd));
    }


    [System.Serializable]
    private class KeywordPayload
    {
        public string keyword;
        public string[] synonyms;
    }
}
