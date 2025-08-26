using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using PresentFutures.XRAI.Spatial;

public class SpatialAnchorManager : MonoBehaviour
{
    public static SpatialAnchorManager Instance { get; private set; }

    [Header("Anchor Prefab")]
    public OVRSpatialAnchor anchorPrefab;

    public const string NumUuidsPlayerPref = "numUuids";

    [Header("De-duplication (3D)")]
    [Tooltip("Meters: if a new hit is closer than this to an existing anchor with the same label, we treat it as the same object.")]
    public float dedupRadius = 1.20f; // 20 cm
    [Tooltip("Degrees: how different the surface normal can be to still count as same object.")]
    public float normalAngleThresholdDeg = 30f;

    private Canvas canvas;
    private TextMeshProUGUI uuidText;
    private TextMeshProUGUI savedStatusText;
    private List<OVRSpatialAnchor> anchors = new List<OVRSpatialAnchor>();
    private OVRSpatialAnchor lastCreatedAnchor;
    private AnchorLoader anchorLoader;

    // ---------- Dedup/registry ----------
    private class AnchorEntry
    {
        public OVRSpatialAnchor Anchor;
        public string Label;
        public Vector3 Normal;             // Surface normal at placement (approx)
        public int SeenCount = 1;          // How many times we confirmed this object
        public GameObject LabelGO;         // Optional label UI root
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

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            CreateSpatialAnchor();
        }

        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            SaveLastCreatedAnchor();
        }

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            UnsaveLastCreatedAnchor();
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            UnsaveAllAnchors();
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
        {
            LoadSavedAnchors();
        }
    }

    // ------------------------------------------------------------------------------------
    // PUBLIC: Creation (controller-based quick spawn)
    // ------------------------------------------------------------------------------------
    public void CreateSpatialAnchor()
    {
        var pos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        var rot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, pos, rot);

        canvas = workingAnchor.gameObject.GetComponentInChildren<Canvas>();
        StartCoroutine(AnchorCreated(workingAnchor));
    }

    // ------------------------------------------------------------------------------------
    // PUBLIC: Creation (programmatic spawn with label)
    // NOTE: keeps your original signature for backwards-compat
    // ------------------------------------------------------------------------------------
    public void CreateSpatialAnchor(GameObject prefab, Vector3 position, Quaternion rotation, string name)
    {
        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, position, rotation);

        canvas = workingAnchor.gameObject.GetComponentInChildren<Canvas>();

        var labelComp = workingAnchor.GetComponent<SpatialLabel>();
        if (labelComp != null) labelComp.Name = name;

        // No surface normal provided here—store a default upward normal.
        _pendingMeta[workingAnchor] = (name, Vector3.up, workingAnchor.gameObject);

        StartCoroutine(AnchorCreated(workingAnchor));
    }

    // ------------------------------------------------------------------------------------
    // OPTIONAL: Creation (preferred for dedup), lets you pass the surface normal
    // Use this from your Florence hit (where you have hit.point and hit.normal)
    // ------------------------------------------------------------------------------------
    public OVRSpatialAnchor CreateSpatialAnchor(GameObject prefab, Vector3 position, Quaternion rotation, string name, Vector3 surfaceNormal, GameObject labelRoot = null)
    {
        // 3D de-dup: if same label nearby with similar normal, reuse instead of new
        OVRSpatialAnchor existing;
        AnchorEntry entry;
        if (TryGetNearbyByLabel(name, position, surfaceNormal, out existing, out entry))
        {
            // Optional: nudge toward the new hit or keep as-is
            // existing.transform.position = Vector3.Lerp(existing.transform.position, position, 0.25f);
            IncrementSeenCount(entry);
            return existing;
        }

        // Create new
        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, position, rotation);

        var labelComp = workingAnchor.GetComponent<SpatialLabel>();
        if (labelComp != null) labelComp.Name = name;

        _pendingMeta[workingAnchor] = (name, surfaceNormal, labelRoot ?? workingAnchor.gameObject);

        StartCoroutine(AnchorCreated(workingAnchor));
        return workingAnchor;
    }

    // ------------------------------------------------------------------------------------
    // INTERNAL: Finalize creation once anchor is ready, then register for dedup
    // ------------------------------------------------------------------------------------
    private IEnumerator AnchorCreated(OVRSpatialAnchor workingAnchor)
    {
        while (!workingAnchor.Created && !workingAnchor.Localized)
        {
            yield return new WaitForEndOfFrame();
        }

        Guid anchorGuid = workingAnchor.Uuid;
        anchors.Add(workingAnchor);
        lastCreatedAnchor = workingAnchor;

        // Register into our dedup registry
        if (_pendingMeta.TryGetValue(workingAnchor, out var meta))
        {
            RegisterAnchor(workingAnchor, meta.label, meta.normal, meta.labelGO);
            _pendingMeta.Remove(workingAnchor);
        }
        else
        {
            // if no metadata was provided, still register with a generic label
            RegisterAnchor(workingAnchor, label: "Object", normal: Vector3.up, labelGO: workingAnchor.gameObject);
        }

        // (Optional) UI update if you want
        // if (uuidText != null) uuidText.text = "UUID: " + anchorGuid.ToString();
        // if (savedStatusText != null) savedStatusText.text = "Not Saved";
    }

    // ------------------------------------------------------------------------------------
    // SAVE / ERASE
    // ------------------------------------------------------------------------------------
    private void SaveLastCreatedAnchor()
    {
        if (lastCreatedAnchor == null) return;

        lastCreatedAnchor.Save((lastCreatedAnchor, success) =>
        {
            if (success)
            {
                Debug.Log("Successful save");
                if (savedStatusText != null) savedStatusText.text = "Saved";
            }
        });

        SaveUuidToPlayerPrefs(lastCreatedAnchor.Uuid);
    }

    void SaveUuidToPlayerPrefs(Guid uuid)
    {
        if (!PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            PlayerPrefs.SetInt(NumUuidsPlayerPref, 0);
        }

        int playerNumUuids = PlayerPrefs.GetInt(NumUuidsPlayerPref);
        PlayerPrefs.SetString("uuid" + playerNumUuids, uuid.ToString());
        PlayerPrefs.SetInt(NumUuidsPlayerPref, ++playerNumUuids);
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

    private void UnsaveAllAnchors()
    {
        foreach (var anchor in anchors)
        {
            UnsaveAnchor(anchor);
        }

        anchors.Clear();
        _entries.Clear();
        _pendingMeta.Clear();
        ClearAllUuidsFromPlayerPrefs();
    }

    private void UnsaveAnchor(OVRSpatialAnchor anchor)
    {
        if (anchor == null) return;

        anchor.Erase((erasedAnchor, success) =>
        {
            if (success)
            {
                var textComponents = erasedAnchor.GetComponentsInChildren<TextMeshProUGUI>();
                if (textComponents.Length > 1)
                {
                    var savedStatusText = textComponents[1];
                    savedStatusText.text = "Not Saved";
                }

                // Remove from registry
                _entries.RemoveAll(e => e.Anchor == erasedAnchor);
            }
        });
    }

    private void ClearAllUuidsFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            int playerNumUuids = PlayerPrefs.GetInt(NumUuidsPlayerPref);
            for (int i = 0; i < playerNumUuids; i++)
            {
                PlayerPrefs.DeleteKey("uuid" + i);
            }
            PlayerPrefs.DeleteKey(NumUuidsPlayerPref);
            PlayerPrefs.Save();
        }
    }

    public void LoadSavedAnchors()
    {
        if (anchorLoader != null)
            anchorLoader.LoadAnchorsByUuid();
    }

    // ------------------------------------------------------------------------------------
    // DEDUP REGISTRY API
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Public wrapper that does not expose internal AnchorEntry type.
    /// Returns true if an existing anchor with the same label is near the hit point (and similar normal).
    /// </summary>
    public bool TryGetNearbyByLabel(string label, Vector3 hitPoint, Vector3 hitNormal, out OVRSpatialAnchor existing)
    {
        AnchorEntry _;
        return TryGetNearbyByLabel(label, hitPoint, hitNormal, out existing, out _);
    }

    /// <summary>
    /// Register an anchor so future spawns can detect duplicates.
    /// Usually called automatically in AnchorCreated().
    /// </summary>
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

    // ----- PRIVATE typed overload (no ambiguity) -----
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
}
