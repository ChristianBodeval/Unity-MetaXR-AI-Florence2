using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PresentFutures.XRAI.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class ControllerIconImage : MonoBehaviour
    {
        [Tooltip("Relative to a Resources/ folder. Example: \"ControllerInputIcon\"")]
        public string resourcesFolder = "ControllerInputIcon";

        [Tooltip("Optional. If left empty, will use the Image on this GameObject.")]
        public Image targetImage;

        [Tooltip("The name of the sprite chosen from the dropdown.")]
        public string selectedSpriteName;

        // cached for editor/runtime refresh
        [HideInInspector] public Sprite[] _sprites = new Sprite[0];

        void Reset()
        {
            targetImage = GetComponent<Image>();
        }

        void OnEnable()
        {
            if (!targetImage) targetImage = GetComponent<Image>();
            RefreshSprites();
            ApplySelected();
        }

        void OnValidate()
        {
            if (!targetImage) targetImage = GetComponent<Image>();
            RefreshSprites();
            ApplySelected();
        }

        public void RefreshSprites()
        {
            _sprites = Resources.LoadAll<Sprite>(resourcesFolder);
        }

        public void ApplySelected()
        {
            if (!targetImage) return;
            if (_sprites == null || _sprites.Length == 0)
            {
                targetImage.sprite = null;
                return;
            }

            if (string.IsNullOrEmpty(selectedSpriteName))
            {
                // default to first
                targetImage.sprite = _sprites[0];
                selectedSpriteName = _sprites[0].name;
                return;
            }

            foreach (var s in _sprites)
            {
                if (s && s.name == selectedSpriteName)
                {
                    targetImage.sprite = s;
                    return;
                }
            }

            // if not found, fall back to first
            targetImage.sprite = _sprites[0];
            selectedSpriteName = _sprites[0].name;
        }
    }
}
