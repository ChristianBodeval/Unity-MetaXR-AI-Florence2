using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using NaughtyAttributes;
using PresentFutures.XRAI.Florence;
using PassthroughCameraSamples;
using Meta.XR;

namespace PresentFutures.XRAI.Florence
{
    /// <summary>
    /// Builds 3D plane-aligned OBBs for the CURRENT 2D detections drawn by Florence2Controller.
    /// It samples a small grid of rays INSIDE each 2D bbox (using a fresh passthrough snapshot),
    /// gathers 3D hits via EnvironmentRaycastManager, fits an oriented box, and spawns a cube.
    ///
    /// Requirements:
    /// - Florence2Controller draws 2D boxes into boundingBoxContainer (names start with "BBox_")
    /// - obbPrefab is a UNIT CUBE (1,1,1) centered at origin.
    /// - EnvironmentRaycastManager available in scene (can be taken from controller).
    /// </summary>
    public class Florence2OBBBuilderStandalone : MonoBehaviour
    {
        [Header("References")]
        [Required] public Florence2Controller controller;     // Drag your existing controller
        [Required] public GameObject obbPrefab;               // Unit cube, pivot centered at origin

        [Tooltip("If not set, will use controller.environmentRaycastManager")]
        public EnvironmentRaycastManager environmentRaycastManager;

        [Header("OBB Fitting")]
        [MinValue(2)] public int samplesGrid = 8;             // NxN rays per bbox
        [MinValue(0.0f)] public float minThickness = 0.04f;   // meters; fallback if surface is thin

        [Header("Anchors (optional)")]
        public bool alsoCreateSpatialAnchor = true;

        [Header("Label (optional)")]
        public bool addFloatingLabel = true;
        public float labelHeightOffset = 0.1f;                // meters above OBB center
        public float labelMaxWidth = 0.5f;
        public float labelMaxHeight = 0.2f;
        public float labelBaseFontSize = 0.12f;

        [Header("Debug")]
        public bool logInfo = false;

        private void Reset()
        {
            if (!controller) controller = FindObjectOfType<Florence2Controller>();
            if (!environmentRaycastManager && controller)
                environmentRaycastManager = controller.environmentRaycastManager;
        }

        [Button("Build OBBs From Latest Detections")]
        public void BuildOBBsFromLatestDetections()
        {
            if (!controller)
            {
                Debug.LogError("[OBB] No Florence2Controller assigned.");
                return;
            }
            if (!obbPrefab)
            {
                Debug.LogError("[OBB] Please assign a unit-cube obbPrefab (pivot centered).");
                return;
            }
            if (!environmentRaycastManager)
            {
                environmentRaycastManager = controller.environmentRaycastManager;
                if (!environmentRaycastManager)
                {
                    Debug.LogError("[OBB] No EnvironmentRaycastManager available.");
                    return;
                }
            }

            StartCoroutine(Co_BuildOBBsNow());
        }

        private IEnumerator Co_BuildOBBsNow()
        {
            // 1) Take our own snapshot (fresh intrinsics/pose)
            var snap = PassthroughCameraOffline.CaptureSnapshot(PassthroughCameraEye.Left, savedFrame: null);

            // 2) Ensure controller UI is materialized
            yield return null;

            var container = controller.boundingBoxContainer;
            var img = controller.resultImage;
            if (!container || !img || img.texture == null)
            {
                Debug.LogError("[OBB] Controller UI not ready (container/resultImage/texture missing).");
                yield break;
            }

            RectTransform imgRect = img.rectTransform;
            float uiW = imgRect.rect.width;
            float uiH = imgRect.rect.height;
            float texW = img.texture.width;
            float texH = img.texture.height;

            // 3) Gather all controller boxes
            var boxes = new List<(RectTransform rt, string label)>();
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!child) continue;
                if (!child.name.StartsWith("BBox_")) continue;

                var rt = child as RectTransform;
                if (!rt) continue;

                var label = child.GetComponentInChildren<TextMeshProUGUI>()?.text ?? "object";
                boxes.Add((rt, label));
            }

            if (boxes.Count == 0)
            {
                if (logInfo) Debug.Log("[OBB] No 2D boxes found.");
                yield break;
            }

            // 4) Convert each UI rect -> texture space -> sample rays -> fit OBB
            float scaleX = uiW / texW;
            float scaleY = uiH / texH;

