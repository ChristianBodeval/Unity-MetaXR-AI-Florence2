using UnityEngine;

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

        public void Set(Color outlineColor, Color fillColor)
        {
            if (outline) outline.color = outlineColor;
            if (fill) fill.color = fillColor;
        }

        public void ResetTo(Color outlineColor, Color fillColor) => Set(outlineColor, fillColor);
    }

    [Header("Select which button to highlight")]
    public ButtonType highlighted = ButtonType.None;

    [Header("Colors")]
    public Color outlineDefault = Color.white;
    public Color fillDefault = new Color(1f, 1f, 1f, 0.15f);
    public Color outlineHighlight = new Color(0f, 0.8f, 0.9f, 1f);
    public Color fillHighlight = new Color(0f, 0.8f, 0.9f, 0.35f);

    [Header("LEFT")]
    public ButtonSprites TriggerL;      // LeftTrigger / LeftTriggerFill
    public ButtonSprites SecondaryL;    // LeftSecondary / LeftSecondaryFill
    public ButtonSprites ThumbstickL;   // ThumpStick / ThumpStickFill
    public ButtonSprites BurgerMenu;    // BurgerMenu / BurgerMenuFill
    public ButtonSprites X;             // X-button / X-buttonFill
    public ButtonSprites Y;             // Y-button / Y-buttonFill

    [Header("RIGHT")]
    public ButtonSprites TriggerR;      // RightTrigger / RightTriggerFill
    public ButtonSprites SecondaryR;    // RightSecondary / RightSecondaryFill
    public ButtonSprites ThumbstickR;   // ThumpStickRight / ThumpStickRightFill
    public ButtonSprites MetaButton;    // MetaButton / MetaButtonFill
    public ButtonSprites A;             // A-button / A-buttonFill
    public ButtonSprites B;             // B-button / B-buttonFill

    public SpriteRenderer ControllerL;             
    public SpriteRenderer ControllerR;             
    public SpriteRenderer ControllerOutlineL;             
    public SpriteRenderer ControllerOutlineR;             


    void Awake() => Apply();
    void OnValidate() => Apply();


    public void SetHighlight(ButtonType type)
    {
        highlighted = type;
        Apply();
    }

    private void Apply()
    {
        // 1) reset all to defaults
        TriggerL.ResetTo(outlineDefault, fillDefault);
        TriggerR.ResetTo(outlineDefault, fillDefault);
        SecondaryL.ResetTo(outlineDefault, fillDefault);
        SecondaryR.ResetTo(outlineDefault, fillDefault);
        ThumbstickL.ResetTo(outlineDefault, fillDefault);
        ThumbstickR.ResetTo(outlineDefault, fillDefault);
        BurgerMenu.ResetTo(outlineDefault, fillDefault);
        MetaButton.ResetTo(outlineDefault, fillDefault);
        X.ResetTo(outlineDefault, fillDefault);
        Y.ResetTo(outlineDefault, fillDefault);
        B.ResetTo(outlineDefault, fillDefault);
        A.ResetTo(outlineDefault, fillDefault);

        ControllerL.color = fillDefault;
        ControllerR.color = fillDefault;
        ControllerOutlineL.color = outlineDefault;
        ControllerOutlineR.color = outlineDefault;
        // 2) highlight the selected one
        switch (highlighted)
        {
            case ButtonType.TriggerL: TriggerL.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.TriggerR: TriggerR.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.SecondaryL: SecondaryL.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.SecondaryR: SecondaryR.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.ThumbstickL: ThumbstickL.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.ThumbstickR: ThumbstickR.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.BurgerMenu: BurgerMenu.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.MetaButton: MetaButton.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.X: X.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.Y: Y.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.B: B.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.A: A.Set(outlineHighlight, fillHighlight); break;
            case ButtonType.None:
            default: break;
        }
    }
}
