
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FlowWrapLayout : UdonSharpBehaviour
{
    [SerializeField] private RectTransform panelBodyRect;
    [SerializeField] private LayoutElement layoutElement;
    private float panelBodyWidth;
    private float spacingX = 20f;
    private float spacingY = 35f;
    private RectTransform rowRect;
    private bool _reflowOnNextEnable = true;

    void Start()
    {
        panelBodyWidth = panelBodyRect.rect.width;
        rowRect = (RectTransform)transform;
    }

    void OnEnable()
    {
        if (!_reflowOnNextEnable) return;
        _reflowOnNextEnable = false;
        SendCustomEventDelayedFrames(nameof(Reflow), 3);
    }

    public void ArmReflowOnNextEnable()
    {
        _reflowOnNextEnable = true;
    }

    public void Reflow()
    {
        float x = 0f;
        float y = 0f;
        float lineH = 0f;

        int childNum = rowRect.childCount;
        for (int i = 0; i < childNum; i++)
        {
            RectTransform child = (RectTransform)rowRect.GetChild(i);

            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);

            float width = LayoutUtility.GetPreferredSize(child, 0);
            float height = LayoutUtility.GetPreferredSize(child, 1);
            bool wrap = x + width > panelBodyWidth;

            if (wrap) {
                x = 0f;
                y -= (lineH + spacingY);
                lineH = 0f;
            }

            child.anchoredPosition = new Vector2(x, y);

            x += (width + spacingX);
            if (height > lineH) lineH = height;
        }

        float totalHeight = (-y) + lineH;

        layoutElement.preferredHeight = totalHeight;
    }
}
