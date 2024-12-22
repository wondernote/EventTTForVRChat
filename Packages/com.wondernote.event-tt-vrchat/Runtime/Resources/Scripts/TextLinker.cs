
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDK3.Data;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TextLinker : UdonSharpBehaviour
{
    [SerializeField] private TextMeshProUGUI textMeshPro;
    private string unescapedRichText;
    private GameObject linkedFieldContainerPrefab;
    private Vector3[] vertices;
    private DataDictionary linkGroups = new DataDictionary();
    private System.Diagnostics.Stopwatch linkAddTime = new System.Diagnostics.Stopwatch();
    private const int FRAME_PROCESS_LIMIT_MS = 20;
    private string noTagsText;
    private int id = 1;
    private int lastIndex = 0;

    public void SetUnescapedRichText(string text)
    {
        unescapedRichText = text;
    }

    public void SetLinkedFieldContainerPrefab(GameObject prefab)
    {
        linkedFieldContainerPrefab = prefab;
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(textMeshPro.text))
        {
            textMeshPro.ForceMeshUpdate();
            TMP_TextInfo textInfo = textMeshPro.textInfo;
            TMP_MeshInfo[] meshInfos = textInfo.meshInfo;
            if (meshInfos.Length > 1) {
                vertices = (Vector3[])meshInfos[1].vertices.Clone();
            }

            string textMeshProText_temporary = textMeshPro.text;
            textMeshPro.text = textMeshProText_temporary.Replace("h2-temporary", "h2").Replace("marker-yellow-temporary", "marker-yellow").Replace("marker-pink-temporary", "marker-pink").Replace("marker-green-temporary", "marker-green");

            InitializeLinkedField();
        }
    }

    private void InitializeLinkedField()
    {
        noTagsText = RemoveTagsAndSpaces(unescapedRichText);
        noTagsText = noTagsText.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&nbsp;", "").Replace("_", "");

        GameObject dummy = Instantiate(linkedFieldContainerPrefab);
        Destroy(dummy);
        AddLinkAsync();
    }

    public void AddLinkAsync()
    {
        linkAddTime.Restart();

        while (true)
        {
            string linkID = "Link_" + id;
            string searchTag = "#$ID-" + id;
            int startIndex = noTagsText.IndexOf(searchTag, lastIndex);
            if (startIndex == -1)
            {
                break;
            }

            startIndex += searchTag.Length;
            int endIndex = noTagsText.IndexOf(searchTag, startIndex);
            if (endIndex != -1)
            {
                DataList lineBoundingBoxes = new DataList();
                Vector3 lineMinPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 lineMaxPos = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                float previousY = 0f;
                bool firstChar = true;
                float lineHeightThreshold = 20f;
                for (int i = startIndex; i < endIndex; i++)
                {
                    int vertexIndex = i * 4;
                    Vector3 bottomLeft = vertices[vertexIndex];
                    Vector3 topLeft = vertices[vertexIndex + 1];
                    Vector3 topRight = vertices[vertexIndex + 2];
                    Vector3 bottomRight = vertices[vertexIndex + 3];

                    float currentY = bottomLeft.y;

                    if (firstChar)
                    {
                        firstChar = false;
                        previousY = currentY;
                        lineMinPos = bottomLeft;
                        lineMaxPos = topRight;
                    }
                    else
                    {
                        float yDifference = previousY - currentY;

                        if (yDifference > lineHeightThreshold)
                        {
                            lineBoundingBoxes.Add(new DataToken(lineMinPos));
                            lineBoundingBoxes.Add(new DataToken(lineMaxPos));

                            lineMinPos = bottomLeft;
                            lineMaxPos = topRight;
                        }
                        else
                        {
                            lineMinPos = Vector3.Min(lineMinPos, bottomLeft);
                            lineMaxPos = Vector3.Max(lineMaxPos, topRight);
                        }
                        previousY = currentY;
                    }
                }

                lineBoundingBoxes.Add(new DataToken(lineMinPos));
                lineBoundingBoxes.Add(new DataToken(lineMaxPos));

                for (int j = 0; j < lineBoundingBoxes.Count; j += 2)
                {
                    if (lineBoundingBoxes.TryGetValue(j, out DataToken minPosToken))
                    {
                        Vector3 minPos = (Vector3)minPosToken.Reference;

                        if (lineBoundingBoxes.TryGetValue(j + 1, out DataToken maxPosToken))
                        {
                            Vector3 maxPos = (Vector3)maxPosToken.Reference;

                            GameObject linkedFieldContainer = Instantiate(linkedFieldContainerPrefab);

                            string linkedText = noTagsText.Substring(startIndex, endIndex - startIndex);
                            RectTransform linkedFieldContainerRect = linkedFieldContainer.GetComponent<RectTransform>();
                            InputField linkedTextInputField = linkedFieldContainerRect.Find("LinkedTextField").GetComponent<InputField>();

                            if (linkedTextInputField != null)
                            {
                                linkedTextInputField.text = linkedText;
                                RectTransform linkedTextFieldRect = linkedTextInputField.gameObject.GetComponent<RectTransform>();
                                linkedTextFieldRect.sizeDelta = new Vector2((maxPos.x - minPos.x) + 10, (maxPos.y - minPos.y) + 10);

                                RectTransform linkIconRect = linkedFieldContainerRect.Find("LinkIcon").GetComponent<RectTransform>();
                                if (j == lineBoundingBoxes.Count - 2)
                                {
                                    float linkIconSize = (maxPos.y - minPos.y) / 2;
                                    linkIconRect.anchoredPosition = new Vector2(linkIconSize * 1.2f, 0);
                                    linkIconRect.sizeDelta = new Vector2(linkIconSize, linkIconSize);
                                }
                                else
                                {
                                    linkIconRect.gameObject.SetActive(false);
                                }
                            }

                            linkedFieldContainer.transform.SetParent(this.transform, false);
                            linkedFieldContainerRect.anchoredPosition = new Vector2(minPos.x, maxPos.y);
                            linkedFieldContainerRect.sizeDelta = new Vector2(maxPos.x - minPos.x, maxPos.y - minPos.y);
                            linkedFieldContainerRect.localScale = new Vector3(1, 1, 1);

                            LinkedFieldController linkedFieldController= linkedFieldContainer.GetComponent<LinkedFieldController>();
                            if (linkedFieldController != null)
                            {
                                linkedFieldController.SetLinkID(linkID);

                                if (linkGroups.TryGetValue(linkID, out DataToken existingToken))
                                {
                                    DataList existingList = existingToken.DataList;
                                    existingList.Add(new DataToken(linkedFieldController));
                                }
                                else
                                {
                                    var newList = new DataList();
                                    newList.Add(new DataToken(linkedFieldController));
                                    linkGroups.SetValue(linkID, new DataToken(newList));
                                }
                            }
                        }
                    }
                }
            }

            id++;
            lastIndex = endIndex + searchTag.Length;

            if (linkAddTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(AddLinkAsync), 1);
                return;
            }
        }

        AssignLinkGroupsToFields();
    }

    private void AssignLinkGroupsToFields()
    {
        DataList keys = linkGroups.GetKeys();

        for (int i = 0; i < keys.Count; i++)
        {
            if (keys.TryGetValue(i, out DataToken keyToken))
            {
                string linkID = keyToken.String;

                if (linkGroups.TryGetValue(linkID, out DataToken token))
                {
                    DataList linkedList = token.DataList;

                    for (int k = 0; k < linkedList.Count; k++)
                    {
                        if (linkedList.TryGetValue(k, out DataToken controllerToken) && controllerToken.Reference != null)
                        {
                            LinkedFieldController controller = (LinkedFieldController)controllerToken.Reference;

                            if (controller != null)
                            {
                                controller.SetLinkedFields(linkedList);
                            }
                        }
                    }
                }
            }
        }
    }

    private string RemoveTagsAndSpaces(string input)
    {
        string output = "";
        bool inTag = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '<')
            {
                inTag = true;
                continue;
            }
            if (c == '>')
            {
                inTag = false;
                continue;
            }
            if (!inTag && c != ' ' && c != '　' && c != '\n' && c != '\r')
            {
                output += c;
            }
        }

        return output;
    }
}
