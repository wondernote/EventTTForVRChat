
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TagGroupAccordion : UdonSharpBehaviour
{
    [SerializeField] private RectTransform childrenRoot;
    [SerializeField] private LayoutElement bodySlotLayoutElement;
    [SerializeField] private RectTransform arrowIcon;
    [SerializeField] private GameObject headerTopBorder;
    [SerializeField] private Image headerFillImage;

    private bool isFirstGroup = false;
    private float arrowClosedZ = -90f;
    private float arrowOpenZ = 90f;

    private bool isOpen = false;
    private bool hasReflowedOnce = false;
    private FlowWrapLayout[] rowFlowWrapLayouts;
    private LeftWingController wingController;

    private float currentBodyHeight = 0f;
    private float targetBodyHeight = 0f;
    private float measuredBodyHeight = 0f;
    private bool isAnimating = false;
    private float animationDuration = 0.18f;

    private float headerClosedAlpha = 0.04f;
    private float headerOpenAlpha = 0.15f;
    private float headerHoverAlpha = 0.15f;
    private bool isPointerOverHeader = false;

    void Start()
    {
        currentBodyHeight = 0f;
        targetBodyHeight = 0f;
        measuredBodyHeight = 0f;
        isAnimating = false;

        bodySlotLayoutElement.preferredHeight = 0f;
        
        ApplyHeaderTopBorderState();
        UpdateArrowVisual(0f);
        UpdateHeaderFillVisual();
    }

    public void SetRowFlowWrapLayouts(FlowWrapLayout[] layouts)
    {
        rowFlowWrapLayouts = layouts;
    }

    public void SetWingController(LeftWingController controller)
    {
        wingController = controller;
    }

    public void WakeChildrenForPanelOpen()
    {
        if (!childrenRoot.gameObject.activeSelf) {
            childrenRoot.gameObject.SetActive(true);
        }
    }

    public void SleepChildrenIfClosedForPanelClose()
    {
        if (isOpen) return;

        if (childrenRoot.gameObject.activeSelf) {
            childrenRoot.gameObject.SetActive(false);
        }
    }

    public void OnHeaderButtonClicked()
    {
        if (isOpen) {
            SetOpen(false);
            return;
        }

        if (wingController != null) {
            wingController.PlayClickSound();
            wingController.CloseOtherTagAccordions(this);
        }

        SetOpen(true);
    }

    public void OnHeaderPointerEnter()
    {        
        if (!isOpen) {
            if (wingController != null) wingController.PlayHoverSound();
        }

        isPointerOverHeader = true;
        UpdateHeaderFillVisual();
    }

    public void OnHeaderPointerExit()
    {
        isPointerOverHeader = false;
        UpdateHeaderFillVisual();
    }

    public void SetOpen(bool open)
    {
        if (isOpen == open && !isAnimating) return;

        isOpen = open;
        UpdateHeaderFillVisual();

        if (isOpen) {
            WakeChildrenForPanelOpen();
            RefreshBodyHeight();
            targetBodyHeight = measuredBodyHeight;
        } else {
            targetBodyHeight = 0f;
        }

        isAnimating = true;
    }
        
    public void ReflowOpenRowsOnce()
    {
        if (rowFlowWrapLayouts == null) return;

        for (int i = 0; i < rowFlowWrapLayouts.Length; i++)
        {
            if (rowFlowWrapLayouts[i] != null) {
                rowFlowWrapLayouts[i].Reflow();
            }
        }

        hasReflowedOnce = true;
    }

    private void RefreshBodyHeight()
    {
        if (!hasReflowedOnce) {
            ReflowOpenRowsOnce();
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(childrenRoot);
        measuredBodyHeight = LayoutUtility.GetPreferredHeight(childrenRoot);
    }

    public void RefreshHeightAfterChipStateChanged()
    {
        if (!isOpen) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(childrenRoot);
        measuredBodyHeight = LayoutUtility.GetPreferredHeight(childrenRoot);

        currentBodyHeight = measuredBodyHeight;
        targetBodyHeight = measuredBodyHeight;

        bodySlotLayoutElement.preferredHeight = measuredBodyHeight;
    }

    public void SetIsFirstGroup(bool value)
    {
        isFirstGroup = value;
        ApplyHeaderTopBorderState();
    }

    private void ApplyHeaderTopBorderState()
    {
        headerTopBorder.SetActive(isFirstGroup);

    }

    private void UpdateArrowVisual(float ratio)
    {
        float z = Mathf.Lerp(arrowClosedZ, arrowOpenZ, Mathf.Clamp01(ratio));
        arrowIcon.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    private void UpdateHeaderFillVisual()
    {
        float alpha = headerClosedAlpha;

        if (isOpen) {
            alpha = headerOpenAlpha;
        } else if (isPointerOverHeader) {
            alpha = headerHoverAlpha;
        }

        Color c = headerFillImage.color;
        c.a = alpha;
        headerFillImage.color = c;
    }

    void Update()
    {
        if (!isAnimating) return;

        float basis = Mathf.Max(measuredBodyHeight, 1f);
        float speed = basis / Mathf.Max(animationDuration, 0.001f);

        currentBodyHeight = Mathf.MoveTowards(
            currentBodyHeight,
            targetBodyHeight,
            speed * Time.deltaTime
        );

        bodySlotLayoutElement.preferredHeight = currentBodyHeight;

        float ratio = 0f;
        if (measuredBodyHeight > 0.001f) {
            ratio = currentBodyHeight / measuredBodyHeight;
        }
        UpdateArrowVisual(ratio);

        if (Mathf.Approximately(currentBodyHeight, targetBodyHeight))
        {
            currentBodyHeight = targetBodyHeight;
            bodySlotLayoutElement.preferredHeight = currentBodyHeight;

            float finalRatio = 0f;
            if (measuredBodyHeight > 0.001f) {
                finalRatio = currentBodyHeight / measuredBodyHeight;
            }
            UpdateArrowVisual(finalRatio);

            isAnimating = false;

            if (wingController != null) wingController.SetNeedsScrollbarFinalize();
        }
    }
}