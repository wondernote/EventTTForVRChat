
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChipListener : UdonSharpBehaviour
{
    [SerializeField] private GameObject check;
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image chipImage;
    [SerializeField] private TextMeshProUGUI textLabel;
    private FlowWrapLayout flowWrapLayout;
    private LeftWingController wingController;
    private int id;
    private int kind;
    private bool isResetChip;
    private Color chipColor;
    private bool isSelected = false;
    private const float BG_ON = 1.0f, BG_OFF = 0.29f, BG_HOVER_SEL = 0.80f;
    private const float TXT_ON = 1.0f, TXT_OFF = 0.40f, TXT_HOVER_SEL = 0.90f;
    private Color baseBgColor;
    private Color baseTxtColor;

    public void SetInitInfo(LeftWingController _wingController, int _id, int _kind, FlowWrapLayout _flowWrapLayout, Color _chipColor)
    {
        wingController = _wingController;
        id = _id;
        kind = _kind;
        flowWrapLayout = _flowWrapLayout;
        chipColor = _chipColor;

        baseBgColor = chipColor;
        baseTxtColor = textLabel.color;

        chipImage.color = WithAlpha(baseBgColor, BG_OFF);
        textLabel.color = WithAlpha(baseTxtColor, TXT_OFF);

        isResetChip = (id == -1) && (flowWrapLayout == null);
        if (isResetChip) SetToggleState(true);
    }

    public void OnToggleChanged()
    {
        if (wingController != null && !IsResetChipOn()) wingController.PlayClickSound();

        bool on = toggle.isOn;
        isSelected = on;
        UpdateVisual(on);

        RectTransform chipRect = (RectTransform)transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(chipRect);

        if(!isResetChip && flowWrapLayout != null) flowWrapLayout.Reflow();

        if (wingController != null) wingController.OnChipChangedById(kind, id, on);
    }

    public void SetOnWithoutNotify(bool on)
    {
        SetToggleState(on);
    }

    public bool GetToggleState()
    {
        return toggle.isOn;
    }

    private void SetToggleState(bool on)
    {
        toggle.SetIsOnWithoutNotify(on);
        isSelected = on;
        UpdateVisual(on);
    }

    private void UpdateVisual(bool on)
    {
        check.SetActive(on);
        chipImage.color = WithAlpha(baseBgColor, isSelected ? BG_ON : BG_OFF);
        textLabel.color = WithAlpha(baseTxtColor, isSelected ? TXT_ON : TXT_OFF);
    }

    public void OnPointerEnter()
    {
        if (!IsResetChipOn()) if (wingController != null) wingController.PlayHoverSound();

        if(isSelected){
            if(!isResetChip){
                chipImage.color = WithAlpha(baseBgColor, BG_HOVER_SEL);
                textLabel.color = WithAlpha(baseTxtColor, TXT_HOVER_SEL);
            }
        } else {
            chipImage.color = WithAlpha(baseBgColor, BG_ON);
            textLabel.color = WithAlpha(baseTxtColor, TXT_ON);
        }
    }

    public void OnPointerExit()
    {
        if(isSelected){
            if(!isResetChip){
                chipImage.color = WithAlpha(baseBgColor, BG_ON);
                textLabel.color = WithAlpha(baseTxtColor, TXT_ON);
            }
        } else {
            chipImage.color = WithAlpha(baseBgColor, BG_OFF);
            textLabel.color  = WithAlpha(baseTxtColor, TXT_OFF);
        }
    }

    private static Color WithAlpha(Color rgb, float a)
    {
        rgb.a = a;
        return rgb;
    }

    private bool IsResetChipOn()
    {
        return (id == -1 && isSelected);
    }
}
