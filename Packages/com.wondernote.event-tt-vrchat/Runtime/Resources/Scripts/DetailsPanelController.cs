
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.Economy;
using System;

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
    private GameObject videoPlayerPrefab;
    private GameObject linkedFieldContainerPrefab;
    private CanvasGroup mainPanelCanvasGroup;

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

    private float originalImageWidth = 2048.0f;
    private float originalImageHeight = 2048.0f;

    private AudioManager audioManager;

    public void SetEventDetails(string _title, DateTime _dateTime, string _summary, string _details, Texture2D _texture, bool imageSetSuccess, string _groupID, int _supportedModel, CanvasGroup _mainPanelCanvasGroup, int _gridX, int _gridY, float _thumbnailWidth, float _thumbnailHeight, Vector2 _uvSize, GameObject _detailsTextPrefab, GameObject _detailsImagePrefab, GameObject _videoPlayerPrefab, GameObject _linkedFieldContainerPrefab, AudioManager _audioManager)
    {
        detailsThumbnailImage.texture = _texture;

        if(imageSetSuccess){
            Vector2 uvPosition = new Vector2(_gridX * _thumbnailWidth / originalImageWidth, _gridY * _thumbnailHeight / originalImageHeight);
            Rect uvRect = new Rect(uvPosition, _uvSize);
            detailsThumbnailImage.uvRect = uvRect;
        }

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
        videoPlayerPrefab = _videoPlayerPrefab;
        linkedFieldContainerPrefab = _linkedFieldContainerPrefab;

        SetDetailsContent(_details);

        if(string.IsNullOrEmpty(_groupID))
        {
            groupButton.gameObject.SetActive(false);
        } else {
            groupID = _groupID;
        }

        audioManager = _audioManager;
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
    }

    private void CreateTextPrefab(string content)
    {
        GameObject detailsText = Instantiate(detailsTextPrefab);

        content = content
        .Replace("</h2>", "</h2><line-height=2.7em><br></line-height>")
        .Replace("</h3>", "</h3><line-height=2.5em><br></line-height>")
        .Replace("</h4>", "</h4><line-height=2em><br></line-height>")
        .Replace("</p>", "</p><line-height=2.5em><br></line-height>")
        .Replace("</ul>", "</ul><line-height=1.5em><br></line-height>")
        .Replace("</ol>", "</ol><line-height=1.5em><br></line-height>")
        .Replace("&nbsp;", "<line-height=1em><br></line-height>");

        string unescapedRichText = ConvertHtmlToCustomRichText(content);
        string escapedRichText = unescapedRichText.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        detailsText.GetComponent<TextMeshProUGUI>().text = escapedRichText;
        TextLinker textLinker = detailsText.GetComponent<TextLinker>();
        textLinker.SetUnescapedRichText(unescapedRichText);
        textLinker.SetLinkedFieldContainerPrefab(linkedFieldContainerPrefab);
        detailsText.transform.SetParent(detailsContainer, false);
    }

    private void CreateVideoPrefab(string content)
    {
        GameObject videoPlayer = Instantiate(videoPlayerPrefab);
        Transform screenTransform = videoPlayer.transform.Find("Screen/VideoUrlBackground/InitialVideoUrlContainer");
        Transform initialVideoUrlTransform = screenTransform.Find("InitialVideoUrl");
        Transform videoDisplayedURLTransform = initialVideoUrlTransform.Find("DisplayedURL");

        string videoUrl = ExtractingStrings(content, "data-oembed-url=\"", "\"");

        InputField inputField = initialVideoUrlTransform.GetComponent<InputField>();
        if (inputField != null) {
            inputField.text = videoUrl;
        }

        TextMeshProUGUI displayedVideoURL = videoDisplayedURLTransform.GetComponent<TextMeshProUGUI>();
        if (displayedVideoURL != null) {
            displayedVideoURL.text = videoUrl;
        }

        RectTransform initialVideoUrlContainerRect = screenTransform.GetComponent<RectTransform>();
        RectTransform initialVideoUrlRect = initialVideoUrlTransform.GetComponent<RectTransform>();

        LayoutRebuilder.ForceRebuildLayoutImmediate(initialVideoUrlRect);
        float videoContainerWidth = initialVideoUrlContainerRect.rect.width;
        float videoUrlWidth = initialVideoUrlRect.rect.width;

        ContentSizeFitter videoContentSizeFitter = initialVideoUrlRect.GetComponent<ContentSizeFitter>();
        videoContentSizeFitter.enabled = false;

        float videoNewWidth = Mathf.Min(videoUrlWidth, videoContainerWidth);
        initialVideoUrlRect.sizeDelta = new Vector2(videoNewWidth, initialVideoUrlRect.sizeDelta.y);

        videoPlayer.transform.SetParent(detailsContainer, false);

        RectTransform videoPlayerRect = videoPlayer.GetComponent<RectTransform>();
        videoPlayerRect.localScale = new Vector3(1, 1, 1);
    }

    private void CreateImagePrefab(string content)
    {
        GameObject detailsImage = Instantiate(detailsImagePrefab);
        GameObject imageItem = detailsImage.transform.Find("Image").gameObject;
        Transform initialImageTransform = imageItem.transform.Find("InitialImage/InitialImageUrlContainer");
        Transform initialImageUrlTransform = initialImageTransform.Find("InitialImageUrl");
        Transform imageDisplayedURLTransform = initialImageUrlTransform.Find("DisplayedURL");

        string imageUrl = ExtractingStrings(content, "src=\"", "\"");

        InputField initialUrlField = initialImageUrlTransform.GetComponent<InputField>();
        if (initialUrlField != null) {
            initialUrlField.text = imageUrl;
        }

        TextMeshProUGUI displayedImageURL = imageDisplayedURLTransform.GetComponent<TextMeshProUGUI>();
        if (displayedImageURL != null) {
            displayedImageURL.text = imageUrl;
        }

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

        RectTransform rectTransform = imageItem.GetComponent<RawImage>().rectTransform;
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

        RectTransform initialImageUrlContainerRect = initialImageTransform.GetComponent<RectTransform>();
        RectTransform initialImageUrlRect = initialImageUrlTransform.GetComponent<RectTransform>();

        LayoutRebuilder.ForceRebuildLayoutImmediate(initialImageUrlRect);
        float imageContainerWidth = initialImageUrlContainerRect.rect.width;
        float imageUrlWidth = initialImageUrlRect.rect.width;

        ContentSizeFitter imageContentSizeFitter = initialImageUrlRect.GetComponent<ContentSizeFitter>();
        imageContentSizeFitter.enabled = false;

        float imageNewWidth = Mathf.Min(imageUrlWidth, imageContainerWidth);
        initialImageUrlRect.sizeDelta = new Vector2(imageNewWidth, initialImageUrlRect.sizeDelta.y);

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
        htmlText = ReplaceListItems(htmlText, "ul", "・");
        htmlText = ReplaceListItems(htmlText, "ol", "1.");

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

    private string ReplaceListItems(string input, string listType, string listItemPrefix)
    {
        int startIndex = 0;

        while (true)
        {
            string startTag = $"<{listType}>";
            string endTag = $"</{listType}>";
            startIndex = input.IndexOf(startTag, startIndex);
            if (startIndex == -1) break;

            int endIndex = input.IndexOf(endTag, startIndex);
            if (endIndex == -1) break;

            int contentStart = startIndex + startTag.Length;
            int contentEnd = endIndex;
            string listContent = input.Substring(contentStart, contentEnd - contentStart);

            string newItemContent = "";
            int liStart = listContent.IndexOf("<li>");
            int itemNumber = 1;

            while (liStart != -1)
            {
                int liEnd = listContent.IndexOf("</li>", liStart);
                if (liEnd == -1) break;

                string itemText = listContent.Substring(liStart + 4, liEnd - (liStart + 4));
                string prefix = listItemPrefix;
                if (listType == "ol")
                {
                    prefix = itemNumber++.ToString() + ". ";
                }
                newItemContent += $"<indent=3%>{prefix}{itemText.Trim()}</indent>\n";

                liStart = listContent.IndexOf("<li>", liEnd);
            }

            input = input.Substring(0, startIndex) + newItemContent + input.Substring(endIndex + endTag.Length);
            startIndex = startIndex + newItemContent.Length;
        }

        return input;
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
