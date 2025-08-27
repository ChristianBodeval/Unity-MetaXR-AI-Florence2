using UnityEngine;
using TMPro;
using Meta.XR;
using Meta.XR.BuildingBlocks;


namespace PresentFutures.XRAI.Spatial
{
    [RequireComponent(typeof(Collider))]
    public class SpatialLabel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private string _name;     // Exposed to inspector
        [SerializeField] private TMP_Text text;

        [Header("Aim & Remove")]
        [Tooltip("How far the player can target labels for removal.")]
        [SerializeField] private float maxAimDistance = 10f;

        [Tooltip("Layers that can be aimed at. Put your SpatialLabel objects on this layer (or 'Default').")]
        [SerializeField] private LayerMask aimLayerMask = ~0;

        [Tooltip("Draw a small gizmo when this label is being aimed at.")]
        [SerializeField] private bool drawAimGizmo = true;

        private static Camera _mainCam;
        private Collider _col;
        private bool _isAimedAt;
        private OVRSpatialAnchor anchor;

        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _name = value;
                    if (text != null) text.text = _name;
                }
            }
        }

        private void Awake()
        {
            anchor = GetComponent<OVRSpatialAnchor>();
            _col = GetComponent<Collider>();
            if (_col == null)
            {
                Debug.LogWarning("SpatialLabel requires a Collider for aiming/removal. Adding a BoxCollider by default.", this);
                _col = gameObject.AddComponent<BoxCollider>();
            }

            if (_mainCam == null) _mainCam = Camera.main;

            
        }

        private void OnValidate()
        {
            // Ensures updates in Inspector reflect immediately
            if (!string.IsNullOrEmpty(_name) && text != null)
            {
                text.text = _name;
            }
        }

        private void Start()
        {
            // Register with the manager as soon as it's spawned
            if (SpatialLabelManager.Instance != null)
            {
                SpatialLabelManager.Instance.RegisterLabel(this);
            }
            else
            {
                Debug.LogWarning("SpatialLabelManager instance not found!");
            }

            // Ensure text is initialized
            if (!string.IsNullOrEmpty(_name) && text != null)
            {
                text.text = _name;
            }

            if (_mainCam == null) _mainCam = Camera.main;
        }

        private void Update()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            Ray ray = new Ray(_mainCam.transform.position, _mainCam.transform.forward);
            bool wasAimedAt = _isAimedAt;
            _isAimedAt = false;

            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == _col)
                {
                    _isAimedAt = true;

                    if (!wasAimedAt) Debug.Log($"[{name}] Aimed at.");

                    if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
                    {
                        
                        Remove();
                    }
                    if (Input.GetMouseButtonDown(0))
                    {
                        Remove();
                    }
                }
            }

            if (wasAimedAt && !_isAimedAt)
            {
                Debug.Log($"[{name}] No longer aimed at.");
            }
        }

        /// <summary>
        /// Removes this label from the scene and unregisters it from the manager.
        /// </summary>
        public void Remove()
        {
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.UnsaveAnchor(anchor);
            }

            Debug.Log($"[{name}] Removed.");
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.UnsaveAnchor(anchor);
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawAimGizmo || !_isAimedAt) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, 0.08f);
        }
    }
}
