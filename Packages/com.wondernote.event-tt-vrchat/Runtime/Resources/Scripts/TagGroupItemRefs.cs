
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TagGroupItemRefs : UdonSharpBehaviour
{
    public Button headerButton;
    public TextMeshProUGUI headerLabel;
    public GameObject headBadge;
    public TextMeshProUGUI badgeCountText;
    public GameObject childrenRoot;
    public TagGroupAccordion accordion;
}