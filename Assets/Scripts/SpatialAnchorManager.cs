using Meta.XR.BuildingBlocks;
using PresentFutures.XRAI.Florence;
using PresentFutures.XRAI.Spatial;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class SpatialAnchorManager : MonoBehaviour
{
    public static SpatialAnchorManager Instance { get; private set; }

    [Header("Anchor Prefab")]
    public OVRSpatialAnchor anchorPrefab;

    public GameObject anchorGOPrefabForLoading;
    public SpatialAnchorCoreBuildingBlock spatialAnchorCore;
    public const string NumUuidsPlayerPref = "numUuids";

    [Header("De-duplication (3D)")]
    [Tooltip("Turn off to allow multiple anchors with the same label close to each other.")]
    public bool enableDedup = true;
    [Tooltip("Meters: if a new hit is closer than this to an existing anchor with the same label, we treat it as the same object.")]
    public float dedupRadius = 1.20f; // 1.2 m
    [Tooltip("Degrees: how different the surface normal can be to still count as same object.")]
    public float normalAngleThresholdDeg = 30f;

    private Canvas canvas;
    private TextMeshProUGUI uuidText;
    private TextMeshProUGUI savedStatusText;
    private readonly List<OVRSpatialAnchor> anchors = new();
    private OVRSpatialAnchor lastCreatedAnchor;
    private AnchorLoader anchorLoader;
    public Florence2Controller florence2Controller;

    // Keep a map of UUID -> saved name (from PlayerPrefs)
    private readonly Dictionary<Guid, string> _savedNames = new();

    // ---------- Dedup/registry ----------
    private class AnchorEntry
    {
        public OVRSpatialAnchor Anchor;
        public string Label;
        public Vector3 Normal;     // Surface normal at placement (approx)
        public int SeenCount = 1;  // How many times we confirmed this object
        public GameObject LabelGO; // Optional label UI root
    }

    // Registry of all anchors we manage
    private readonly List<AnchorEntry> _entries = new();

    // If creation is async, cache pending metadata until the OVRSpatialAnchor is actually Created/Localized
    private readonly Dictionary<OVRSpatialAnchor, (string label, Vector3 normal, GameObject labelGO)> _pendingMeta
        = new Dictionary<OVRSpatialAnchor, (string, Vector3, GameObject)>();

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // uncomment if you want persistence

        anchorLoader = GetComponent<AnchorLoader>();
    }

    private void Update()
    {
        LogAllPlayerPrefsAnchors();
    }

    public void LoadSavedAnchors()
    {
        // Load all stored UUIDs (and names) from PlayerPrefs
        List<Guid> loadedAnchors = LoadAllAnchors();

        if (loadedAnchors.Count == 0)
        {
            Debug.Log("[SpatialAnchorManager] No saved anchors found.");
            return;
        }

        Debug.Log($"[SpatialAnchorManager] Loading {loadedAnchors.Count} anchors...");

        // Ask the core building block to resolve and instantiate them
        spatialAnchorCore.LoadAndInstantiateAnchors(anchorGOPrefabForLoading, loadedAnchors);
        // SpatialLabel.Start() will pull names from _savedNames via TryGetSavedName(...)
    }

    // ------------------------------------------------------------------------------------
    // PUBLIC: Creation (controller-based quick spawn)
    // ------------------------------------------------------------------------------------
    public void CreateSpatialAnchor()
    {
        Debug.Log("Called 1");
        var pos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        var rot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, pos, rot);
        canvas = workingAnchor.gameObject.GetComponentInChildren<Canvas>();

        StartCoroutine(AnchorCreated(workingAnchor)); // name resolved internally
    }

    // ------------------------------------------------------------------------------------
    // PUBLIC: Creation (programmatic spawn with label)
    // ------------------------------------------------------------------------------------
    public void CreateSpatialAnchor(GameObject prefab, Vector3 position, Quaternion rotation, string name)
    {
        Debug.Log("Called 2");

        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, position, rotation);

        canvas = workingAnchor.gameObject.GetComponentInChildren<Canvas>();

        var labelComp = workingAnchor.GetComponent<PresentFutures.XRAI.Spatial.SpatialLabel>();
        if (labelComp != null) labelComp.Name = name;

        // No surface normal provided here—store a default upward normal.
        _pendingMeta[workingAnchor] = (name, Vector3.up, workingAnchor.gameObject);

        StartCoroutine(AnchorCreated(workingAnchor, name));
    }

    // ------------------------------------------------------------------------------------
    // OPTIONAL: Creation (preferred for dedup)
    // ------------------------------------------------------------------------------------
    public OVRSpatialAnchor CreateSpatialAnchor(GameObject prefab, Vector3 position, Quaternion rotation, string name, Vector3 surfaceNormal, GameObject labelRoot = null)
    {
        Debug.Log("Called 3");

        if (enableDedup && TryGetNearbyByLabel(name, position, surfaceNormal, out OVRSpatialAnchor existing, out AnchorEntry entry))
        {
            IncrementSeenCount(entry);
            return existing;
        }

        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, position, rotation);

        var labelComp = workingAnchor.GetComponent<PresentFutures.XRAI.Spatial.SpatialLabel>();
        if (labelComp != null) labelComp.Name = name;

        _pendingMeta[workingAnchor] = (name, surfaceNormal, labelRoot ?? workingAnchor.gameObject);

        StartCoroutine(AnchorCreated(workingAnchor, name));

        return workingAnchor;
    }

    // ------------------------------------------------------------------------------------
    // INTERNAL: finalize creation then register + save
    // ------------------------------------------------------------------------------------
    private IEnumerator AnchorCreated(OVRSpatialAnchor workingAnchor)
    {
        // Parameterless overload: resolve name from pending meta, saved names or label component
        return AnchorCreated_Internal(workingAnchor, null);
    }

    private IEnumerator AnchorCreated(OVRSpatialAnchor workingAnchor, string name)
    {
        return AnchorCreated_Internal(workingAnchor, name);
    }

    private IEnumerator AnchorCreated_Internal(OVRSpatialAnchor workingAnchor, string name)
    {
        float lastLogTime = 0f;

        // Wait until the anchor exists in the session (Created OR Localized)
        while (!workingAnchor.Created && !workingAnchor.Localized)
        {
            if (Time.time - lastLogTime > 1f)
            {
                Debug.Log($"[SpatialAnchor] Waiting... Created={workingAnchor.Created}, Localized={workingAnchor.Localized}");
                lastLogTime = Time.time;
            }
            yield return new WaitForEndOfFrame();
        }

        Guid anchorGuid = workingAnchor.Uuid;
        anchors.Add(workingAnchor);
        lastCreatedAnchor = workingAnchor;

        // Choose a final label/name
        string finalName = name;

        if (string.IsNullOrEmpty(finalName) && _pendingMeta.TryGetValue(workingAnchor, out var meta))
        {
            finalName = meta.label;
        }

        if (string.IsNullOrEmpty(finalName) && _savedNames.TryGetValue(anchorGuid, out var savedName))
        {
            finalName = savedName;
        }

        var labelComp = workingAnchor.GetComponent<PresentFutures.XRAI.Spatial.SpatialLabel>();
        if (labelComp != null)
        {
            if (string.IsNullOrEmpty(finalName))
            {
                // Fall back to existing label component name or a generic default
                finalName = string.IsNullOrEmpty(labelComp.Name) ? "Object" : labelComp.Name;
            }

            // ✅ Ensure SpatialLabel shows the name as soon as the anchor spawns
            labelComp.Name = finalName;
        }

        // Register in our dedup registry
        if (_pendingMeta.TryGetValue(workingAnchor, out var meta2))
        {
            RegisterAnchor(workingAnchor, meta2.label, meta2.normal, meta2.labelGO);
            _pendingMeta.Remove(workingAnchor);
        }
        else
        {
            RegisterAnchor(workingAnchor, label: string.IsNullOrEmpty(finalName) ? "Object" : finalName, normal: Vector3.up, labelGO: workingAnchor.gameObject);
        }

        // Persist (Meta storage + our UUID index) once save actually succeeds
        SaveAnchorToPlayerPrefs(workingAnchor, finalName);

        yield return null;
    }

    private void UnsaveLastCreatedAnchor()
    {
        if (lastCreatedAnchor == null) return;

        lastCreatedAnchor.Erase((lastCreatedAnchor, success) =>
        {
            if (success)
            {
                if (savedStatusText != null) savedStatusText.text = "Not Saved";
            }
        });
    }

    public void UnsaveAllAnchors()
    {
        foreach (var anchor in anchors)
        {
            UnsaveAnchor(anchor);
            if (anchor) Destroy(anchor.gameObject);
        }

        anchors.Clear();
        _entries.Clear();
        _pendingMeta.Clear();
        ClearAllUuidsFromPlayerPrefs();
    }

    public async void UnsaveAnchor(OVRSpatialAnchor anchor)
    {
        if (!anchor) return;

        RemoveUuidFromPlayerPrefs(anchor);
        spatialAnchorCore.EraseAnchorByUuid(anchor.Uuid);

        var label = anchor.transform.GetComponent<PresentFutures.XRAI.Spatial.SpatialLabel>();
        if (label) label.Disable();
    }

    public async Task<bool> UnsaveAnchorAsync(OVRSpatialAnchor anchor, bool destroyAnchorGO = true)
    {
        if (!anchor) return false;

        var result = await anchor.EraseAnchorAsync();
        if (!result.Success)
        {
            Debug.LogWarning($"[SpatialAnchorManager] EraseAnchorAsync failed: {result.Status}");
            return false;
        }

        RemoveUuidFromPlayerPrefs(anchor);      // keep PlayerPrefs in sync
        _entries.RemoveAll(e => e.Anchor == anchor);
        anchors.Remove(anchor);

        if (destroyAnchorGO && anchor) Destroy(anchor.gameObject);
        return true;
    }

    // ---------------------------
    // Save/Load UUIDs (PlayerPrefs)
    // ---------------------------

    private async void SaveAnchorToPlayerPrefs(OVRSpatialAnchor anchor, string name)
    {
        if (!anchor) return;

        var result = await anchor.SaveAnchorAsync();

        if (result.Success)
        {
            SaveUuidToPlayerPrefs(anchor.Uuid, name);
            Debug.Log($"[SpatialAnchorManager] Saved anchor UUID = {anchor.Uuid}");
        }
        else
        {
            Debug.LogWarning($"[SpatialAnchorManager] SaveAnchorAsync failed: {result.Status}");
        }
    }

    // "uuid" or "uuid;name"
    private static bool TryParseUuidAndName(string entry, out Guid uuid, out string name)
    {
        uuid = Guid.Empty;
        name = string.Empty;
        if (string.IsNullOrEmpty(entry)) return false;

        int sep = entry.IndexOf(';');
        string guidPart = sep >= 0 ? entry.Substring(0, sep) : entry;
        if (!Guid.TryParse(guidPart, out uuid)) return false;

        if (sep >= 0 && sep + 1 < entry.Length)
            name = entry.Substring(sep + 1);

        return true;
    }

    void SaveUuidToPlayerPrefs(Guid uuid, string name)
    {
        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);

        // Handle both old ("uuid") and new ("uuid;name") formats
        for (int i = 0; i < count; i++)
        {
            string existing = PlayerPrefs.GetString("uuid" + i, "");
            if (TryParseUuidAndName(existing, out var ex, out _))
            {
                if (ex == uuid) return; // already recorded
            }
        }

        PlayerPrefs.SetString("uuid" + count, uuid.ToString() + ";" + name);
        PlayerPrefs.SetInt(NumUuidsPlayerPref, count + 1);
        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(name))
            _savedNames[uuid] = name;
    }

    private List<Guid> LoadAllAnchors()
    {
        _savedNames.Clear();
        var loadedAnchors = new List<Guid>();

        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        for (int i = 0; i < count; i++)
        {
            string key = "uuid" + i;
            string entry = PlayerPrefs.GetString(key, string.Empty);

            if (TryParseUuidAndName(entry, out var uuid, out var name))
            {
                loadedAnchors.Add(uuid);
                if (!string.IsNullOrEmpty(name))
                    _savedNames[uuid] = name;
            }
        }

        Debug.Log($"Loaded {loadedAnchors.Count} spatial anchors from storage");
        return loadedAnchors;
    }

    private void ClearAllUuidsFromPlayerPrefs()
    {
        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        for (int i = 0; i < count; i++)
            PlayerPrefs.DeleteKey("uuid" + i);

        PlayerPrefs.DeleteKey(NumUuidsPlayerPref);
        PlayerPrefs.Save();
        _savedNames.Clear();
    }

    // ------------------------------------------------------------------------------------
    // DEDUP REGISTRY API
    // ------------------------------------------------------------------------------------
    public bool TryGetNearbyByLabel(string label, Vector3 hitPoint, Vector3 hitNormal, out OVRSpatialAnchor existing)
    {
        AnchorEntry _;
        return TryGetNearbyByLabel(label, hitPoint, hitNormal, out existing, out _);
    }

    public void RegisterAnchor(OVRSpatialAnchor anchor, string label, Vector3 normal, GameObject labelGO = null)
    {
        if (anchor == null) return;

        var existing = _entries.FirstOrDefault(e => e.Anchor == anchor);
        if (existing != null)
        {
            IncrementSeenCount(existing);
            return;
        }

        _entries.Add(new AnchorEntry
        {
            Anchor = anchor,
            Label = label,
            Normal = normal,
            LabelGO = labelGO
        });

        if (labelGO != null)
        {
            var tmp = labelGO.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = label;
        }
    }

    public void LogAllPlayerPrefsAnchors()
    {
        if (!PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            Debug.Log("[PlayerPrefs] No saved UUIDs found.");
            return;
        }

        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        Debug.Log($"[PlayerPrefs] Stored Anchor Count = {count}");

        for (int i = 0; i < count; i++)
        {
            string key = "uuid" + i;
            string value = PlayerPrefs.GetString(key, "MISSING");
            Debug.Log($"[PlayerPrefs] {key} = {value}");
        }
    }

    private bool TryGetNearbyByLabel(string label, Vector3 hitPoint, Vector3 hitNormal,
        out OVRSpatialAnchor existing, out AnchorEntry entry)
    {
        existing = null;
        entry = null;

        var candidates = _entries.Where(e => string.Equals(e.Label, label, StringComparison.OrdinalIgnoreCase));
        foreach (var c in candidates)
        {
            if (!c.Anchor || !c.Anchor.gameObject) continue;

            float dist = Vector3.Distance(c.Anchor.transform.position, hitPoint);
            if (dist > dedupRadius) continue;

            float ang = Vector3.Angle(c.Normal, hitNormal);
            if (ang > normalAngleThresholdDeg) continue;

            existing = c.Anchor;
            entry = c;
            return true;
        }
        return false;
    }

    private void IncrementSeenCount(AnchorEntry e)
    {
        e.SeenCount++;
        if (e.LabelGO)
        {
            var tmp = e.LabelGO.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = $"{e.Label} (x{e.SeenCount})";
        }
    }

    // Deletes only instantiated anchor GameObjects in the scene.
    // Keeps PlayerPrefs + Meta storage untouched so you can reload.
    public void DeleteAnchorsInSceneOnly()
    {
        for (int i = anchors.Count - 1; i >= 0; i--)
        {
            var a = anchors[i];
            if (a) Destroy(a.gameObject);
        }

        anchors.Clear();
        lastCreatedAnchor = null;

        // Clear dedup registry of scene instances (storage untouched)
        _entries.Clear();
        _pendingMeta.Clear();

        Debug.Log("[SpatialAnchorManager] Deleted scene anchors only (storage & PlayerPrefs untouched).");
    }

    /// <summary>
    /// Remove a specific anchor's UUID from PlayerPrefs (compacts indices).
    /// Returns true if a record was removed.
    /// </summary>
    public bool RemoveUuidFromPlayerPrefs(OVRSpatialAnchor anchor)
    {
        if (!anchor) return false;
        return RemoveUuidFromPlayerPrefs(anchor.Uuid);
    }

    /// <summary>
    /// Remove a UUID from PlayerPrefs (compacts indices).
    /// Returns true if a record was removed.
    /// </summary>
    public static bool RemoveUuidFromPlayerPrefs(Guid uuid)
    {
        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        if (count <= 0) return false;

        int index = -1;

        // Find the matching index (handle "uuid" or "uuid;name")
        for (int i = 0; i < count; i++)
        {
            string key = "uuid" + i;
            string val = PlayerPrefs.GetString(key, string.Empty);
            if (TryParseUuidAndName(val, out var parsed, out _))
            {
                if (parsed == uuid)
                {
                    index = i;
                    break;
                }
            }
        }

        if (index == -1) return false; // not found

        // Shift subsequent entries down to keep 0..N-2 contiguous
        for (int i = index; i < count - 1; i++)
        {
            string nextVal = PlayerPrefs.GetString("uuid" + (i + 1), string.Empty);
            if (!string.IsNullOrEmpty(nextVal))
                PlayerPrefs.SetString("uuid" + i, nextVal);
            else
                PlayerPrefs.DeleteKey("uuid" + i);
        }

        // Delete last key and update count
        PlayerPrefs.DeleteKey("uuid" + (count - 1));
        PlayerPrefs.SetInt(NumUuidsPlayerPref, count - 1);
        PlayerPrefs.Save();

        Debug.Log($"[SpatialAnchorManager] Removed UUID {uuid} from PlayerPrefs. New count = {count - 1}");
        return true;
    }

    // Public accessor so SpatialLabel can pull the saved name after load
    public bool TryGetSavedName(Guid uuid, out string name) => _savedNames.TryGetValue(uuid, out name);
}
