
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TextLinker : UdonSharpBehaviour
{
    [SerializeField] private TextMeshProUGUI textMeshPro;
    private string unescapedRichText;
    private GameObject linkedFieldContainerPrefab;
    private Vector3[] vertices;

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

            string noTagsText = RemoveTagsAndSpaces(unescapedRichText);
            noTagsText = noTagsText.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

            string baseSearchTag = "#$ID-";
            int id = 1;
            int lastIndex = 0;

            while (true)
            {
                string searchTag = baseSearchTag + id;
                int startIndex = noTagsText.IndexOf(searchTag, lastIndex);
                if (startIndex == -1)
                {
                    break;
                }

                startIndex += searchTag.Length;
                int endIndex = noTagsText.IndexOf(searchTag, startIndex);
                if (endIndex != -1)
                {
                    Vector3 minPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 maxPos = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        int vertexIndex = i * 4;
                        Vector3 bottomLeft = vertices[vertexIndex];
                        Vector3 topRight = vertices[vertexIndex + 2];
                        minPos = Vector3.Min(minPos, bottomLeft);
                        maxPos = Vector3.Max(maxPos, topRight);
                    }

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
                        float linkIconSize = (maxPos.y - minPos.y) / 2;
                        linkIconRect.anchoredPosition = new Vector2(linkIconSize * 1.2f, 0);
                        linkIconRect.sizeDelta = new Vector2(linkIconSize, linkIconSize);
                    }

                    linkedFieldContainer.transform.SetParent(this.transform, false);
                    linkedFieldContainerRect.anchoredPosition = new Vector2(minPos.x, maxPos.y);
                    linkedFieldContainerRect.sizeDelta = new Vector2(maxPos.x - minPos.x, maxPos.y - minPos.y);
                    linkedFieldContainerRect.localScale = new Vector3(1, 1, 1);
                }

                id++;
                lastIndex = endIndex + searchTag.Length;
            }
        }
    }

    string RemoveTagsAndSpaces(string input)
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
            if (!inTag && c != ' ' && c != '　')
            {
                output += c;
            }
        }

        return output;
    }
}
