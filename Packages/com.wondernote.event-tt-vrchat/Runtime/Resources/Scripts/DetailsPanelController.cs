
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.Economy;
using System;
using VRC.SDK3.Data;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DetailsPanelController : UdonSharpBehaviour
{
    [Header("Details Element Settings")]
    [SerializeField] private Transform detailsContainer;
    [SerializeField] private RawImage detailsThumbnailImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dateTimeText;
    [SerializeField] private Button groupButton;
    [SerializeField] private TextMeshProUGUI summaryText;

    private string titleSuffix;
    private string groupID;
    private string[] japaneseWeekDays = new string[] { "日", "月", "火", "水", "木", "金", "土" };

    private GameObject detailsTextPrefab;
    private GameObject detailsImagePrefab;
    private GameObject[] videoPlayerPrefabs;
    private GameObject linkedFieldContainerPrefab;
    private CanvasGroup mainPanelCanvasGroup;
    private int currentVideoPrefabIndex = 0;

    [Header("Scrollbar Settings")]
    [SerializeField] private ScrollRect scrollRect;
    private float lastScrollbarValue = 1;
    private float startPosition = 1.0f;
    private float targetScrollPosition = 1.0f;
    private bool isWheelScroll = false;
    private bool isStickScroll = false;
    private float scrollPositionChange;

    private float lerpTime = 0.2f;
    private float currentLerpTime = 0f;
    private float lerpProgress;
    private float scrollSensitivity = 1400.0f;
    private float thumbstickSensitivity = 22.5f;
    private bool isPointerHoveringDetails = true;

    private AudioManager audioManager;
    private DataList detailedImgsByContentList;
    private TextureFormat textureFormat;
    private EventTimetable eventTimetable;

    public void SetEventDetails(string _title, DateTime _dateTime, string _summary, string _details, Texture2D _texture, string _groupID, int _supportedModel, CanvasGroup _mainPanelCanvasGroup, GameObject _detailsTextPrefab, GameObject _detailsImagePrefab, GameObject[] _videoPlayerPrefabs, GameObject _linkedFieldContainerPrefab, AudioManager _audioManager, DataList _detailedImgsByContentList, TextureFormat _textureFormat, EventTimetable timetable)
    {
        detailsThumbnailImage.texture = _texture;

        Rect currentRect = detailsThumbnailImage.uvRect;
        detailsThumbnailImage.uvRect = new Rect(currentRect.x, currentRect.y + currentRect.height, currentRect.width, -currentRect.height);

        switch (_supportedModel)
        {
            case 1:
                titleSuffix = "";
                break;
            case 2:
                titleSuffix = "<space=2em><sprite name=\"pc_only\">";
                break;
            case 3:
                titleSuffix = "<space=2em><sprite name=\"quest_support\">";
                break;
            case 4:
                titleSuffix = "<space=2em><sprite name=\"quest_only\">";
                break;
            default:
                titleSuffix = "";
                break;
        }
        titleText.text = _title + titleSuffix;

        string dayOfWeek = japaneseWeekDays[(int)_dateTime.DayOfWeek];
        dateTimeText.text = _dateTime.ToString($"M月d日({dayOfWeek}) HH:mm～");

        summaryText.text = _summary;
        detailsTextPrefab = _detailsTextPrefab;
        detailsImagePrefab = _detailsImagePrefab;
        videoPlayerPrefabs = _videoPlayerPrefabs;
        linkedFieldContainerPrefab = _linkedFieldContainerPrefab;
        detailedImgsByContentList = _detailedImgsByContentList;
        textureFormat = _textureFormat;
        audioManager = _audioManager;
        eventTimetable = timetable;

        SetDetailsContent(_details);

        if(string.IsNullOrEmpty(_groupID) || !_groupID.StartsWith("grp_"))
        {
            groupButton.gameObject.SetActive(false);
        } else {
            groupID = _groupID;
        }

        mainPanelCanvasGroup = _mainPanelCanvasGroup;
    }

    private void SetDetailsContent(string htmlContent)
    {
        int lastIndex = 0;
        while (true)
        {
            int figureStart = htmlContent.IndexOf("<figure", lastIndex);
            if (figureStart == -1) {
                if (lastIndex < htmlContent.Length) {
                    CreateTextPrefab(htmlContent.Substring(lastIndex));
                }
                break;
            }

            if (figureStart > lastIndex) {
                CreateTextPrefab(htmlContent.Substring(lastIndex, figureStart - lastIndex));
            }

            int figureEnd = htmlContent.IndexOf("</figure>", figureStart) + "</figure>".Length;
            if (figureEnd == -1) {
                break;
            }
            string figureContent = htmlContent.Substring(figureStart, figureEnd - figureStart);
            if (figureContent.Contains("class=\"media\"")) {
                CreateVideoPrefab(figureContent);
            } else if (figureContent.Contains("class=\"image")) {
                CreateImagePrefab(figureContent);
            }

            lastIndex = figureEnd;
        }

        detailedImgsByContentList.Clear();
    }

    private void CreateTextPrefab(string content)
    {
        GameObject detailsText = Instantiate(detailsTextPrefab);
        TextLinker textLinker = detailsText.GetComponent<TextLinker>();
        textLinker.SetLinkedFieldContainerPrefab(linkedFieldContainerPrefab);

        content = content
        .Replace("</h2>", "</h2><line-height=2.7em><br></line-height>")
        .Replace("</h3>", "</h3><line-height=2.5em><br></line-height>")
        .Replace("</h4>", "</h4><line-height=2em><br></line-height>")
        .Replace("</p>", "</p><line-height=2.5em><br></line-height>")
        .Replace("</ul></li>", "</ulli>")
        .Replace("</ol></li>", "</olli>")
        .Replace("</ul>", "</ul><line-height=1.5em><br></line-height>")
        .Replace("</ol>", "</ol><line-height=1.5em><br></line-height>")
        .Replace("</ulli>", "</ul></li>")
        .Replace("</olli>", "</ol></li>")
        .Replace("<p>&nbsp;</p>", "<p><line-height=1em><br></line-height></p>");

        string unescapedRichText = ConvertHtmlToCustomRichText(content);
        string escapedRichText = unescapedRichText.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("</size>", "</size>\u200B").Replace("&nbsp;", "\u2002").Replace("˸", ":");
        detailsText.GetComponent<TextMeshProUGUI>().text = escapedRichText;

        textLinker.SetUnescapedRichText(unescapedRichText);

        detailsText.transform.SetParent(detailsContainer, false);
    }

    private void CreateVideoPrefab(string content)
    {
        GameObject prefabToInstantiate = videoPlayerPrefabs[currentVideoPrefabIndex];
        currentVideoPrefabIndex = (currentVideoPrefabIndex + 1) % videoPlayerPrefabs.Length;

        GameObject videoPlayer = Instantiate(prefabToInstantiate);
        string videoUrl = ExtractingStrings(content, "data-oembed-url=\"", "\"");

        GameObject videoUrlBgImage = videoPlayer.transform.Find("Screen/VideoUrlBackground").gameObject;
        RawImage thumbnailRawImage = videoUrlBgImage.GetComponent<RawImage>();
        SetDetailedImage(videoUrl, thumbnailRawImage);

        VideoPlayerController videoPlayerController = videoPlayer.GetComponent<VideoPlayerController>();
        VRCUrl videoVRCUrl = eventTimetable.FindMatchingVRCUrl(videoUrl);
        if (videoPlayerController != null)
        {
            videoPlayerController.SetVRCUrl(videoVRCUrl);
            videoPlayerController.SetAudioManager(audioManager);
        }

        videoPlayer.transform.SetParent(detailsContainer, false);

        RectTransform videoPlayerRect = videoPlayer.GetComponent<RectTransform>();
        videoPlayerRect.localScale = new Vector3(1, 1, 1);
    }

    private void CreateImagePrefab(string content)
    {
        GameObject detailsImage = Instantiate(detailsImagePrefab);
        GameObject imageItem = detailsImage.transform.Find("Image").gameObject;
        GameObject initialImage = detailsImage.transform.Find("Image/InitialImage").gameObject;

        string widthPercentString = ExtractingStrings(content, "width:", "%");
        if (string.IsNullOrEmpty(widthPercentString)) {
            widthPercentString = "100";
        }
        float widthRatio = float.Parse(widthPercentString ) / 100.0f;
        float maxWidth = 1056f;
        float imageWidth = maxWidth * widthRatio;
        string aspectRatioString = ExtractingStrings(content, "aspect-ratio:", ";");
        string[] ratioParts = aspectRatioString.Split('/');
        float aspectWidth = float.Parse(ratioParts[0]);
        float aspectHeight = float.Parse(ratioParts[1]);
        float imageHeight = imageWidth * (aspectHeight / aspectWidth);

        RawImage detailedRawImage = imageItem.GetComponent<RawImage>();
        RectTransform rectTransform = detailedRawImage.rectTransform;
        rectTransform.sizeDelta = new Vector2(imageWidth, imageHeight);

        LayoutElement layoutElement = imageItem.GetComponent<LayoutElement>();
        if (layoutElement != null) {
            layoutElement.preferredWidth = imageWidth;
            layoutElement.preferredHeight = imageHeight;
        }

        HorizontalLayoutGroup layoutGroup = detailsImage.GetComponent<HorizontalLayoutGroup>();

        if (content.Contains("image-style-block-align-left")) {
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
        } else if (content.Contains("image-style-align-center")) {
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
        } else if (content.Contains("image-style-block-align-right")) {
            layoutGroup.childAlignment = TextAnchor.UpperRight;
        } else {
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
        }

        string imageId = ExtractingStrings(content, "data-image-id=\"", "\"");

        if (SetDetailedImage(imageId, detailedRawImage))
        {
            Destroy(initialImage);
        }

        detailsImage.transform.SetParent(detailsContainer, false);

        RectTransform detailsImageRect = detailsImage.GetComponent<RectTransform>();
        detailsImageRect.localScale = new Vector3(1, 1, 1);
    }

    private string ExtractingStrings(string targetContent, string prefix, string suffix)
    {
        string startMarker = prefix;
        int startIndex = targetContent.IndexOf(startMarker);
        if (startIndex == -1) {
            return "";
        }
        startIndex += startMarker.Length;

        int endIndex = targetContent.IndexOf(suffix, startIndex);
        if (endIndex == -1) {
            return "";
        }
        return targetContent.Substring(startIndex, endIndex - startIndex).Trim();
    }

    private bool SetDetailedImage(string imageId, RawImage rawImage)
    {
        for (int i = 0; i < detailedImgsByContentList.Count; i++)
        {
            var detailedImageToken = detailedImgsByContentList[i];
            if (detailedImageToken.TokenType == TokenType.DataDictionary)
            {
                var detailedImageDictionary = detailedImageToken.DataDictionary;
                if (detailedImageDictionary["image_id"] == imageId)
                {
                    string base64DetailedImage = detailedImageDictionary["base64DetailedImage"].String;
                    int detailedImageWidth = (int)detailedImageDictionary["width"].Double;
                    int detailedImageHeight = (int)detailedImageDictionary["height"].Double;

                    byte[] imageBytes = Convert.FromBase64String(base64DetailedImage);
                    Texture2D newTexture = new Texture2D(detailedImageWidth, detailedImageHeight, textureFormat, true, false);
                    newTexture.LoadRawTextureData(imageBytes);
                    newTexture.Apply();

                    rawImage.texture = newTexture;

                    Rect currentRect = rawImage.uvRect;
                    rawImage.uvRect = new Rect(currentRect.x, currentRect.y + currentRect.height, currentRect.width, -currentRect.height);

                    return true;
                }
            }
            else
            {
                Debug.LogError("An element in detailedImageToken is not a DataDictionary.");
            }
        }

        return false;
    }

    private string ConvertHtmlToCustomRichText(string htmlText)
    {
        htmlText = ReplaceTagWithStyle(htmlText, "<h2", "</h2>", "h2-temporary");
        htmlText = ReplaceTagWithStyle(htmlText, "<h3", "</h3>", "h3");
        htmlText = ReplaceTagWithStyle(htmlText, "<h4", "</h4>", "h4");
        htmlText = htmlText.Replace("<style=\"h4\">", "<style=\"h4\">◆");
        htmlText = ReplaceTagWithStyle(htmlText, "<strong>", "</strong>", "bold");
        htmlText = ReplaceTagWithStyle(htmlText, "<mark class=\"marker-yellow\">", "</mark>", "marker-yellow-temporary", false, "yellow");
        htmlText = ReplaceTagWithStyle(htmlText, "<mark class=\"marker-pink\">", "</mark>", "marker-pink-temporary", false, "pink");
        htmlText = ReplaceTagWithStyle(htmlText, "<mark class=\"marker-green\">", "</mark>", "marker-green-temporary", false, "green");
        htmlText = ReplaceTagWithStyle(htmlText, "<mark class=\"underlineMarker-yellow\">", "</mark>", "underlineMarker-yellow");
        htmlText = ReplaceTagWithStyle(htmlText, "<mark class=\"underlineMarker-pink\">", "</mark>", "underlineMarker-pink");
        htmlText = ReplaceTagWithStyle(htmlText, "<mark class=\"underlineMarker-green\">", "</mark>", "underlineMarker-green");
        htmlText = AddIdsToLinkTags(htmlText);
        htmlText = ReplaceTagWithStyle(htmlText, "<link", "</link>", "link");
        htmlText = ReplaceTagWithStyle(htmlText, "<p>", "</p>", "", true);
        htmlText = ReplaceListItems(htmlText);

        return htmlText;
    }

    private string ReplaceTagWithStyle(string input, string tagPattern, string closeTag, string styleName, bool keepContentOnly = false, string markColor = "")
    {
        int startIndex = 0;
        while ((startIndex = input.IndexOf(tagPattern, startIndex)) != -1)
        {
            int startTagClose = input.IndexOf('>', startIndex);
            if (startTagClose == -1) break;

            int contentStart = startTagClose + 1;
            int endIndex = input.IndexOf(closeTag, contentStart);
            if (endIndex == -1) break;

            string content = input.Substring(contentStart, endIndex - contentStart);

            if (keepContentOnly)
            {
                input = input.Substring(0, startIndex) + content + input.Substring(endIndex + closeTag.Length);
                startIndex += content.Length;
                continue;
            }

            if (!(markColor == "") && content.Contains("<s>") && content.Contains("</s>"))
            {
                switch (markColor)
                {
                    case "yellow":
                        content = content.Replace("<s>", "<s><mark=#feff7280>").Replace("</s>", "</mark></s>");
                        break;
                    case "pink":
                        content = content.Replace("<s>", "<s><mark=#ffb7b780>").Replace("</s>", "</mark></s>");
                        break;
                    case "green":
                        content = content.Replace("<s>", "<s><mark=#8cff8c80>").Replace("</s>", "</mark></s>");
                        break;
                }
            }

            string replacement = $"<style=\"{styleName}\">{content}</style>";
            input = input.Substring(0, startIndex) + replacement + input.Substring(endIndex + closeTag.Length);
            startIndex += replacement.Length;
        }

        return input;
    }

    private string AddIdsToLinkTags(string input)
    {
        int linkId = 1;
        int index = input.IndexOf("<link");
        while (index != -1)
        {
            int closeTagIndex = input.IndexOf(">", index + 1);
            if (closeTagIndex == -1) break;

            int endTagIndex = input.IndexOf("</link>", closeTagIndex);
            if (endTagIndex == -1) break;

            string startTag = $"<link><size=0%>#$ID-{linkId}</size>";
            input = input.Substring(0, index) + startTag + input.Substring(closeTagIndex + 1);

            endTagIndex = input.IndexOf("</link>", index + startTag.Length);

            string endTag = $"<size=0%>#$ID-{linkId}</size></link>";
            input = input.Substring(0, endTagIndex) + endTag + input.Substring(endTagIndex + "</link>".Length);

            index = input.IndexOf("<link", endTagIndex + endTag.Length);
            linkId++;
        }

        return input;
    }

    private string ReplaceListItems(string htmlText)
    {
        if (!(htmlText.Contains("<ul>") || htmlText.Contains("<ol>")))
        {
            return htmlText;
        }

        int indentLevel = 0;
        int[] ulItemCounters = new int[10];
        int[] olItemCounters = new int[10];
        string result = "";
        string[] listTypeStack = new string[10];
        int stackPointer = -1;
        bool isInsideListItem = false;

        if (!htmlText.StartsWith("<")) {
            htmlText = "__TEXT__" + htmlText;
        }

        htmlText = htmlText.Replace(">", ">__TEXT__").Replace("__TEXT__<", "<");

        string[] tokens = htmlText.Split(new char[] { '<', '>' }, StringSplitOptions.RemoveEmptyEntries);

        string listItemContent = "";

        foreach (string token in tokens)
        {
            string trimmedToken = token.Trim();

            if (trimmedToken.StartsWith("/ul") || trimmedToken.StartsWith("/ol"))
            {
                indentLevel--;
                if (stackPointer >= 0) stackPointer--;
                isInsideListItem = false;
            }
            else if (trimmedToken.StartsWith("ul") || trimmedToken.StartsWith("ol"))
            {
                indentLevel++;
                stackPointer++;
                if (stackPointer < listTypeStack.Length)
                {
                    if (trimmedToken.StartsWith("ul"))
                    {
                        ulItemCounters[indentLevel] = 0;
                        listTypeStack[stackPointer] = "ul";
                    }
                    else if (trimmedToken.StartsWith("ol"))
                    {
                        olItemCounters[indentLevel] = 0;
                        listTypeStack[stackPointer] = "ol";
                    }
                }
            }
            else if (trimmedToken.Equals("li"))
            {
                if (!string.IsNullOrEmpty(listItemContent))
                {
                    if (stackPointer >= 0 && listTypeStack[stackPointer] == "ul")
                    {
                        result += $"<indent={(indentLevel - 1) * 3}%>・{listItemContent}</indent>\n";
                    }
                    else if (stackPointer >= 0 && listTypeStack[stackPointer] == "ol")
                    {
                        result += $"<indent={(indentLevel - 1) * 3}%>{GetFormattedNumber((indentLevel - 1), olItemCounters[(indentLevel - 1)] + 1)} {listItemContent}</indent>\n";
                    }
                }
                listItemContent = "";

                isInsideListItem = true;
                continue;
            }
            else if (trimmedToken.Equals("/li"))
            {
                if (!string.IsNullOrEmpty(listItemContent))
                {
                    if (stackPointer >= 0 && listTypeStack[stackPointer] == "ul")
                    {
                        result += $"<indent={indentLevel * 3}%>・{listItemContent}</indent>\n";
                    }
                    else if (stackPointer >= 0 && listTypeStack[stackPointer] == "ol")
                    {
                        result += $"<indent={indentLevel * 3}%>{GetFormattedNumber(indentLevel, olItemCounters[indentLevel] + 1)} {listItemContent}</indent>\n";
                    }
                }
                listItemContent = "";

                if (stackPointer >= 0 && listTypeStack[stackPointer] == "ul")
                {
                    ulItemCounters[indentLevel]++;
                }
                else if (stackPointer >= 0 && listTypeStack[stackPointer] == "ol")
                {
                    olItemCounters[indentLevel]++;
                }
                isInsideListItem = false;
            }
            else if (isInsideListItem && !string.IsNullOrEmpty(trimmedToken))
            {
                string content = trimmedToken;
                if (content.StartsWith("__TEXT__"))
                {
                    content = content.Replace("__TEXT__", "").Trim();
                } else {
                    content = $"<{content}>";
                }

                listItemContent += content;
                continue;
            }
            else if (!isInsideListItem && !string.IsNullOrEmpty(trimmedToken))
            {
                if (trimmedToken.StartsWith("__TEXT__"))
                {
                    string textContent = trimmedToken.Replace("__TEXT__", "");
                    result += $"{textContent}";
                }
                else
                {
                    result += $"<{trimmedToken}>";
                }
            }
        }

        return result;
    }

    private string GetFormattedNumber(int level, int count)
    {
        if (level == 1)
        {
            return $"{count}.";
        }
        else if (level == 2)
        {
            return $"{(char)('a' + (count - 1))}.";
        }
        else if (level == 3)
        {
            return $"{ToRoman(count).ToLower()}.";
        }
        else if (level == 4)
        {
            return $"{(char)('A' + (count - 1))}.";
        }
        else
        {
            return $"{ToRoman(count)}.";
        }
    }

    private string ToRoman(int number)
    {
        if (number < 1) return "";
        if (number >= 1000) return "M" + ToRoman(number - 1000);
        if (number >= 900) return "CM" + ToRoman(number - 900);
        if (number >= 500) return "D" + ToRoman(number - 500);
        if (number >= 400) return "CD" + ToRoman(number - 400);
        if (number >= 100) return "C" + ToRoman(number - 100);
        if (number >= 90) return "XC" + ToRoman(number - 90);
        if (number >= 50) return "L" + ToRoman(number - 50);
        if (number >= 40) return "XL" + ToRoman(number - 40);
        if (number >= 10) return "X" + ToRoman(number - 10);
        if (number >= 9) return "IX" + ToRoman(number - 9);
        if (number >= 5) return "V" + ToRoman(number - 5);
        if (number >= 4) return "IV" + ToRoman(number - 4);
        if (number >= 1) return "I" + ToRoman(number - 1);
        return "";
    }

    public void GroupButtonPressed()
    {
        audioManager.PlayAppearanceSound();
        Store.OpenGroupPage(groupID);
    }

    public void GroupButtonHovered()
    {
        if (audioManager != null)
        {
            audioManager.PlayHoverSound();
        }
    }

    public void OnPointerEnterDetails()
    {
        isPointerHoveringDetails = true;
    }
    public void OnPointerExitDetails()
    {
        isPointerHoveringDetails = false;
    }

    private void Update()
    {
        if(isPointerHoveringDetails) {
            float scrollLength = Input.GetAxis("Mouse ScrollWheel");
            float thumbstickLength = Input.GetAxis("Oculus_CrossPlatform_SecondaryThumbstickVertical");

            if (Mathf.Abs(scrollLength) > 0f) {
                CalculateScrollTarget(scrollLength);
            }

            if (Mathf.Abs(thumbstickLength) > 0f) {
                isStickScroll = true;
                float tiltPixelAmount = thumbstickLength * thumbstickSensitivity;
                float tiltableHeight = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
                float tiltNormalizedAmount = tiltPixelAmount / tiltableHeight;
                float newScrollPosition = scrollRect.verticalNormalizedPosition + tiltNormalizedAmount;
                scrollRect.verticalNormalizedPosition = Mathf.Clamp(newScrollPosition, 0f, 1f);
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

        if (scrollRect.verticalNormalizedPosition != lastScrollbarValue) {
            lastScrollbarValue = scrollRect.verticalNormalizedPosition;

            if(!isWheelScroll && !isStickScroll) {
                targetScrollPosition = lastScrollbarValue;
            }
        }
    }

    private void CalculateScrollTarget(float length)
    {
        float scrollPixelAmount = length * scrollSensitivity;
        float scrollableHeight = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
        float scrollNormalizedAmount = scrollPixelAmount / scrollableHeight;

        if (!isWheelScroll) {
        currentLerpTime = 0f;
        startPosition = scrollRect.verticalNormalizedPosition;
        isWheelScroll = true;
        } else
        {
        float denominator = targetScrollPosition - startPosition + scrollPositionChange;
        if (Mathf.Abs(denominator) < Mathf.Epsilon) {
            isWheelScroll = false;
            } else
            {
                currentLerpTime = lerpTime * lerpProgress * ((targetScrollPosition - startPosition) / denominator);
            }
        }

        float newTargetScrollPosition = Mathf.Clamp(targetScrollPosition + scrollNormalizedAmount, 0f, 1f);
        scrollPositionChange = newTargetScrollPosition - targetScrollPosition;
        targetScrollPosition = newTargetScrollPosition;
    }

    public void CloseDetails()
    {
        Destroy(this.gameObject);
        mainPanelCanvasGroup.interactable = true;
        mainPanelCanvasGroup.blocksRaycasts = true;
    }
}
