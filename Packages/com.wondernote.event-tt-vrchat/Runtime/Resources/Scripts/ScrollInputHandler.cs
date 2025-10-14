
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ScrollInputHandler : UdonSharpBehaviour
{
    [Header("Target")]
    [SerializeField] public ScrollRect scrollRect;
    private float scrollSensitivity = 1900.0f;
    private float thumbstickSensitivity = 1620f;
    private float lerpTime = 0.2f;
    private bool isScrollbarVisible = false;
    private bool isPointerHovering = false;
    private bool isWheelScroll = false;
    private bool isStickScroll = false;
    private float currentLerpTime = 0f;
    private float lerpProgress;
    private float startPosition = 1.0f;
    private float targetScrollPosition;
    private float scrollPositionChange;
    private bool returnButtonClicked = false;

    public void SetPointerHover(bool v)
    {
        isPointerHovering = v;
    }
    public void SetScrollbarVisible(bool v)
    {
        isScrollbarVisible = v;
    }
    public void SetReturnButtonClicked(bool v)
    {
        returnButtonClicked = v;
    }
    public bool GetIsWheelScroll()
    {
        return isWheelScroll;
    }
    public bool GetIsStickScroll()
    {
        return isStickScroll;
    }

    public void SyncToScrollPosition()
    {
        float v = scrollRect.verticalNormalizedPosition;
        startPosition = v;
        targetScrollPosition = v;
    }

    public void UpdateCustomScroll()
    {
        if (!isScrollbarVisible) return;
        if (!isPointerHovering) return;

        float scrollLength = Input.GetAxis("Mouse ScrollWheel");
        float thumbstickLength = Input.GetAxis("Oculus_CrossPlatform_SecondaryThumbstickVertical");

        if (Mathf.Abs(scrollLength) > 0f) {
            CalculateScrollTarget(scrollLength);
        }

        if (Mathf.Abs(thumbstickLength) > 0f) {
            isStickScroll = true;
            float pixelsPerSecond = thumbstickLength * thumbstickSensitivity;
            float delta = Time.deltaTime;
            float tiltPixelAmount = pixelsPerSecond * delta;
            float tiltableHeight = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
            float tiltNormalizedAmount = tiltPixelAmount / tiltableHeight;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + tiltNormalizedAmount);
        } else {
            isStickScroll = false;
        }

        if (isWheelScroll) {
            currentLerpTime += Time.deltaTime;
            if (currentLerpTime > lerpTime) {
                currentLerpTime = lerpTime;
            }
            lerpProgress = currentLerpTime / lerpTime;
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(startPosition, targetScrollPosition, lerpProgress);

            if (Mathf.Abs(scrollRect.verticalNormalizedPosition - targetScrollPosition) < 0.003 || currentLerpTime == lerpTime) {
                isWheelScroll = false;
                scrollRect.verticalNormalizedPosition = targetScrollPosition;
            }
        }
    }

    private void CalculateScrollTarget(float length)
    {
        float scrollPixelAmount = length * scrollSensitivity;
        float scrollableHeight = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
        float scrollNormalizedAmount = scrollPixelAmount / scrollableHeight;

        if (!returnButtonClicked) {
            if (!isWheelScroll) {
                currentLerpTime = 0f;
                startPosition = scrollRect.verticalNormalizedPosition;
                isWheelScroll = true;
            } else {
                float denominator = targetScrollPosition - startPosition + scrollPositionChange;
                if (Mathf.Abs(denominator) < Mathf.Epsilon) {
                    isWheelScroll = false;
                } else {
                    currentLerpTime = lerpTime * lerpProgress * ((targetScrollPosition - startPosition) / denominator);
                }
            }

            float newTargetScrollPosition = Mathf.Clamp(targetScrollPosition + scrollNormalizedAmount, 0f, 1f);
            scrollPositionChange = newTargetScrollPosition - targetScrollPosition;
            targetScrollPosition = newTargetScrollPosition;
        }
    }
}
