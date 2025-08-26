using UnityEngine;
using UnityEngine.UI; // <— for RawImage

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CameraViewPyramid : MonoBehaviour
{
    public enum EyeMode { Mono, Left, Right, Center }

    [Header("Camera")]
    [Tooltip("Leave empty to auto-find the child Camera in the Meta XR Camera Rig.")]
    public Camera targetCamera;

    [Header("Size")]
    [Min(0.001f)] public float height = 5f;   // Distance from camera to base
    public bool matchFarClip = false;         // Base at far clip if true
    public EyeMode eye = EyeMode.Left;        // Left to match Meta left camera

    [Header("Passthrough Image Shape")]
    [Tooltip("If true, crops the frustum to match the image aspect (width/height).")]
    public bool useImageAspect = true;

    [Tooltip("Optional: assign the UI RawImage that displays the left-eye frame.")]
    public RawImage uiSource;

    [Tooltip("Optional: assign the LEFT passthrough texture directly.")]
    public Texture imageSource;

    [Tooltip("Fallback aspect if no UI/Texture is provided: width/height (e.g., 4:3 = 1.3333).")]
    [Min(0.01f)] public float imageAspect = 1280f / 960f; // default to 4:3

    [Tooltip("Keep the image rectangle centered inside the camera view.")]
    public bool centerCrop = true;

    [Header("Perf")]
    public bool liveUpdate = true;

    [Header("Gizmo")]
    public bool drawGizmo = true;

    Mesh _mesh;

    void OnEnable() { EnsureComponents(); Rebuild(); }
    void Reset() { EnsureComponents(); Rebuild(); }
    void OnValidate() { EnsureComponents(); Rebuild(); }
    void Update() { if (liveUpdate) Rebuild(); }

    void EnsureComponents()
    {
        var mf = GetComponent<MeshFilter>();
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "CameraViewPyramid_Mesh" };
            _mesh.MarkDynamic();
        }
        if (mf.sharedMesh != _mesh) mf.sharedMesh = _mesh;

        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>(true);
            if (targetCamera == null) targetCamera = Camera.main;
        }
    }

    public void Rebuild()
    {
        if (targetCamera == null || _mesh == null) return;

        float d = matchFarClip ? targetCamera.farClipPlane : height;
        if (d < 0.001f) d = 0.001f;

        transform.position = targetCamera.transform.position;
        transform.rotation = targetCamera.transform.rotation;
        transform.localScale = Vector3.one;

        Vector3 bl, tl, tr, br;
        GetCornersAtDistance(d, out bl, out tl, out tr, out br);

        Vector3 apex = Vector3.zero;
        Vector3[] verts = { apex, bl, tl, tr, br };

        int[] tris =
        {
            0,1,2,  0,2,3,  0,3,4,  0,4,1, // sides
            1,2,3,  1,3,4                 // base
        };

        Vector2[] uvs =
        {
            new Vector2(0.5f,0),
            new Vector2(0,0),
            new Vector2(0,1),
            new Vector2(1,1),
            new Vector2(1,0)
        };

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.triangles = tris;
        _mesh.uv = uvs;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    Rect GetImageViewportRect()
    {
        if (!useImageAspect) return new Rect(0f, 0f, 1f, 1f);

        float a_img = imageAspect;

        // 1) Prefer the UI RawImage rect (e.g., 1280×960 from your screenshot)
        if (uiSource != null)
        {
            var rt = uiSource.rectTransform.rect;
            if (rt.height > 0f) a_img = rt.width / rt.height;
        }
        // 2) Else try the texture
        else if (imageSource != null && imageSource.height > 0)
        {
            a_img = (float)imageSource.width / imageSource.height;
        }

        a_img = Mathf.Max(0.01f, a_img);
        float a_cam = Mathf.Max(0.01f, targetCamera.aspect);

        if (a_img <= a_cam)
        {
            // narrower than camera -> pillarbox (reduce width)
            float w = a_img / a_cam;
            float x = centerCrop ? (1f - w) * 0.5f : 0f;
            return new Rect(x, 0f, w, 1f);
        }
        else
        {
            // wider than camera -> letterbox (reduce height)
            float h = a_cam / a_img;
            float y = centerCrop ? (1f - h) * 0.5f : 0f;
            return new Rect(0f, y, 1f, h);
        }
    }

    void GetCornersAtDistance(float d, out Vector3 bl, out Vector3 tl, out Vector3 tr, out Vector3 br)
    {
        var corners = new Vector3[4];
        Rect rect = GetImageViewportRect();

        if (!targetCamera.stereoEnabled || eye == EyeMode.Mono)
        {
            targetCamera.CalculateFrustumCorners(rect, d, Camera.MonoOrStereoscopicEye.Mono, corners);
        }
        else if (eye == EyeMode.Left)
        {
            targetCamera.CalculateFrustumCorners(rect, d, Camera.MonoOrStereoscopicEye.Left, corners);
        }
        else if (eye == EyeMode.Right)
        {
            targetCamera.CalculateFrustumCorners(rect, d, Camera.MonoOrStereoscopicEye.Right, corners);
        }
        else
        {
            var L = new Vector3[4];
            var R = new Vector3[4];
            targetCamera.CalculateFrustumCorners(rect, d, Camera.MonoOrStereoscopicEye.Left, L);
            targetCamera.CalculateFrustumCorners(rect, d, Camera.MonoOrStereoscopicEye.Right, R);
            for (int i = 0; i < 4; i++) corners[i] = (L[i] + R[i]) * 0.5f;
        }

        // Unity order: BL, TL, TR, BR
        bl = corners[0];
        tl = corners[1];
        tr = corners[2];
        br = corners[3];
    }

    void OnDrawGizmos()
    {
        if (!drawGizmo || targetCamera == null || _mesh == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireMesh(_mesh);
    }
}