            foreach (var (rt, label) in boxes)
            {
                // Controller placed boxes with:
                // anchoredPosition = (x, -y)  &  sizeDelta = (w, h)  after scaling by (scaleX, scaleY)
                float x_tex = rt.anchoredPosition.x / scaleX;
                float y_tex = -rt.anchoredPosition.y / scaleY;
                float w_tex = rt.sizeDelta.x / scaleX;
                float h_tex = rt.sizeDelta.y / scaleY;

                var pts = new List<Vector3>();
                var norms = new List<Vector3>();

                int grid = Mathf.Max(2, samplesGrid);
                for (int iy = 0; iy < grid; iy++)
                    for (int ix = 0; ix < grid; ix++)
                    {
                        float px = x_tex + (ix + 0.5f) * w_tex / grid;
                        float py = y_tex + (iy + 0.5f) * h_tex / grid;

                        float u = Mathf.Clamp01(px / texW);
                        float v = 1f - Mathf.Clamp01(py / texH); // invert Y to top-left origin

                        var ray = PassthroughCameraOffline.NormalizedToRayInWorld(snap, new Vector2(u, v));
                        if (environmentRaycastManager.Raycast(ray, out EnvironmentRaycastHit hit))
                        {
                            pts.Add(hit.point);
                            norms.Add(hit.normal);
                        }
                    }

                if (pts.Count < 4)
                {
                    if (logInfo) Debug.Log($"[OBB] '{label}': insufficient hits ({pts.Count}).");
                    continue;
                }

                if (TryFitPlaneAlignedOBB(pts, norms, out var center, out var rotation, out var size))
                {
                    var go = Instantiate(obbPrefab, center, rotation);
                    go.transform.localScale = size;
                    go.name = $"OBB_{label}";

                    if (addFloatingLabel)
                    {
                        var txtHolder = new GameObject("Label");
                        txtHolder.transform.SetParent(go.transform, false);
                        txtHolder.transform.localPosition = new Vector3(0, size.y * 0.5f + labelHeightOffset, 0);

                        var tm = txtHolder.AddComponent<TextMeshPro>();
                        tm.text = label;
                        tm.fontSize = labelBaseFontSize;
                        tm.enableAutoSizing = true;
                        tm.alignment = TextAlignmentOptions.Center;
                        tm.rectTransform.sizeDelta = new Vector2(labelMaxWidth, labelMaxHeight);
                    }

                    if (alsoCreateSpatialAnchor && controller.spatialAnchorPrefab)
                    {
                        var lookRot = Quaternion.LookRotation(Camera.main.transform.position - center, Vector3.up);
                        SpatialAnchorManager.Instance.CreateSpatialAnchor(
                            controller.spatialAnchorPrefab,
                            center,
                            lookRot,
                            label,
                            Vector3.up,
                            null
                        );
                    }

                    if (logInfo) Debug.Log($"[OBB] '{label}' size={size} center={center}");
                }
                else if (logInfo)
                {
                    Debug.Log($"[OBB] '{label}': OBB fit failed.");
                }

                yield return null; // keep frame responsive
            }
        }

        /// <summary>
        /// Fits a plane-aligned OBB using average normal for plane orientation and min/max extents in tangent basis.
        /// </summary>
        private bool TryFitPlaneAlignedOBB(
            List<Vector3> points,
            List<Vector3> normals,
            out Vector3 center,
            out Quaternion rotation,
            out Vector3 size)
        {
            center = default;
            rotation = default;
            size = default;

            if (points == null || points.Count < 4) return false;

            // 1) Centroid
            Vector3 centroid = Vector3.zero;
            foreach (var p in points) centroid += p;
            centroid /= points.Count;

            // 2) Average normal (fallback up if degenerate)
            Vector3 n = Vector3.zero;
            if (normals != null && normals.Count > 0)
            {
                foreach (var no in normals) n += no.normalized;
            }
            n = n == Vector3.zero ? Vector3.up : n.normalized;

            // 3) Tangent basis (t, b, n)
            Vector3 temp = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.8f ? Vector3.right : Vector3.up;
            Vector3 t = Vector3.Normalize(Vector3.Cross(temp, n));
            Vector3 b = Vector3.Normalize(Vector3.Cross(n, t));

            // 4) Project points to basis & take bounds
            float minT = float.PositiveInfinity, maxT = float.NegativeInfinity;
            float minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
            float minN = float.PositiveInfinity, maxN = float.NegativeInfinity;

            foreach (var p in points)
            {
                Vector3 d = p - centroid;
                float pt = Vector3.Dot(d, t);
                float pb = Vector3.Dot(d, b);
                float pn = Vector3.Dot(d, n);

                if (pt < minT) minT = pt; if (pt > maxT) maxT = pt;
                if (pb < minB) minB = pb; if (pb > maxB) maxB = pb;
                if (pn < minN) minN = pn; if (pn > maxN) maxN = pn;
            }

            float thickness = Mathf.Max(minThickness, (maxN - minN));
            Vector3 sz = new Vector3(maxT - minT, maxB - minB, thickness);

            if (sz.x <= Mathf.Epsilon || sz.y <= Mathf.Epsilon)
                return false;

            // 5) Center offset in world (midpoints in each axis)
            Vector3 centerOffset =
                ((minT + maxT) * 0.5f) * t +
                ((minB + maxB) * 0.5f) * b +
                ((minN + maxN) * 0.5f) * n;

            center = centroid + centerOffset;

            // Local axes: right=t, up=b, forward=n
            rotation = Quaternion.LookRotation(n, b);
            size = sz;

            return true;
        }
    }
}
