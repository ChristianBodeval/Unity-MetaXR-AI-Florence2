// WitUtteranceTemplateUploader.cs
// Define a template like: "add note to {object}" and push utterances programmatically.
// Robust token resolution + cleaner errors.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class WitUtteranceTemplateUploader : MonoBehaviour
{
    [Header("Wit.ai")]
    [Tooltip("Preferred: set via PlayerPrefs/ENV/TextAsset/Resources. For quick tests you can paste here.")]
    [SerializeField] private string witServerToken = ""; // Inspector (dev only)

    [Tooltip("Wit API version parameter")]
    public string apiVersion = "20240501";

    [Header("Intent")]
    [Tooltip("Intent name for the utterances, e.g., addNoteToObject")]
    public string intentName = "addNoteToObject";

    [Header("Entities")]
    [Tooltip("Action entity name")]
    public string actionEntity = "action";

    [Tooltip("Resolved value text to tag in the sentence, e.g., \"add note\"")]
    public string actionResolvedValue = "add note";

    [Tooltip("Object entity name")]
    public string objectEntity = "object";

    [Header("Template")]
    [Tooltip("Use a placeholder token in the sentence, e.g., \"add note to {object}\"")]
    public string utteranceTemplate = "add note to {object}";

    [Tooltip("The exact placeholder token to replace, e.g., {object}")]
    public string placeholderToken = "{object}";

    [Header("Auth Sources (fallback order)")]
    public bool useInspectorField = true;
    public bool usePlayerPrefs = true;
    public string playerPrefsKey = "WIT_SERVER_TOKEN";
    public bool useEnvironmentVar = true;
    public string envVarName = "WIT_SERVER_TOKEN";
    public bool useTextAsset = false;
    public TextAsset tokenTextAsset;
    public bool useResources = false;
    public string resourcesPath = "wit_server_token"; // Resources/wit_server_token.txt

    [Header("Options")]
    public bool lowercaseObject = false;
    public bool requireSpanMatches = false;
    [Tooltip("(Debug) Use query param auth instead of Authorization header")]
    public bool useQueryParamAuth = false;
    [Tooltip("(Debug) Log where token was loaded from, and mask it")]
    public bool debugAuth = false;

    private const string API_BASE = "https://api.wit.ai";

    // ---------------- Public API ----------------

    /// <summary>Upload a single utterance by replacing the placeholder with objectValue.</summary>
    public void UploadOne(string objectValue)
    {
        var u = BuildUtterance(objectValue);
        if (u != null) StartCoroutine(AddUtterances(new[] { u }));
    }

    /// <summary>Upload many utterances (one for each objectValue).</summary>
    public void UploadMany(IEnumerable<string> objectValues)
    {
        var list = new List<Utterance>();
        foreach (var v in objectValues.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var u = BuildUtterance(v);
            if (u != null) list.Add(u);
        }
        if (list.Count > 0) StartCoroutine(AddUtterances(list.ToArray()));
    }

    /// <summary>Optionally set/override the token at runtime (e.g., from a settings menu).</summary>
    public void SetServerToken(string token)
    {
        witServerToken = token ?? "";
        if (debugAuth) Debug.Log("[WitUploader] Server token set via SetServerToken(...)");
    }

    // ---------------- Builders ----------------

    private Utterance BuildUtterance(string objectValueRaw)
    {
        if (string.IsNullOrWhiteSpace(utteranceTemplate) || string.IsNullOrWhiteSpace(placeholderToken))
        {
            Debug.LogError("[WitUploader] Template or placeholder is empty.");
            return null;
        }

        var obj = (objectValueRaw ?? "").Trim();
        if (lowercaseObject) obj = obj.ToLowerInvariant();

        var text = utteranceTemplate.Replace(placeholderToken, obj);

        // Find spans (ordinal search for determinism)
        int actionStart = IndexOfOrdinal(text, actionResolvedValue);
        int objectStart = LastIndexOfOrdinal(text, obj);

        var ents = new List<EntityAnnotation>();

        if (!string.IsNullOrEmpty(actionResolvedValue) && actionStart >= 0)
        {
            ents.Add(new EntityAnnotation
            {
                entity = actionEntity,
                start = actionStart,
                end = actionStart + actionResolvedValue.Length,
                value = actionResolvedValue
            });
        }
        else
        {
            var msg = $"[WitUploader] Action span not found in: \"{text}\" (needle: \"{actionResolvedValue}\")";
            if (requireSpanMatches) { Debug.LogWarning(msg + " -> skipping"); return null; }
            Debug.LogWarning(msg);
        }

        if (!string.IsNullOrEmpty(obj) && objectStart >= 0)
        {
            ents.Add(new EntityAnnotation
            {
                entity = objectEntity,
                start = objectStart,
                end = objectStart + obj.Length,
                value = obj
            });
        }
        else
        {
            var msg = $"[WitUploader] Object span not found in: \"{text}\" (needle: \"{obj}\")";
            if (requireSpanMatches) { Debug.LogWarning(msg + " -> skipping"); return null; }
            Debug.LogWarning(msg);
        }

        return new Utterance
        {
            text = text,
            intent = intentName,
            entities = ents.ToArray()
        };
    }

    // ---------------- HTTP ----------------

    private IEnumerator AddUtterances(Utterance[] utterances)
    {
        if (utterances == null || utterances.Length == 0) yield break;

        if (!TryResolveServerToken(out var token, out var sourceMsg))
        {
            Debug.LogError("[WitUploader] Could not resolve a valid Server Access Token. " +
                           "Check Inspector/PlayerPrefs/ENV/TextAsset/Resources settings.");
            yield break;
        }

        var url = $"{API_BASE}/utterances?v={apiVersion}";
        if (useQueryParamAuth)
            url += $"&access_token={UnityWebRequest.EscapeURL(token)}";

        // Unity's JsonUtility can't serialize top-level arrays: wrap -> serialize -> strip wrapper
        var wrapper = new UtteranceArrayWrapper { items = utterances.ToList() };
        var jsonWrapped = JsonUtility.ToJson(wrapper);
        var json = ExtractArrayJson(jsonWrapped, "items");

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!useQueryParamAuth)
            req.SetRequestHeader("Authorization", $"Bearer {token}");

        if (debugAuth)
        {
            var masked = Mask(token);
            Debug.Log($"[WitUploader] Using token from {sourceMsg}. Masked: {masked}. Auth via {(useQueryParamAuth ? "query param" : "header")}. URL: {url}");
        }

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[WitUploader] POST /utterances error: {req.responseCode} - {req.downloadHandler.text}");
        }
        else
        {
            Debug.Log($"[WitUploader] Uploaded {utterances.Length} utterance(s).");
        }
    }

    // ---------------- Token Resolution ----------------

    private bool TryResolveServerToken(out string token, out string sourceMsg)
    {
        token = null;
        sourceMsg = "none";

        // 1) Inspector
        if (useInspectorField && !string.IsNullOrWhiteSpace(witServerToken))
        {
            token = SanitizeToken(witServerToken);
            sourceMsg = "Inspector";
        }

        // 2) PlayerPrefs
        if (string.IsNullOrEmpty(token) && usePlayerPrefs && !string.IsNullOrEmpty(playerPrefsKey))
        {
            var t = PlayerPrefs.GetString(playerPrefsKey, null);
            if (!string.IsNullOrWhiteSpace(t))
            {
                token = SanitizeToken(t);
                sourceMsg = $"PlayerPrefs({playerPrefsKey})";
            }
        }

        // 3) ENV var
        if (string.IsNullOrEmpty(token) && useEnvironmentVar && !string.IsNullOrEmpty(envVarName))
        {
            var t = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(t))
            {
                token = SanitizeToken(t);
                sourceMsg = $"ENV({envVarName})";
            }
        }

        // 4) TextAsset
        if (string.IsNullOrEmpty(token) && useTextAsset && tokenTextAsset != null)
        {
            var t = tokenTextAsset.text;
            if (!string.IsNullOrWhiteSpace(t))
            {
                token = SanitizeToken(t);
                sourceMsg = "TextAsset";
            }
        }

        // 5) Resources
        if (string.IsNullOrEmpty(token) && useResources && !string.IsNullOrEmpty(resourcesPath))
        {
            var ta = Resources.Load<TextAsset>(resourcesPath);
            if (ta != null && !string.IsNullOrWhiteSpace(ta.text))
            {
                token = SanitizeToken(ta.text);
                sourceMsg = $"Resources({resourcesPath})";
            }
        }

        // Final validation: Wit tokens vary; avoid overly strict checks.
        if (!string.IsNullOrWhiteSpace(token) && token.Length >= 10)
            return true;

        token = null;
        return false;
    }

    private static string SanitizeToken(string raw)
    {
        if (raw == null) return null;
        var t = raw.Trim();
        // strip common accidental quotes/newlines
        t = t.Trim('\r', '\n', '\t', ' ');
        if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\""))
            t = t.Substring(1, t.Length - 2);
        return t;
    }

    private static string Mask(string t)
    {
        if (string.IsNullOrEmpty(t)) return "(empty)";
        var head = t.Length >= 6 ? t.Substring(0, 6) : t;
        var tail = t.Length >= 4 ? t.Substring(t.Length - 4) : "";
        return $"{head}...{tail}";
    }

    // ---------------- Helpers ----------------

    private static int IndexOfOrdinal(string s, string sub)
        => (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(sub)) ? -1
           : s.IndexOf(sub, StringComparison.Ordinal);

    private static int LastIndexOfOrdinal(string s, string sub)
        => (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(sub)) ? -1
           : s.LastIndexOf(sub, StringComparison.Ordinal);

    // {"items":[...]} -> [...]
    private static string ExtractArrayJson(string jsonWithKey, string key)
    {
        if (string.IsNullOrEmpty(jsonWithKey)) return "[]";
        var needle = $"\"{key}\":";
        int i = jsonWithKey.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0) return "[]";
        int start = jsonWithKey.IndexOf('[', i + needle.Length);
        int end = jsonWithKey.LastIndexOf(']');
        if (start < 0 || end < 0 || end < start) return "[]";
        return jsonWithKey.Substring(start, end - start + 1);
    }

    // ---------------- DTOs ----------------

    [Serializable]
    public class EntityAnnotation
    {
        public string entity;  // e.g., "action", "object"
        public int start;
        public int end;
        public string value;   // resolved value
    }

    [Serializable]
    public class Utterance
    {
        public string text;          // "add note to keyboard"
        public string intent;        // "addNoteToObject"
        public EntityAnnotation[] entities;
    }

    [Serializable]
    private class UtteranceArrayWrapper
    {
        public List<Utterance> items;
    }
}
