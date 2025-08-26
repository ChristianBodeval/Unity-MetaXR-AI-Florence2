using System.Collections.Generic;
using UnityEngine;

namespace PresentFutures.XRAI.Spatial
{
    public class SpatialLabelManager : MonoBehaviour
    {
        public static SpatialLabelManager Instance { get; private set; }

        private List<SpatialLabel> labels = new List<SpatialLabel>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Add a label to the manager
        /// </summary>
        public void RegisterLabel(SpatialLabel label)
        {
            if (!labels.Contains(label))
            {
                labels.Add(label);
                Debug.Log($"Label registered: {label.name}");
            }
        }

        /// <summary>
        /// Optional: Remove a label
        /// </summary>
        public void UnregisterLabel(SpatialLabel label)
        {
            if (labels.Contains(label))
            {
                labels.Remove(label);
                Debug.Log($"Label unregistered: {label.name}");
            }
        }

        public List<SpatialLabel> GetAllLabels()
        {
            return labels;
        }
    }
}