using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ControllerUI : MonoBehaviour
{
    public enum ButtonType
    {
        None,
        TriggerL,
        TriggerR,
        SecondaryL,
        SecondaryR,
        ThumbstickL,
        ThumbstickR,
        BurgerMenu,
        MetaButton,
        X,
        Y,
        B,
        A
    }

    [System.Serializable]
    public struct ButtonSprites
    {
        public SpriteRenderer outline; // stroke/line
        public SpriteRenderer fill;    // background

        private Image outlineImg => outline ? outline.GetComponent<Image>() : null;
        private Image fillImg => fill ? fill.GetComponent<Image>() : null;

        public void Set(Color outlineColor, Color fillColor, bool useUIImages)
        {
            if (useUIImages)
            {
                if (outlineImg) outlineImg.color = outlineColor;
                if (fillImg) fillImg.color = fillColor;
            }
            else
            {
                if (outline) outline.color = outlineColor;
                if (fill) fill.color = fillColor;
            }
        }

        public void ResetTo(Color outlineColor, Color fillColor, bool useUIImages)
            => Set(outlineColor, fillColor, useUIImages);

        public void UpdateEnabledState(bool useUIImages)
        {
            if (outline)
            {
                outline.enabled = !useUIImages;
                if (outlineImg) outlineImg.enabled = useUIImages;
            }
            if (fill)
            {
                fill.enabled = !useUIImages;
                if (fillImg) fillImg.enabled = useUIImages;
            }
        }
    }

    [Header("Select which button to highlight")]
    public ButtonType highlighted = ButtonType.None;

    [Header("Values")]
    public float distanceBetweenControllers;

    //TODO Sprites need fixing
    public bool useUIImages = false;
    public bool isUsingOneController;

    [Header("Colors")]
    public Color outlineDefault = Color.white;
    public Color fillDefault = new Color(1f, 1f, 1f, 0.15f);
    public Color outlineHighlight = new Color(0f, 0.8f, 0.9f, 1f);
    public Color fillHighlight = new Color(0f, 0.8f, 0.9f, 0.35f);

    [Header("LEFT")]
    public ButtonSprites TriggerL;
    public ButtonSprites SecondaryL;
    public ButtonSprites ThumbstickL;
    public ButtonSprites BurgerMenu;
    public ButtonSprites X;
    public ButtonSprites Y;

    [Header("RIGHT")]
    public ButtonSprites TriggerR;
    public ButtonSprites SecondaryR;
    public ButtonSprites ThumbstickR;
    public ButtonSprites MetaButton;
    public ButtonSprites A;
    public ButtonSprites B;

    [Header("Controller Bodies & Outlines")]
    public SpriteRenderer ControllerL;
    public SpriteRenderer ControllerR;
    public SpriteRenderer ControllerOutlineL;
    public SpriteRenderer ControllerOutlineR;

    [Header("Layout Logic")]
    public GameObject leftController;
    public GameObject rightController;
    public GameObject controllerHolder;

    void Awake() => Apply();
    void OnValidate() => Apply();

    public void SetHighlight(ButtonType type)
    {
        highlighted = type;
        Apply();
    }


    private void Apply()
    {
        // toggle enable/disable between SR and UI Images
        UpdateEnabledStates();

        // reset to defaults
        TriggerL.ResetTo(outlineDefault, fillDefault, useUIImages);
        TriggerR.ResetTo(outlineDefault, fillDefault, useUIImages);
        SecondaryL.ResetTo(outlineDefault, fillDefault, useUIImages);
        SecondaryR.ResetTo(outlineDefault, fillDefault, useUIImages);
        ThumbstickL.ResetTo(outlineDefault, fillDefault, useUIImages);
        ThumbstickR.ResetTo(outlineDefault, fillDefault, useUIImages);
        BurgerMenu.ResetTo(outlineDefault, fillDefault, useUIImages);
        MetaButton.ResetTo(outlineDefault, fillDefault, useUIImages);
        X.ResetTo(outlineDefault, fillDefault, useUIImages);
        Y.ResetTo(outlineDefault, fillDefault, useUIImages);
        B.ResetTo(outlineDefault, fillDefault, useUIImages);
        A.ResetTo(outlineDefault, fillDefault, useUIImages);

        SetVisualColor(ControllerL, fillDefault);
        SetVisualColor(ControllerR, fillDefault);
        SetVisualColor(ControllerOutlineL, outlineDefault);
        SetVisualColor(ControllerOutlineR, outlineDefault);

        // highlight the selected button
        switch (highlighted)
        {
            case ButtonType.TriggerL: TriggerL.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.TriggerR: TriggerR.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.SecondaryL: SecondaryL.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.SecondaryR: SecondaryR.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.ThumbstickL: ThumbstickL.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.ThumbstickR: ThumbstickR.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.BurgerMenu: BurgerMenu.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.MetaButton: MetaButton.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.X: X.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.Y: Y.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.B: B.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.A: A.Set(outlineHighlight, fillHighlight, useUIImages); break;
            case ButtonType.None:
            default: break;
        }

        // layout logic
        if (isUsingOneController)
        {
            leftController.transform.localPosition = Vector3.zero;
            rightController.transform.localPosition = Vector3.zero;

            if (IsLeftSide(highlighted))
            {
                if (leftController) leftController.SetActive(true);
                if (rightController) rightController.SetActive(false);
            }
            else
            {
                if (leftController) leftController.SetActive(false);
                if (rightController) rightController.SetActive(true);
            }
        }
        else
        {

            leftController.transform.localPosition = new Vector3(-distanceBetweenControllers, 0, 0);
            rightController.transform.localPosition = new Vector3(distanceBetweenControllers, 0, 0);

            if (leftController) { leftController.SetActive(true); }
            if (rightController) { rightController.SetActive(true); }
        }
    }

    private bool IsLeftSide(ButtonType t)
    {
        return t == ButtonType.TriggerL ||
               t == ButtonType.SecondaryL ||
               t == ButtonType.ThumbstickL ||
               t == ButtonType.BurgerMenu ||
               t == ButtonType.X ||
               t == ButtonType.Y;
    }

    private void SetVisualColor(SpriteRenderer sr, Color color)
    {
        if (!sr) return;

        if (useUIImages)
        {
            if (sr.TryGetComponent<Image>(out var img))
                img.color = color;
        }
        else
        {
            sr.color = color;
        }
    }

    private void UpdateEnabledStates()
    {
        TriggerL.UpdateEnabledState(useUIImages);
        TriggerR.UpdateEnabledState(useUIImages);
        SecondaryL.UpdateEnabledState(useUIImages);
        SecondaryR.UpdateEnabledState(useUIImages);
        ThumbstickL.UpdateEnabledState(useUIImages);
        ThumbstickR.UpdateEnabledState(useUIImages);
        BurgerMenu.UpdateEnabledState(useUIImages);
        MetaButton.UpdateEnabledState(useUIImages);
        X.UpdateEnabledState(useUIImages);
        Y.UpdateEnabledState(useUIImages);
        B.UpdateEnabledState(useUIImages);
        A.UpdateEnabledState(useUIImages);

        ToggleRenderer(ControllerL, useUIImages);
        ToggleRenderer(ControllerR, useUIImages);
        ToggleRenderer(ControllerOutlineL, useUIImages);
        ToggleRenderer(ControllerOutlineR, useUIImages);
    }

    private void ToggleRenderer(SpriteRenderer sr, bool useUI)
    {
        if (!sr) return;

        if (sr.TryGetComponent<Image>(out var img))
        {
            sr.enabled = !useUI;
            img.enabled = useUI;
        }
        else
        {
            sr.enabled = !useUI; // if no Image exists, just toggle SR
        }
    }
}
