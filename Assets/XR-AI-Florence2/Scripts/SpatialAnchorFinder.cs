using Meta.WitAi.Attributes;
using PresentFutures.XRAI.Spatial;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks all OVRSpatialAnchor instances in the scene
/// and resolves anchors by the NAME saved with your "uuid;name" PlayerPrefs format.
/// Keeps a visible list in the Inspector for debugging.
/// </summary>
public class SpatialAnchorFinder : MonoBehaviour
{
    public static SpatialAnchorFinder Instance { get; private set; }

    [Header("PlayerPrefs Keys")]
    [Tooltip("Must match your SpatialAnchorManager's const")]
    public string NumUuidsPlayerPref = "numUuids";

    [Header("Auto Refresh")]
    public bool refreshOnEnable = true;
    public bool periodicRefresh = true;
    [Min(0.2f)] public float refreshIntervalSeconds = 2f;

    [Header("Tracked Anchors (runtime only, for debugging)")]
    [SerializeField] private List<OVRSpatialAnchor> trackedAnchors = new();

    // Internal fast lookup (uuid -> anchor)
    private readonly Dictionary<string, OVRSpatialAnchor> _byUuid = new();

    Coroutine _loop;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        if (refreshOnEnable) RefreshNow();
        if (periodicRefresh) _loop = StartCoroutine(RefreshLoop());
    }

    void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    IEnumerator RefreshLoop()
    {
        var wait = new WaitForSeconds(refreshIntervalSeconds);
        while (true)
        {
            RefreshNow();
            yield return wait;
        }
    }

    /// <summary>Rebuild the UUID->Anchor index and tracked list from the scene.</summary>
    public void RefreshNow()
    {
        _byUuid.Clear();
        trackedAnchors.Clear();

        var anchors = FindObjectsOfType<OVRSpatialAnchor>(includeInactive: true);
        foreach (var a in anchors)
        {
            if (!a) continue;
            var g = a.Uuid;
            if (g == Guid.Empty) continue; // not bound yet

            var key = ToKey(g);
            if (!_byUuid.ContainsKey(key))
            {
                _byUuid[key] = a;
                trackedAnchors.Add(a);
            }
        }
    }

    /// <summary>Register a newly created anchor immediately.</summary>
    public void Register(OVRSpatialAnchor anchor)
    {
        if (!anchor) return;
        var g = anchor.Uuid;
        if (g == Guid.Empty) return;

        var key = ToKey(g);
        _byUuid[key] = anchor;

        if (!trackedAnchors.Contains(anchor))
            trackedAnchors.Add(anchor);
    }

    /// <summary>Unregister when an anchor is destroyed.</summary>
    public void Unregister(OVRSpatialAnchor anchor)
    {
        if (!anchor) return;
        _byUuid.Remove(ToKey(anchor.Uuid));
        trackedAnchors.Remove(anchor);
    }

    /// <summary>
    /// Returns the spawned GameObject for the anchor whose saved NAME matches.
    /// </summary>
    public GameObject GetGameObjectBySavedName(string savedName)
    {
        if (string.IsNullOrEmpty(savedName)) return null;

        if (!TryGetSavedUuidByName(savedName, out var uuid))
            return null;

        if (_byUuid.TryGetValue(ToKey(uuid), out var anchor) && anchor)
            return anchor.gameObject;

        return null;
    }

    public bool TryGetSavedNameByUuid(Guid uuid, out string name)
    {
        name = null;
        if (uuid == Guid.Empty) return false;

        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        for (int i = 0; i < count; i++)
        {
            var entry = PlayerPrefs.GetString("uuid" + i, "");
            if (TryParseUuidAndName(entry, out var exUuid, out var exName))
            {
                if (exUuid == uuid)
                {
                    name = exName;
                    return true;
                }
            }
        }
        return false;
    }

    public bool TryGetSavedUuidByName(string savedName, out Guid uuid)
    {
        uuid = Guid.Empty;
        if (string.IsNullOrEmpty(savedName)) return false;

        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        for (int i = 0; i < count; i++)
        {
            var entry = PlayerPrefs.GetString("uuid" + i, "");
            if (TryParseUuidAndName(entry, out var exUuid, out var exName))
            {
                if (string.Equals(exName, savedName, StringComparison.Ordinal))
                {
                    uuid = exUuid;
                    return true;
                }
            }
        }
        return false;
    }

    // --- Helpers ---

    private static string ToKey(Guid g) => g.ToString("N").ToLowerInvariant();

    public static bool TryParseUuidAndName(string stored, out Guid uuid, out string name)
    {
        uuid = Guid.Empty;
        name = null;
        if (string.IsNullOrEmpty(stored)) return false;

        int sep = stored.IndexOf(';');
        if (sep < 0)
        {
            return Guid.TryParse(stored, out uuid);
        }

        var uuidPart = stored.Substring(0, sep);
        var namePart = (sep + 1 < stored.Length) ? stored.Substring(sep + 1) : "";
        if (!Guid.TryParse(uuidPart, out uuid)) return false;

        name = namePart;
        return true;
    }




    /// <summary>
    /// Returns a list of all anchors whose SpatialLabel name matches.
    /// </summary>
    public List<OVRSpatialAnchor> GetAnchorsBySpatialLabelName(string labelName)
    {
        List<OVRSpatialAnchor> matches = new List<OVRSpatialAnchor>();
        if (string.IsNullOrEmpty(labelName))
            return matches;

        foreach (var anchor in trackedAnchors)
        {
            if (!anchor) continue;

            var label = anchor.transform.GetComponent<SpatialLabel>();

            string objectName = label.ObjectName;

            if (label && string.Equals(objectName, labelName, StringComparison.Ordinal))
            {
                matches.Add(anchor);
            }
        }

        return matches;
    }

    [Button]
    public void MakeAnchorsPresenceAwareByLabelName(string labelName)
    {
        var anchors = GetAnchorsBySpatialLabelName(labelName);

        foreach (var anchor in anchors)
        {
            if (!anchor) continue;

            var spatialAnchor = anchor.transform.GetComponent<SpatialLabel>();
            if (spatialAnchor != null)
            {
                spatialAnchor.MakePressenceAware(true);
            }
            else
            {
                Debug.LogWarning($"Anchor '{anchor.name}' does not have a SpatialAnchor component.");
            }
        }
    }







}
