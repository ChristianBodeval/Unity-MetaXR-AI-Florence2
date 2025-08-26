using UnityEngine;
using TMPro;

namespace PresentFutures.XRAI.Spatial
{
    public class SpatialLabel : MonoBehaviour
    {
        [SerializeField] private string _name; // Exposed to inspector
        [SerializeField] private TMP_Text text;

        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _name = value;
                    if (text != null)
                    {
                        text.text = _name;
                    }
                }
            }
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
        }

        private void OnDestroy()
        {
            // Optional: Remove from manager when destroyed
            if (SpatialLabelManager.Instance != null)
            {
                SpatialLabelManager.Instance.UnregisterLabel(this);
            }
        }
    }
}
