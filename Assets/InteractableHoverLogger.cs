using Oculus.Interaction;
using UnityEngine;

public class InteractableHoverLogger : MonoBehaviour
{
    [SerializeField] private InteractableUnityEventWrapper wrapper;

    void OnEnable()
    {
        wrapper.WhenHover.AddListener(OnHover);
        wrapper.WhenUnhover.AddListener(OnUnhover);
        wrapper.WhenSelect.AddListener(OnSelect);
        wrapper.WhenUnselect.AddListener(OnUnselect);
    }

    void OnDisable()
    {
        wrapper.WhenHover.RemoveListener(OnHover);
        wrapper.WhenUnhover.RemoveListener(OnUnhover);
        wrapper.WhenSelect.RemoveListener(OnSelect);
        wrapper.WhenUnselect.RemoveListener(OnUnselect);
    }

    private void OnHover() => Debug.Log($"{name} hovered");
    private void OnUnhover() => Debug.Log($"{name} unhovered");
    private void OnSelect() => Debug.Log($"{name} selected");
    private void OnUnselect() => Debug.Log($"{name} unselected");
}
