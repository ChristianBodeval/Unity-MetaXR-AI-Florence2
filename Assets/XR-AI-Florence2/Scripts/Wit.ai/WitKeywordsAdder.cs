using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class WitKeywordsAdder : MonoBehaviour
{
    [Header("Wit / Wiz")]
    [Tooltip("Your SERVER (Admin) token. For testing only—do NOT ship in builds.")]
    [SerializeField] private string serverAccessToken = "BCMF2A5BZYZNQO2RWTJO7N2CSA3CA5J2";

    [Tooltip("Keywords entity id/name in Wit (e.g., 'object')")]
    [SerializeField] private string entityId = "object";

    [Tooltip("Wit API version (kept for compatibility)")]
    [SerializeField] private string apiVersion = "20200513";

    [Header("Values to add on Start")]
    [SerializeField]
    private KeywordItem[] valuesToAdd =
    {
        new KeywordItem("tv", new[] {"television"}),
        new KeywordItem("keyboard", new[] {"kb"}),
        new KeywordItem("mouse", new[] {"computer mouse"}),
        new KeywordItem("monitor", new[] {"screen", "display"})
    };

    private const string BASE = "https://api.wit.ai";

    private void Start()
    {
        // Add all values when the scene starts
        StartCoroutine(AddValuesBatch(valuesToAdd));
    }

    public IEnumerator AddValuesBatch(KeywordItem[] items)
    {
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.value)) continue;
            yield return StartCoroutine(AddEntityValue(entityId, item.value, item.synonyms));
            // (Optional) rate-limit a bit if you add many:
            // yield return new WaitForSeconds(0.05f);
        }
        Debug.Log("[WitKeywordsAdder] Finished adding keywords.");
    }

    /// POST /entities/{entity_id}/values
    public IEnumerator AddEntityValue(string id, string value, string[] synonyms = null)
    {
        string url = $"{BASE}/entities/{UnityWebRequest.EscapeURL(id)}/values?v={apiVersion}";

        var payload = new EntityValuePayload
        {
            value = value,
            synonyms = (synonyms != null && synonyms.Length > 0) ? synonyms : new[] { value }
        };

        string body = JsonUtility.ToJson(payload);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + serverAccessToken);
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            var code = (int)req.responseCode;
            if (req.result == UnityWebRequest.Result.Success || code == 200 || code == 201)
            {
                Debug.Log($"[WitKeywordsAdder] Added '{value}' to entity '{id}'.");
            }
            else if (code == 409) // already exists
            {
                Debug.Log($"[WitKeywordsAdder] '{value}' already exists in '{id}' (409). Skipping.");
            }
            else
            {
                Debug.LogError($"[WitKeywordsAdder] Failed to add '{value}' → {code} {req.error}\n{req.downloadHandler.text}");
            }
        }
    }

    // -------- Data types --------
    [System.Serializable]
    public class KeywordItem
    {
        public string value;
        public string[] synonyms;
        public KeywordItem(string v, string[] s = null) { value = v; synonyms = s; }
    }

    [System.Serializable]
    private struct EntityValuePayload
    {
        public string value;
        public string[] synonyms;
    }
}
