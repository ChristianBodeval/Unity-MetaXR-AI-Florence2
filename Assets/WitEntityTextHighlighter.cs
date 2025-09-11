using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    [SerializeField] public TMP_Text targetText; // drag in your TMP_Text

    [Header("Colors")]
    [SerializeField] private Color actionsColor = new Color(0.95f, 0.55f, 0.20f); // orange
    [SerializeField] private Color objectsColor = new Color(0.25f, 0.70f, 1f);    // blue

    [Header("Debug Lists (runtime)")]
    [ReadOnly] public List<string> actionsList = new List<string>();
    [ReadOnly] public List<string> objectsList = new List<string>();

    // Case-insensitive sets so "track" == "Track" == "TRACK"
    private readonly HashSet<string> _actionsSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    private readonly HashSet<string> _objectsSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

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
    /// Call this to recolor whatever text is already in targetText.
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
        if (string.IsNullOrEmpty(input)) return input;

        // Precompute color hex
        string actionHex = ColorUtility.ToHtmlStringRGB(actionsColor);
        string objectHex = ColorUtility.ToHtmlStringRGB(objectsColor);

        // Sort by length (desc) to prefer longer matches first
        var allActions = new List<string>(_actionsSet);
        var allObjects = new List<string>(_objectsSet);
        allActions.Sort((a, b) => b.Length.CompareTo(a.Length));
        allObjects.Sort((a, b) => b.Length.CompareTo(a.Length));

        // Build a lookup for quick category detection in evaluator
        var categoryLookup = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var s in allActions) if (!string.IsNullOrWhiteSpace(s)) categoryLookup[s] = "action";
        foreach (var s in allObjects) if (!string.IsNullOrWhiteSpace(s) && !categoryLookup.ContainsKey(s)) categoryLookup[s] = "object";

        // Build a single alternation pattern with word boundaries for all keywords
        // Example: \b(?:turn on|track|play)\b
        string alternation = BuildAlternation(allActions, allObjects);
        if (string.IsNullOrEmpty(alternation)) return input;

        var keywordRegex = new Regex(@"\b(?:" + alternation + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Split into segments by TMP/rich-text tags so we don't recolor inside <...>
        // We keep the tags as-is and only process plain text segments.
        var segments = SplitByTags(input);

        var sb = new StringBuilder(input.Length + 64);
        foreach (var seg in segments)
        {
            if (seg.isTag)
            {
                // pass tags through
                sb.Append(seg.text);
                continue;
            }

            // Replace keywords in this plain-text segment
            string replaced = keywordRegex.Replace(seg.text, m =>
            {
                string val = m.Value; // original casing preserved
                // Determine category using lookup (case-insensitive)
                string cat;
                if (!categoryLookup.TryGetValue(val, out cat))
                {
                    // Fallback: find by scanning (handles different casing)
                    cat = _actionsSet.Contains(val) ? "action" : (_objectsSet.Contains(val) ? "object" : null);
                }

                if (cat == "action")
                    return $"<color=#{actionHex}>{val}</color>";
                else if (cat == "object")
                    return $"<color=#{objectHex}>{val}</color>";
                else
                    return val;
            });

            sb.Append(replaced);
        }

        return sb.ToString();
    }

    // --- Helpers ---

    private static string BuildAlternation(List<string> actions, List<string> objects)
    {
        // Combine, remove empties, sort by length desc to prefer longest first
        var all = new List<string>(actions.Count + objects.Count);
        foreach (var s in actions) if (!string.IsNullOrWhiteSpace(s)) all.Add(s);
        foreach (var s in objects) if (!string.IsNullOrWhiteSpace(s)) all.Add(s);

        if (all.Count == 0) return null;

        all.Sort((a, b) => b.Length.CompareTo(a.Length)); // longest-first

        // Escape each for regex; join with |
        for (int i = 0; i < all.Count; i++)
            all[i] = Regex.Escape(all[i]);

        return string.Join("|", all);
    }

    // Splits a string into (text|tag) parts where tags are <...> (TMP rich-text)
    private static List<(string text, bool isTag)> SplitByTags(string input)
    {
        var parts = new List<(string text, bool isTag)>();
        int i = 0;
        while (i < input.Length)
        {
            int lt = input.IndexOf('<', i);
            if (lt < 0)
            {
                parts.Add((input.Substring(i), false));
                break;
            }

            // text before tag
            if (lt > i)
                parts.Add((input.Substring(i, lt - i), false));

            int gt = input.IndexOf('>', lt + 1);
            if (gt < 0)
            {
                // no closing '>', treat rest as text
                parts.Add((input.Substring(lt), false));
                break;
            }

            // tag itself
            parts.Add((input.Substring(lt, gt - lt + 1), true));
            i = gt + 1;
        }

        if (parts.Count == 0)
            parts.Add((input, false));

        return parts;
    }

    // Optional: programmatic additions at runtime (case-insensitive store)
    public void AddActionKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        _actionsSet.Add(keyword.Trim());
        if (!actionsList.Contains(keyword, StringComparer.InvariantCultureIgnoreCase))
            actionsList.Add(keyword.Trim());
    }

    public void AddObjectKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        _objectsSet.Add(keyword.Trim());
        if (!objectsList.Contains(keyword, StringComparer.InvariantCultureIgnoreCase))
            objectsList.Add(keyword.Trim());
    }
}

// Small helper for List.Contains with IEqualityComparer (Unity doesn't ship LINQ's overload for Lists)
static class ListExtensions
{
    public static bool Contains(this List<string> list, string value, StringComparer comparer)
    {
        foreach (var s in list)
            if (comparer.Equals(s, value)) return true;
        return false;
    }
}
