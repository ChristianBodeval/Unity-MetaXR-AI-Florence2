using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Standalone RoundedCorner UI modifier (no Meta deps).
/// - If a decidingRectTransform is set (and has RoundedCornerUI):
///     Derived = max(0, decider.Global + decider LayoutGroup padding.left), then clamped to this element's max radius.
///     (Uses HorizontalLayoutGroup.padding.left if present, otherwise VerticalLayoutGroup.padding.left.)
///     Direct inputs are ignored; inspector shows read-only derived values.
/// - Without decider:
///     - If Global Border changes, all 4 corners sync to it (clamped to max radius).
///     - If corners are edited, corners take priority (each clamped to max radius).
/// - All values are clamped to [0 … min(width, height)/2] of THIS element.
/// - Live updates via:
///     - Change bus (any instance edit notifies listeners with sender reference)
///     - OnRectTransformDimensionsChange (layout/size changes)
/// - Values you set in the Inspector are serialized and applied again in Start().
/// - **Also updates at runtime; any change triggers ForceUIUpdate2().**
/// - **Only updates if RectTransform width is greater than 0.**
/// - **NEW:** One-way binding support — this instance can follow another instance’s values.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class RoundedCornerUI : UIBehaviour, IMeshModifier
{
    public ToggleHandler button;
    // ─────────────────────────────────────────────────────────────────────────────
    // Instance registry + change bus (now passes SENDER)
    // ─────────────────────────────────────────────────────────────────────────────
    private static readonly List<RoundedCornerUI> s_all = new();
    private static event Action<RoundedCornerUI> OnAnyChanged;
    private static void BroadcastChanged(RoundedCornerUI who) => OnAnyChanged?.Invoke(who);

    [Header("Inheritance")]
    public RectTransform decidingRectTransform;

    [Header("Global Control (no decider)")]
    [SerializeField] private float globalBorder = -1f;

    [Header("Corners (TL, TR, BR, BL) (no decider)")]
    [SerializeField] private Vector4 cornerValues = new Vector4(8f, 8f, 8f, 8f);

    [HideInInspector] public Vector4 borderRadius;

    // Expose current corners safely for followers.
    public Vector4 CurrentCornerValues => borderRadius;

    private Image _image;

    // ---- Runtime change coalescing ----
    private bool _runtimeDirty;        // set when a change happens
    private bool _started;             // ensure Start init runs once

    // ─────────────────────────────────────────────────────────────────────────────
    // NEW: One-way binding (this instance follows another instance)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Follow Another RoundedCornerUI (one-way)")]
    [Tooltip("If assigned, this instance will mirror values from the source.")]
    [SerializeField] private RoundedCornerUI followSource;

    [SerializeField] private bool followGlobalBorder = true;
    [SerializeField] private bool followCornerValues = false;

    [Tooltip("If true, keep syncing whenever the source changes. If false, only sync once on enable/validate.")]
    [SerializeField] private bool continuousFollow = true;

    // prevent feedback loops if user accidentally chains followers
    private bool _isApplyingFromSource;

    /// <summary>
    /// Assign (or change) the follow source at runtime/code.
    /// </summary>
    public void SetFollowSource(RoundedCornerUI newSource, bool followGlobal = true, bool followCorners = false, bool keepFollowing = true)
    {
        followSource = newSource;
        followGlobalBorder = followGlobal;
        followCornerValues = followCorners;
        continuousFollow = keepFollowing;
        ApplyFromSourceIfAny(immediate: true);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Properties
    // ─────────────────────────────────────────────────────────────────────────────
    public Vector4 CornerValues
    {
        get => cornerValues;
        set
        {
            if (!HasValidRect()) { cornerValues = value; return; }

            var maxR = GetMaxCornerRadius();
            cornerValues = ClampV4(value, 0f, maxR);
            borderRadius = cornerValues;

            SetImageDirty();
            MarkDirtyRuntime(true);
            BroadcastChanged(this);
        }
    }

    public float GlobalBorder
    {
        get => Mathf.Clamp(globalBorder < 0f ? 0f : globalBorder, 0f, GetMaxCornerRadius());
        set
        {
            if (!HasValidRect()) { globalBorder = value; return; }

            var clamped = Mathf.Clamp(value, 0f, GetMaxCornerRadius());
            globalBorder = clamped;
            ApplyGlobal(globalBorder, notify: true);
            MarkDirtyRuntime(true);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        if (!_image) _image = GetComponent<Image>();
        if (!HasValidRect()) return;

        var maxR = GetMaxCornerRadius();

        if (globalBorder < 0f && globalBorder != -1f) globalBorder = 0f;
        if (globalBorder >= 0f) globalBorder = Mathf.Clamp(globalBorder, 0f, maxR);

        cornerValues = ClampV4(cornerValues, 0f, maxR);

        // If we have a source set, do an immediate one-shot sync (even in edit mode)
        ApplyFromSourceIfAny(immediate: true);

        RecalculateBorder();
        SetImageDirty();
        BroadcastChanged(this);
        MarkDirtyRuntime(true);
    }
#endif

    RectTransform rt;
    ContentSizeFitter cs1;
    ContentSizeFitter cs2;

    protected override void OnEnable()
    {
        rt = GetComponent<RectTransform>();

        // Defensive: some hierarchies may not match this pattern
        try
        {
            cs1 = rt.GetChild(0).GetComponent<ContentSizeFitter>();
            cs2 = cs1.transform.Find("Text").GetComponent<ContentSizeFitter>();
        }
        catch { /* ignore if not present */ }

        base.OnEnable();
        if (!_image) _image = GetComponent<Image>();
        if (!s_all.Contains(this)) s_all.Add(this);

        OnAnyChanged += HandleAnyChanged;

        if (HasValidRect())
        {
            // Initial sync from source if set
            ApplyFromSourceIfAny(immediate: true);

            RecalculateBorder();
            SetImageDirty();
            MarkDirtyRuntime(true);
        }
    }

    protected override void Start()
    {
        base.Start();
        _started = true;
        if (HasValidRect())
        {
            RecalculateBorder();
            SetImageDirty();
            ForceUIUpdate2(); // ensure initial layout in play/edit
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        s_all.Remove(this);
        OnAnyChanged -= HandleAnyChanged;
        _image = null;
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (!HasValidRect()) return;

        RecalculateBorder();
        SetImageDirty();
        BroadcastChanged(this);
        MarkDirtyRuntime(true);
    }

    private void HandleAnyChanged(RoundedCornerUI who)
    {
        if (!HasValidRect()) return;

        // Only react to the specific source we are following (and only if continuous)
        if (continuousFollow && followSource != null && who == followSource)
        {
            ApplyFromSourceIfAny(immediate: false);
        }

        RecalculateBorder();
        SetImageDirty();
        MarkDirtyRuntime(true);
    }

    // --- Runtime coalesced updater ---
    private void LateUpdate()
    {
        if (!_runtimeDirty) return;
        _runtimeDirty = false; // clear the dirty flag
        ForceUIUpdate2();
    }

    /// <summary>
    /// Mark this control dirty. If immediate is true, triggers ForceUIUpdate2 next LateUpdate.
    /// </summary>
    private void MarkDirtyRuntime(bool immediate = false)
    {
        _runtimeDirty = true;
        if (!Application.isPlaying && !_started)
        {
            // In Edit Mode before Start, apply immediately to keep Scene view responsive.
            ForceUIUpdate2();
            _runtimeDirty = false;
        }
    }

    public void ForceUIUpdate2()
    {
        ForceUIUpdate();

        if (rt && rt.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        if (decidingRectTransform && decidingRectTransform.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(decidingRectTransform);

        // Be defensive: not all parents will have a ContentSizeFitter
        if (rt && rt.parent)
        {
            var parentFitter = rt.parent.GetComponent<ContentSizeFitter>();
            if (parentFitter) parentFitter.enabled = true;
        }

        //RuntimeSetGlobal(transform.parent.parent.GetComponent<ToggleHandler>().roundedGlobalBorder);
        // Also nudge the Graphic to rebuild its mesh now.
    }

    void UpdateCS()
    {
        if (cs1) cs1.enabled = true;
        if (cs2) cs2.enabled = true;
    }

    private void SetImageDirty()
    {
        if (_image) _image.SetAllDirty();
    }

    /// <summary>Forces the Image to redraw its mesh with the new corner radii.</summary>
    public void ForceUIUpdate()
    {
        if (_image)
        {
            _image.SetAllDirty();
        }
    }

    private void ApplyGlobal(float value, bool notify)
    {
        if (!HasValidRect()) return;

        var decider = GetDecider();
        if (decider != null)
        {
            float derived = ComputeDerivedFromDecider(decider);
            float maxR = GetMaxCornerRadius();
            float v = Mathf.Clamp(derived, 0f, maxR);

            borderRadius = new Vector4(v, v, v, v);
            globalBorder = v;
            cornerValues = borderRadius;
            SetImageDirty();
            if (notify) BroadcastChanged(this);
            return;
        }

        float maxRadius = GetMaxCornerRadius();
        float clamped = Mathf.Clamp(value, 0f, maxRadius);
        globalBorder = clamped;
        borderRadius = new Vector4(clamped, clamped, clamped, clamped);
        cornerValues = borderRadius;
        SetImageDirty();
        if (notify) BroadcastChanged(this);
    }

    private void RecalculateBorder()
    {
        if (!HasValidRect()) return;

        var decider = GetDecider();
        float maxR = GetMaxCornerRadius();

        if (decider != null)
        {
            float derived = ComputeDerivedFromDecider(decider);
            float v = Mathf.Clamp(derived, 0f, maxR);
            borderRadius = new Vector4(v, v, v, v);
            globalBorder = v;
            cornerValues = borderRadius;
        }
        else
        {
            if (globalBorder >= 0f)
            {
                ApplyGlobal(globalBorder, notify: false);
            }
            else
            {
                borderRadius = ClampV4(cornerValues, 0f, maxR);
                cornerValues = borderRadius;
            }
        }
    }

    private RoundedCornerUI GetDecider()
    {
        if (!decidingRectTransform) return null;
        return decidingRectTransform.GetComponent<RoundedCornerUI>();
    }

    private float ComputeDerivedFromDecider(RoundedCornerUI decider)
    {
        float deciderGlobal = decider.GlobalBorder;

        // Prefer HorizontalLayoutGroup padding.left; otherwise use VerticalLayoutGroup padding.left
        int paddingLeft = 0;
        var hlg = decidingRectTransform ? decidingRectTransform.GetComponent<HorizontalLayoutGroup>() : null;
        if (hlg != null)
        {
            paddingLeft = hlg.padding.left;
        }
        else
        {
            var vlg = decidingRectTransform ? decidingRectTransform.GetComponent<VerticalLayoutGroup>() : null;
            if (vlg != null) paddingLeft = vlg.padding.left;
        }

        float derived = deciderGlobal + paddingLeft;
        return Mathf.Max(0f, derived);
    }

    private float GetMaxCornerRadius()
    {
        var rt = (RectTransform)transform;
        float w = Mathf.Abs(rt.rect.width);
        float h = Mathf.Abs(rt.rect.height);
        if (w <= 0f || h <= 0f) return 0f;
        return Mathf.Max(0f, Mathf.Min(w, h) * 0.5f);
    }

    private static Vector4 ClampV4(Vector4 v, float min, float max)
    {
        return new Vector4(
          Mathf.Clamp(v.x, min, max),
          Mathf.Clamp(v.y, min, max),
          Mathf.Clamp(v.z, min, max),
          Mathf.Clamp(v.w, min, max)
        );
    }

    private bool HasValidRect()
    {
        var rt = (RectTransform)transform;
        return rt && rt.rect.width > 0f && rt.rect.height > 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // NEW: Apply from source (one-way)
    // ─────────────────────────────────────────────────────────────────────────────
    private void ApplyFromSourceIfAny(bool immediate)
    {
        if (followSource == null || _isApplyingFromSource) return;

        _isApplyingFromSource = true;
        try
        {
            bool changed = false;

            if (followGlobalBorder)
            {
                // Copy source GlobalBorder (already clamped on getter)
                float srcGlobal = followSource.GlobalBorder;
                if (!Mathf.Approximately(srcGlobal, GlobalBorder))
                {
                    GlobalBorder = srcGlobal; // calls ApplyGlobal + broadcasts
                    changed = true;
                }
            }

            if (followCornerValues)
            {
                Vector4 srcCorners = followSource.CurrentCornerValues;
                // Apply corner values directly (will clamp internally)
                if (CurrentCornerValues != srcCorners)
                {
                    CornerValues = srcCorners; // broadcasts
                    changed = true;
                }
            }

            if (changed)
            {
                if (immediate)
                {
                    ForceUIUpdate2();
                    _runtimeDirty = false;
                }
            }
        }
        finally
        {
            _isApplyingFromSource = false;
        }
    }

    // --- IMeshModifier ---
    public void ModifyMesh(Mesh mesh) { }

    public void ModifyMesh(VertexHelper verts)
    {
        if (!_image)
        {
            _image = GetComponent<Image>();
            if (!_image) return;
        }

        var rectTransform = (RectTransform)transform;
        if (rectTransform.rect.width <= 0f) return;

        var rect = rectTransform.rect;
        var offset = new Vector4(rect.x, rect.y, Mathf.Abs(rect.width), Mathf.Abs(rect.height));

        UIVertex vert = new UIVertex();
        for (int i = 0; i < verts.currentVertCount; i++)
        {
            verts.PopulateUIVertex(ref vert, i);

            var uv0 = vert.uv0;
            uv0.z = offset.z;
            uv0.w = offset.w;
            vert.uv0 = uv0;

            vert.uv1 = borderRadius * 0.5f;

            verts.SetUIVertex(vert, i);
        }
    }

    // --------- Runtime API helpers ---------

    /// <summary>Runtime-safe way to set all four corners at once.</summary>
    public void RuntimeSetCornerValues(Vector4 corners)
    {
        CornerValues = corners; // triggers MarkDirtyRuntime
    }

    /// <summary>Runtime-safe way to set a uniform global border (disables decider influence).</summary>
    public void RuntimeSetGlobal(float value)
    {
        decidingRectTransform = null;
        GlobalBorder = value; // triggers MarkDirtyRuntime
    }

    /// <summary>Runtime-safe way to set/replace the decider RectTransform.</summary>
    public void RuntimeSetDecider(RectTransform decider)
    {
        decidingRectTransform = decider;
        RecalculateBorder();
        SetImageDirty();
        MarkDirtyRuntime(true);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RoundedCornerUI))]
public class RoundedCornerUIEditor : Editor
{
    SerializedProperty decidingRectTransformProp;
    SerializedProperty globalBorderProp;
    SerializedProperty cornerValuesProp;

    // Follow props
    SerializedProperty followSourceProp;
    SerializedProperty followGlobalBorderProp;
    SerializedProperty followCornerValuesProp;
    SerializedProperty continuousFollowProp;

    void OnEnable()
    {
        decidingRectTransformProp = serializedObject.FindProperty("decidingRectTransform");
        globalBorderProp = serializedObject.FindProperty("globalBorder");
        cornerValuesProp = serializedObject.FindProperty("cornerValues");

        followSourceProp = serializedObject.FindProperty("followSource");
        followGlobalBorderProp = serializedObject.FindProperty("followGlobalBorder");
        followCornerValuesProp = serializedObject.FindProperty("followCornerValues");
        continuousFollowProp = serializedObject.FindProperty("continuousFollow");

        EditorApplication.update += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.update -= Repaint;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Follow section
        EditorGUILayout.LabelField("Follow Another RoundedCornerUI (one-way)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(followSourceProp, new GUIContent("Follow Source"));
        using (new EditorGUI.DisabledScope(followSourceProp.objectReferenceValue == null))
        {
            EditorGUILayout.PropertyField(followGlobalBorderProp, new GUIContent("Follow Global Border"));
            EditorGUILayout.PropertyField(followCornerValuesProp, new GUIContent("Follow Corner Values"));
            EditorGUILayout.PropertyField(continuousFollowProp, new GUIContent("Continuous Follow"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(decidingRectTransformProp);

        var t = (RoundedCornerUI)target;
        RoundedCornerUI decider = null;
        bool hasDecider = false;

        if (decidingRectTransformProp.objectReferenceValue != null)
        {
            var rt = decidingRectTransformProp.objectReferenceValue as RectTransform;
            if (rt) decider = rt.GetComponent<RoundedCornerUI>();
            hasDecider = (decider != null);
        }

        float maxR = t != null ? GetMaxCornerRadiusFor(t) : 0f;

        if (hasDecider)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Inherited: Derived = Decider.Global + Decider LayoutGroup padding.left (clamped).\nUses HorizontalLayoutGroup.padding.left if present, otherwise VerticalLayoutGroup.padding.left.", MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                int padLeft = 0;
                var hlg = (decidingRectTransformProp.objectReferenceValue as RectTransform)?.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null) padLeft = hlg.padding.left;
                else
                {
                    var vlg = (decidingRectTransformProp.objectReferenceValue as RectTransform)?.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null) padLeft = vlg.padding.left;
                }

                float deciderGlobal = decider.GlobalBorder;
                float derived = Mathf.Max(0f, deciderGlobal + padLeft);
                float clampedDerived = Mathf.Clamp(derived, 0f, maxR);

                EditorGUILayout.FloatField("Max Radius (this)", maxR);
                EditorGUILayout.FloatField("Decider Global", deciderGlobal);
                EditorGUILayout.IntField("Decider padding.left", padLeft);
                EditorGUILayout.FloatField("Derived (raw)", derived);
                EditorGUILayout.FloatField("Derived (clamped)", clampedDerived);
            }

            if (GUILayout.Button("Sync Now"))
            {
                t.GlobalBorder = t.GlobalBorder;
                EditorUtility.SetDirty(t);
            }
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("No Decider: Direct Control", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Max Radius: {maxR:0.###}");

            float g = globalBorderProp.floatValue < 0f ? 0f : globalBorderProp.floatValue;
            EditorGUI.BeginChangeCheck();
            g = EditorGUILayout.Slider(new GUIContent("Global Border"), Mathf.Max(0f, g), 0f, maxR);
            if (EditorGUI.EndChangeCheck())
            {
                globalBorderProp.floatValue = Mathf.Clamp(g, 0f, maxR);
            }

            Vector4 v = cornerValuesProp.vector4Value;
            EditorGUI.BeginChangeCheck();
            v.x = EditorGUILayout.Slider("Top Left", Mathf.Clamp(v.x, 0f, maxR), 0f, maxR);
            v.y = EditorGUILayout.Slider("Top Right", Mathf.Clamp(v.y, 0f, maxR), 0f, maxR);
            v.z = EditorGUILayout.Slider("Bottom Right", Mathf.Clamp(v.z, 0f, maxR), 0f, maxR);
            v.w = EditorGUILayout.Slider("Bottom Left", Mathf.Clamp(v.w, 0f, maxR), 0f, maxR);
            if (EditorGUI.EndChangeCheck())
            {
                v.x = Mathf.Clamp(v.x, 0f, maxR);
                v.y = Mathf.Clamp(v.y, 0f, maxR);
                v.z = Mathf.Clamp(v.z, 0f, maxR);
                v.w = Mathf.Clamp(v.w, 0f, maxR);
                cornerValuesProp.vector4Value = v;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static float GetMaxCornerRadiusFor(RoundedCornerUI ui)
    {
        var rt = (RectTransform)ui.transform;
        float w = Mathf.Abs(rt.rect.width);
        float h = Mathf.Abs(rt.rect.height);
        if (w <= 0f || h <= 0f) return 0f;
        return Mathf.Max(0f, Mathf.Min(w, h) * 0.5f);
    }
}
#endif
