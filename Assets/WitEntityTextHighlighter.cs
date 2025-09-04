using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using NaughtyAttributes;

[RequireComponent(typeof(TMP_Text))]
public class WitEntityTextHighlighter : MonoBehaviour
{
    [Header("Wit/Wiz API")]
    [Tooltip("SERVER token (use only for local testing, do NOT ship it).")]
    [SerializeField] private string serverToken = "REPLACE_WITH_SERVER_TOKEN";
    [Tooltip("The entity that represents actions in Wit/Wiz (e.g., 'action').")]
    [SerializeField] private string actionEntityName = "action";
    [Tooltip("The entity that represents objects in Wit/Wiz (e.g., 'object').")]
    [SerializeField] private string objectEntityName = "object";
    [SerializeField] private string apiVersion = "20240304";

    [Header("References")]
    [SerializeField] private TMP_Text targetText; // drag in your TMP_Text

    [Header("Colors")]
    [SerializeField] private Color actionsColor = new Color(0.95f, 0.55f, 0.20f); // orange
    [SerializeField] private Color objectsColor = new Color(0.25f, 0.70f, 1f);    // blue

    [Header("Debug Lists (runtime)")]
    [ReadOnly] public List<string> actionsList = new List<string>();
    [ReadOnly] public List<string> objectsList = new List<string>();

    private HashSet<string> _actionsSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    private HashSet<string> _objectsSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

    [Serializable] private class WitEntityResponse { public WitKeyword[] keywords; }
    [Serializable] private class WitKeyword { public string keyword; public string[] synonyms; }

    private void Awake()
    {
        if (!targetText) targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        StartCoroutine(RefreshAllKeywords());
    }

    [Button("Refresh Keywords Now")]
    public void EditorRefreshKeywords()
    {
        StartCoroutine(RefreshAllKeywords());
    }

    private IEnumerator RefreshAllKeywords()
    {
        if (string.IsNullOrWhiteSpace(serverToken))
        {
            Debug.LogWarning("[WitEntityTextHighlighter] Server token missing.");
            yield break;
        }

        yield return FetchEntityKeywords(actionEntityName, _actionsSet, actionsList);
        yield return FetchEntityKeywords(objectEntityName, _objectsSet, objectsList);
    }

    private IEnumerator FetchEntityKeywords(string entityName, HashSet<string> targetSet, List<string> publicList)
    {
        targetSet.Clear();
        publicList.Clear();

        if (string.IsNullOrWhiteSpace(entityName))
            yield break;

        string url = $"https://api.wit.ai/entities/{UnityWebRequest.EscapeURL(entityName)}?v={apiVersion}";
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + serverToken);

            yield return req.SendWebRequest();

            int code = (int)req.responseCode;
            if (req.result == UnityWebRequest.Result.Success && (code == 200 || code == 201))
            {
                try
                {
                    var json = req.downloadHandler.text;
                    var parsed = JsonUtility.FromJson<WitEntityResponse>(json);
                    if (parsed != null && parsed.keywords != null)
                    {
                        foreach (var kw in parsed.keywords)
                        {
                            if (!string.IsNullOrWhiteSpace(kw.keyword))
                                targetSet.Add(kw.keyword.Trim());

                            if (kw.synonyms != null)
                            {
                                foreach (var syn in kw.synonyms)
                                {
                                    if (!string.IsNullOrWhiteSpace(syn))
                                        targetSet.Add(syn.Trim());
                                }
                            }
                        }
                    }

                    // Copy unique values into the public list for inspector display
                    publicList.AddRange(targetSet);

                    Debug.Log($"[WitEntityTextHighlighter] Loaded {targetSet.Count} unique keywords for '{entityName}'.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WitEntityTextHighlighter] Parse error for '{entityName}': {e}");
                }
            }
            else
            {
                Debug.LogError($"[WitEntityTextHighlighter] Fetch failed for '{entityName}' ({code}): {req.error}\n{req.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// Call this method to recolor whatever text is already in targetText.
    /// </summary>

    [Button]
    public void HighlightText()
    {
        if (!targetText) return;
        if (string.IsNullOrEmpty(targetText.text)) return;

        targetText.text = BuildColoredText(targetText.text);
    }

    private string BuildColoredText(string input)
    {
        string result = input;

        string actionHex = ColorUtility.ToHtmlStringRGB(actionsColor);
        string objectHex = ColorUtility.ToHtmlStringRGB(objectsColor);

        var allActions = new List<string>(_actionsSet);
        var allObjects = new List<string>(_objectsSet);
        allActions.Sort((a, b) => b.Length.CompareTo(a.Length));
        allObjects.Sort((a, b) => b.Length.CompareTo(a.Length));

        foreach (var keyword in allActions)
        {
            if (string.IsNullOrEmpty(keyword)) continue;
            string pattern = $@"\b{Regex.Escape(keyword)}\b";
            result = Regex.Replace(result, pattern,
                m => $"<color=#{actionHex}>{m.Value}</color>",
                RegexOptions.IgnoreCase);
        }

        foreach (var keyword in allObjects)
        {
            if (string.IsNullOrEmpty(keyword)) continue;
            string pattern = $@"\b{Regex.Escape(keyword)}\b";
            result = Regex.Replace(result, pattern,
                m => $"<color=#{objectHex}>{m.Value}</color>",
                RegexOptions.IgnoreCase);
        }

        return result;
    }
}
