
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Data;

public class LinkedFieldController : UdonSharpBehaviour
{
    private string linkID;
    [SerializeField] private Image highlightImage;
    private TextLinker textLinker;
    private LinkedFieldController[] linkedFields;

    private void Start()
    {
        Transform detailsTextTransform = transform.parent;
        if (detailsTextTransform != null)
        {
            textLinker = detailsTextTransform.GetComponent<TextLinker>();
        }
    }

    public void SetLinkID(string id)
    {
        linkID = id;
    }

    public void OnPointerEnterLinkedField()
    {
        foreach (var field in linkedFields)
        {
            field.ApplyHighlight(true);
        }
    }

    public void OnPointerExitLinkedField()
    {
        foreach (var field in linkedFields)
        {
            field.ApplyHighlight(false);
        }
    }

    private void ApplyHighlight(bool highlight)
    {
        if (highlight)
        {
            highlightImage.color = new Color32(238, 106, 66, 51);
        }
        else
        {
            highlightImage.color = Color.clear;
        }
    }

    public void SetLinkedFields(DataList linkedList)
    {
        linkedFields = new LinkedFieldController[linkedList.Count];
        for (int i = 0; i < linkedList.Count; i++)
        {
            if (linkedList.TryGetValue(i, out DataToken token))
            {
                linkedFields[i] = (LinkedFieldController)token.Reference;
            }
        }
    }
}
