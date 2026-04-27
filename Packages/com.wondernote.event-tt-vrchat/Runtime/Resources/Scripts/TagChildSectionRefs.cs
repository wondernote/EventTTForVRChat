
using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TagChildSectionRefs : UdonSharpBehaviour
{
    public TextMeshProUGUI childLabel;
    public RectTransform rowTagsRoot;
    public FlowWrapLayout rowFlowWrap;
}