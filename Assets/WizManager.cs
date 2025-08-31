using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;

public class WizManager : MonoBehaviour
{
    public static WizManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    // ⚠️ Use SERVER token only for local testing. Do not ship it in builds.
    [SerializeField] private string serverToken = "BCMF2A5BZYZNQO2RWTJO7N2CSA3CA5J2";
    [SerializeField] private string entityName = "object"; // the entity with tv/keyboard
    [SerializeField] private string apiVersion = "20240304";
    

    //TODO make it so that each keyword is called in a queue, so it is able to check if there already is objects in the database. Or batch upload more at time in the Florence controller. Or use the Dictionary in the SpatialAnchorFinder script
    public IEnumerator AddKeywordCoroutine(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) yield break;

        // Step 1: Fetch existing keywords
        string getUrl = $"https://api.wit.ai/entities/{UnityWebRequest.EscapeURL(entityName)}?v={apiVersion}";
        using (var getReq = UnityWebRequest.Get(getUrl))
        {
            getReq.SetRequestHeader("Authorization", "Bearer " + serverToken);

            yield return getReq.SendWebRequest();

            if (getReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[WizManager] Failed to fetch entity keywords: {getReq.error}\n{getReq.downloadHandler.text}");
                yield break;
            }

            // Parse response (keywords are in JSON)
            var response = JsonUtility.FromJson<EntityResponse>(getReq.downloadHandler.text);
            if (response != null && response.keywords != null)
            {
                foreach (var k in response.keywords)
                {
                    if (string.Equals(k.keyword, keyword, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning($"[WizManager] Keyword '{keyword}' already exists in '{entityName}', skipping.");
                        yield break;
                    }
                }
            }
        }

        // Step 2: Add keyword if it doesn’t exist
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
                Debug.Log($"[WizManager] Added '{keyword}' to '{entityName}'.");
            }
            else
            {
                Debug.LogError($"[WizManager] Failed ({code}): {req.error}\n{req.downloadHandler.text}");
            }
        }
    }

    [Button]
    public void AddKeywordTest()
    {
        Debug.Log("[WizManager] Starting AddKeyword coroutine…");
        StartCoroutine(AddKeywordCoroutine("lamp"));
    }

    [System.Serializable]
    private class KeywordPayload
    {
        public string keyword;
        public string[] synonyms;
    }

    [System.Serializable]
    private class EntityResponse
    {
        public KeywordEntry[] keywords;
    }

    [System.Serializable]
    private class KeywordEntry
    {
        public string keyword;
        public string[] synonyms;
    }
}
