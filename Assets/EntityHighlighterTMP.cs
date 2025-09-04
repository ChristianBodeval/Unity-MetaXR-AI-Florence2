using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using NaughtyAttributes;   // optional (for the [Button]s)

[RequireComponent(typeof(TMP_Text))]
public class WitEntityTMPHighlighter : MonoBehaviour
{
    [Header("Wit/Wiz API")]
    [Tooltip("SERVER token (use only for local testing, do NOT ship it).")]
    [SerializeField] private string serverToken = "REPLACE_WITH_SERVER_TOKEN";
    [Tooltip("The entity that represents actions in Wit/Wiz (e.g., 'action').")]
    [SerializeField] private string actionEntityName = "action";
    [Tooltip("The entity that represents objects in Wit/Wiz (e.g., 'object').")]
    [SerializeField] private string objectEntityName = "object";
    [SerializeField] private string apiVersion = "20240304";

    [Header("Colors")]
    [SerializeField] private Color actionsColor = new Color(0.95f, 0.55f, 0.20f); // orange-ish
    [SerializeField] private Color objectsColor = new Color(0.25f, 0.70f, 1f);     // blue-ish

    [Header("Auto Refresh")]
    [Tooltip("Optional: re-fetch entity keywords every N seconds (0 = don't auto-refresh).")]
    [SerializeField] private float autoRefreshSeconds = 0f;

    private TMP_Text textComp;
    private HashSet<string> _actionsSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    private HashSet<string> _objectsSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

    // --- Tokenization helpers (same as before) ---
    private static readonly Regex Splitter = new Regex(@"(\s+|[^\p{L}\p{Nd}_]+)", RegexOptions.Compiled);
    private static readonly Regex EdgePunct = new Regex(@"^[^\p{L}\p{Nd}_]+|[^\p{L}\p{Nd}_]+$", RegexOptions.Compiled);

    // --- Minimal JSON DTOs for Wit entity response ---
    [Serializable] private class WitEntityResponse { public WitKeyword[] keywords; }
    [Serializable] private class WitKeyword { public string keyword; public string[] synonyms; }

    private Coroutine _autoRefreshRoutine;

    // Singleton-style convenience (optional)
    public static WitEntityTMPHighlighter Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        textComp = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        // initial fetch
        StartCoroutine(RefreshAllKeywords());

        if (autoRefreshSeconds > 0f)
            _autoRefreshRoutine = StartCoroutine(AutoRefreshLoop());
    }

    private void OnDisable()
    {
        if (_autoRefreshRoutine != null)
        {
            StopCoroutine(_autoRefreshRoutine);
            _autoRefreshRoutine = null;
        }
    }

    private IEnumerator AutoRefreshLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(1f, autoRefreshSeconds));
        while (true)
        {
            yield return wait;
            yield return RefreshAllKeywords();
        }
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
            Debug.LogWarning("[WitEntityTMPHighlighter] Server token missing.");
            yield break;
        }

        // Fetch both entities in sequence
        yield return FetchEntityKeywords(actionEntityName, _actionsSet);
        yield return FetchEntityKeywords(objectEntityName, _objectsSet);
    }

    private IEnumerator FetchEntityKeywords(string entityName, HashSet<string> targetSet)
    {
        targetSet.Clear();

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
                    // Parse keywords
                    var json = req.downloadHandler.text;
                    var parsed = JsonUtility.FromJson<WitEntityResponse>(json);
                    if (parsed != null && parsed.keywords != null)
                    {
                        foreach (var kw in parsed.keywords)
                        {
                            if (!string.IsNullOrWhiteSpace(kw.keyword))
                                targetSet.Add(kw.keyword.Trim());

                            // include synonyms as well
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
                    Debug.Log($"[WitEntityTMPHighlighter] Loaded {targetSet.Count} keywords for '{entityName}'.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WitEntityTMPHighlighter] Parse error for '{entityName}': {e}");
                }
            }
            else
            {
                Debug.LogError($"[WitEntityTMPHighlighter] Fetch failed for '{entityName}' ({code}): {req.error}\n{req.downloadHandler.text}");
            }
        }
    }

    // --- Always-highlight in Update (per your request) ---
    private void Update()
    {
        if (!textComp) return;
        if (string.IsNullOrEmpty(textComp.text)) return;

        textComp.text = BuildColoredText(textComp.text);
    }

    private string BuildColoredText(string input)
    {
        var tokens = Splitter.Split(input);
        var sb = new StringBuilder(tokens.Length * 2);
        string actionHex = ColorUtility.ToHtmlStringRGB(actionsColor);
        string objectHex = ColorUtility.ToHtmlStringRGB(objectsColor);

        foreach (var token in tokens)
        {
            // keep whitespace-only or punct-only as-is
            string core = EdgePunct.Replace(token, "");
            if (string.IsNullOrEmpty(core))
            {
                sb.Append(token);
                continue;
            }

            bool isAction = _actionsSet.Contains(core);
            bool isObject = _objectsSet.Contains(core);

            if (!isAction && !isObject)
            {
                // try lowercase normalization
                var lc = core.ToLowerInvariant();
                isAction = _actionsSet.Contains(lc);
                isObject = _objectsSet.Contains(lc);
            }

            if (isAction || isObject)
            {
                string hex = isAction ? actionHex : objectHex; // action precedence
                sb.Append("<color=#").Append(hex).Append(">").Append(token).Append("</color>");
            }
            else
            {
                sb.Append(token);
            }
        }
        return sb.ToString();
    }
}
