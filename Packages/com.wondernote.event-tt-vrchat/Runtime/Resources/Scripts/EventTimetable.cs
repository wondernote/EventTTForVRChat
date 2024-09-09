
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDK3.StringLoading;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.Economy;
using System;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EventTimetable : UdonSharpBehaviour
{
    [Header("API Settings")]
    [SerializeField] private VRCUrl timetableApiUrl;
    [SerializeField] private VRCUrl[] imageUrls;

    [Header("Event Item Settings")]
    [SerializeField] private GameObject dateTimeContainers;
    [SerializeField] private GameObject dateItemPrefab;
    [SerializeField] private GameObject timeItemPrefab;
    [SerializeField] private GameObject eventItemPrefab;
    private RectTransform dateTimeContainersRect;

    [Header("Default Image Settings")]
    [SerializeField] private Image backGroundImage;
    [SerializeField] private Sprite backGroundSprite;
    [SerializeField] private Sprite parseErrorSprite;
    [SerializeField] private Sprite noEventsSprite;
    [SerializeField] private Sprite loadErrorSprite;
    [SerializeField] private Texture2D loadingImage;
    [SerializeField] private Texture2D blankLogoImage;
    [SerializeField] private Texture2D failedThumbnailImage;

    [Header("Scrollbar Settings")]
    [SerializeField] private ScrollRect scrollRect;
    private bool isScrollbarVisible = false;
    private float lastScrollbarValue = 1;
    private float viewportTop;
    private float dateTimeContainersChildCount;
    private float stickyDateHeight;
    private Transform firstDateItem;
    private float dateTextHeightWithMargin;
    private float startPosition = 1.0f;
    private float targetScrollPosition;
    private bool isWheelScroll = false;
    private bool isStickScroll = false;
    private float scrollPositionChange;
    private int currentStickyIndex = -1;

    private float lerpTime = 0.2f;
    private float currentLerpTime = 0f;
    private float lerpProgress;
    private float scrollSensitivity = 1900.0f;
    private float thumbstickSensitivity = 27.0f;

    [Header("Return Button Settings")]
    [SerializeField] private GameObject returnButton;
    [SerializeField] private Image returnButtonImage;
    private float initialVerticalPosition = -1;
    private float threshold = 0.05f;
    private float fadeAlpha = 0f;
    private float fadeMinAlpha = 0f;
    private float fadeMaxAlpha = 0.85f;
    private float fadeSpeed = 4f;
    private bool startFadeIn = false;
    private bool startFadeOut = false;
    private bool returnButtonClicked = false;

    [Header("Details Info Settings")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup mainPanelCanvasGroup;
    [SerializeField] private GameObject detailsPanelPrefab;
    [SerializeField] private GameObject detailsTextPrefab;
    [SerializeField] private GameObject detailsImagePrefab;
    [SerializeField] private GameObject videoPlayerPrefab;
    [SerializeField] private GameObject linkedFieldContainerPrefab;
    [SerializeField] private TextMeshProUGUI FooterText;

    [Header("Audio Settings")]
    [SerializeField] private AudioManager audioManager;

    private VRCImageDownloader imgDownloader;
    private int downloadingImgIndex;
    private int imagesToDownloadCount;
    private DataList eventList = new DataList();
    private Texture2D[] eventTextureCache;
    private float thumbnailWidth;
    private float thumbnailHeight;
    private float originalImageWidth = 2048.0f;
    private float originalImageHeight = 2048.0f;
    private Vector2 uvSize;
    private int gridLayoutPaddingLeft = 37;
    private int gridLayoutPaddingRight = 37;
    private int gridLayoutPaddingTop = 25;
    private int gridLayoutPaddingBottom = 18;
    private bool isPointerHoveringMain = false;

    private void Start()
    {
        imgDownloader = new VRCImageDownloader();
        eventTextureCache = new Texture2D[50];
        scrollRect.verticalNormalizedPosition = 1.0f;
        targetScrollPosition = 1.0f;
        returnButtonImage.color = new Color(1f, 1f, 1f, 0f);
        returnButton.SetActive(false);
        FooterText.text = "■イベントの登録はウェブサイト (https://wondernote.net/) から　■アセットのダウンロードはVCC・GitHub・BOOTHから　※詳しくは左記サイトをご覧ください";
        FetchTimetableInfo();
    }

    private void FetchTimetableInfo()
    {
        VRCStringDownloader.LoadUrl(timetableApiUrl, this.GetComponent<UdonBehaviour>());
    }

    public override void OnStringLoadSuccess(IVRCStringDownload download)
    {
        InitializeValues();
        bool[] results = ParseJson(download.Result);
        bool isJsonParsed = results[0];
        bool hasEvents = !results[1];

        if (isJsonParsed)
        {
            if (hasEvents) {
                backGroundImage.sprite = backGroundSprite;
                DisplayAllEvents();
                UpdateItemHeights();
                Canvas.ForceUpdateCanvases();

                dateTimeContainersRect = dateTimeContainers.GetComponent<RectTransform>();
                viewportTop = dateTimeContainersRect.anchoredPosition.y;
                dateTimeContainersChildCount = dateTimeContainersRect.childCount;

                firstDateItem = dateTimeContainers.transform.GetChild(0);
                stickyDateHeight = firstDateItem.position.y;

                RectTransform backgroundImageRect = firstDateItem.Find("DateTextBackground/BackgroundImage").GetComponent<RectTransform>();
                dateTextHeightWithMargin = backgroundImageRect.sizeDelta.y - 10;

                initialVerticalPosition = GetInitialScrollPosition();
                SetInitialPosition(initialVerticalPosition);
                InitializeStickyDate();
            } else {
                backGroundImage.sprite = noEventsSprite;
                return;
            }
        } else {
            backGroundImage.sprite = parseErrorSprite;
            return;
        }

        ClearEventTextureCache();
        DownloadNextImage();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        backGroundImage.sprite = loadErrorSprite;
        Debug.LogError($"Error loading string: {result.ErrorCode} - {result.Error}");
    }

    private void InitializeValues()
    {
        downloadingImgIndex = 0;
        imagesToDownloadCount = 0;
        thumbnailWidth = 0;
        thumbnailHeight = 0;
    }

    private bool[] ParseJson(string jsonResponse)
    {
        bool isSuccess = false;
        bool isEmpty = true;

        jsonResponse = jsonResponse.Replace("<a ", "<link ").Replace("<\\/a>", "<\\/link>");

        DataToken result;
        if (VRCJson.TryDeserializeFromJson(jsonResponse, out result))
        {
            if (result.TokenType == TokenType.DataDictionary)
            {
                var rootDictionary = result.DataDictionary;

                DataToken eventsToken;
                if (rootDictionary.TryGetValue("eventTT_info", TokenType.DataList, out eventsToken))
                {
                    eventList.Clear();

                    if (eventsToken.DataList.Count == 0)
                    {
                        Debug.LogError("eventTT_info is empty.");
                        isSuccess = true;
                        return new bool[] { isSuccess, isEmpty };
                    }

                    for (int i = 0; i < eventsToken.DataList.Count; i++)
                    {
                    var eventToken = eventsToken.DataList[i];
                        if (eventToken.TokenType == TokenType.DataDictionary)
                        {
                            var eventDictionary = eventToken.DataDictionary;

                            DataDictionary newEventDictionary = new DataDictionary();
                            newEventDictionary.Add("title", eventDictionary["title"]);
                            newEventDictionary.Add("summary", eventDictionary["summary"]);
                            newEventDictionary.Add("details", eventDictionary["details"]);
                            newEventDictionary.Add("supported_model", new DataToken((int)eventDictionary["supported_model"].Double));
                            newEventDictionary.Add("group_id", eventDictionary["group_id"]);
                            newEventDictionary.Add("datetime", eventDictionary["datetime"]);
                            newEventDictionary.Add("image_index", new DataToken((int)eventDictionary["image_index"].Double));

                            if (eventDictionary.ContainsKey("position") && eventDictionary["position"].TokenType == TokenType.DataDictionary)
                            {
                                var positionDictionary = eventDictionary["position"].DataDictionary;
                                DataDictionary newPositionDictionary = new DataDictionary();
                                newPositionDictionary.Add("gridX", new DataToken((int)positionDictionary["gridX"].Double));
                                newPositionDictionary.Add("gridY", new DataToken((int)positionDictionary["gridY"].Double));
                                newEventDictionary.Add("position", new DataToken(newPositionDictionary));
                            }
                            else
                            {
                                Debug.LogError("position element of JSON is not a DataDictionary.");
                            }

                            eventList.Add(new DataToken(newEventDictionary));
                        }
                        else
                        {
                            Debug.LogError("An element in eventTT_info is not a DataDictionary.");
                        }
                    }
                }

                DataToken thumbnailSizeToken;
                if (rootDictionary.TryGetValue("thumbnail_size", TokenType.DataDictionary, out thumbnailSizeToken))
                {
                    var sizeDictionary = thumbnailSizeToken.DataDictionary;
                    thumbnailWidth = (int)sizeDictionary["width"].Double;
                    thumbnailHeight = (int)sizeDictionary["height"].Double;
                    uvSize = new Vector2(thumbnailWidth / originalImageWidth, thumbnailHeight / originalImageHeight);
                }
                else
                {
                    Debug.LogError("Failed to retrieve thumbnail_size from JSON.");
                }

                DataToken imageCountToken;
                if (rootDictionary.TryGetValue("imagesSetCount", TokenType.Double, out imageCountToken))
                {
                    imagesToDownloadCount = (int)imageCountToken.Double;
                }
                else
                {
                    Debug.LogError("Failed to retrieve imagesSetCount from JSON.");
                }

                isSuccess = true;
                isEmpty = false;
                return new bool[] { isSuccess, isEmpty };
            }
            else
            {
                Debug.LogError("Root element of JSON is not a DataDictionary.");
            }
        }
        else
        {
            Debug.LogError($"Failed to Deserialize JSON: {jsonResponse}");
        }

        return new bool[] { isSuccess, isEmpty };
    }

    private void DisplayAllEvents()
    {
        for (int i = 0; i < eventList.Count; i++)
        {
            DataDictionary eventData = eventList[i].DataDictionary;
            string title = eventData["title"].String;

            DateTime dateTime;
            if (DateTime.TryParse(eventData["datetime"].String, out dateTime))
            {
                GameObject dateItem = GetOrCreateDateItem(dateTime);
                GameObject timeItem = GetOrCreateTimeItem(dateItem, dateTime);
                GameObject eventItem = Instantiate(eventItemPrefab);

                Transform timeContainersTransform = timeItem.transform.Find("TimeContainers");
                eventItem.transform.SetParent(timeContainersTransform, false);

                EventItemScript eventItemScript = eventItem.GetComponent<EventItemScript>();

                if (eventItemScript != null)
                {
                    eventItemScript.SetImageIndex(eventData["image_index"].Int);
                    int gridX = eventData["position"].DataDictionary["gridX"].Int;
                    int gridY = eventData["position"].DataDictionary["gridY"].Int;
                    eventItemScript.SetGridX(gridX);
                    eventItemScript.SetGridY(gridY);
                    eventItemScript.SetTitle(title);
                    eventItemScript.SetDateTime(dateTime);
                    eventItemScript.SetThumbnailImage(loadingImage, false);

                    string summary = eventData["summary"].String;
                    string details = !eventData["details"].IsNull ? eventData["details"].String : null;
                    string groupId = !eventData["group_id"].IsNull ? eventData["group_id"].String : null;
                    int supportedModel = eventData["supported_model"].Int;
                    eventItemScript.SetDetails(summary, details, groupId, supportedModel, canvas, detailsPanelPrefab, detailsTextPrefab, detailsImagePrefab, videoPlayerPrefab, linkedFieldContainerPrefab, mainPanelCanvasGroup, thumbnailWidth, thumbnailHeight, uvSize, audioManager);
                }
            }
            else
            {
                Debug.LogError($"Event date time format is invalid: {eventData["datetime"].String}");
            }
        }
    }

    private GameObject GetOrCreateDateItem(DateTime dateTime)
    {
        string dateName = dateTime.ToString("yyyyMMdd");
        Transform dateTransform = dateTimeContainers.transform.Find(dateName);
        if (dateTransform == null)
        {
            GameObject dateItem = Instantiate(dateItemPrefab, dateTimeContainers.transform);
            dateItem.name = dateName;

            TextMeshProUGUI dateText = dateItem.transform.Find("DateTextBackground/BackgroundImage/DateText").GetComponent<TextMeshProUGUI>();
            if (dateText != null)
            {
                string[] japaneseWeekDays = { "日", "月", "火", "水", "木", "金", "土" };
                string dayOfWeek = japaneseWeekDays[(int)dateTime.DayOfWeek];
                dateText.text = dateTime.ToString("M月d日") + $" ({dayOfWeek})";
            }

            return dateItem;
        }
        return dateTransform.gameObject;
    }

    private GameObject GetOrCreateTimeItem(GameObject dateItem, DateTime dateTime)
    {
        string timeName = $"Time{dateTime.Hour:D2}";
        Transform dateContainersTransform = dateItem.transform.Find("DateContainers");
        Transform timeTransform = dateContainersTransform.Find(timeName);

        if (timeTransform == null)
        {
            GameObject timeItem = Instantiate(timeItemPrefab, dateContainersTransform);
            timeItem.name = timeName;

            TextMeshProUGUI timeText = timeItem.GetComponentInChildren<TextMeshProUGUI>();
        if (timeText != null)
        {
            timeText.text = $"{dateTime.Hour}:00";
        }

            return timeItem;
        }
        return timeTransform.gameObject;
    }

    private void UpdateItemHeights()
    {
        foreach (Transform dateItemTransform in dateTimeContainers.transform)
        {
            float dateItemHeight = 0f;
            Transform dateContainersTransform = dateItemTransform.Find("DateContainers");

            foreach (Transform timeItemTransform in dateContainersTransform)
            {
                float timeItemHeight = 0f;
                Transform timeContainersTransform = timeItemTransform.Find("TimeContainers");

                GridLayoutGroup grid = timeContainersTransform.GetComponent<GridLayoutGroup>();
                RectTransform containersRect = timeContainersTransform.GetComponent<RectTransform>();
                int childCount = timeContainersTransform.childCount;
                float containerWidth = containersRect.rect.width - gridLayoutPaddingLeft - gridLayoutPaddingRight;
                int itemsPerRow = Mathf.FloorToInt((containerWidth + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x));
                int numberOfRows = Mathf.CeilToInt((float)childCount / itemsPerRow);
                timeItemHeight = numberOfRows * grid.cellSize.y + (numberOfRows - 1) * grid.spacing.y + gridLayoutPaddingTop + gridLayoutPaddingBottom;

                int lastRowIndex = childCount == 0 ? 0 : ((childCount - 1) / itemsPerRow) * itemsPerRow;
                bool allTextsOneLine = true;
                for (int i = lastRowIndex; i < childCount; i++) {
                    EventItemScript eventItemScript = timeContainersTransform.GetChild(i).GetComponent<EventItemScript>();

                    if (eventItemScript.GetTextLineCount() == 2) {
                        allTextsOneLine = false;
                        break;
                    }
                }
                if (allTextsOneLine) {
                    timeItemHeight -= 24;
                }
                containersRect.sizeDelta = new Vector2(containersRect.sizeDelta.x, timeItemHeight);

                int lastItemCount = childCount % itemsPerRow;
                int numOfBlanks = (lastItemCount == 0) ? 0 : itemsPerRow - lastItemCount;
                for (int i = 0; i < numOfBlanks; i++)
                {
                    GameObject eventItem = Instantiate(eventItemPrefab);
                    eventItem.transform.SetParent(timeContainersTransform, false);
                    EventItemScript eventItemScript = eventItem.GetComponent<EventItemScript>();

                    if (eventItemScript != null)
                    {
                        eventItemScript.SetImageIndex(-1);
                        eventItemScript.SetTitle("");
                        eventItemScript.SetDateTime(DateTime.MinValue);
                        eventItemScript.SetThumbnailImage(blankLogoImage, false);
                    }
                }

                RectTransform timeTextRect = timeItemTransform.Find("TimeText").GetComponent<RectTransform>();
                float timeTextHeight = timeTextRect.sizeDelta.y;
                timeItemHeight += timeTextHeight;

                RectTransform timeItemRect = timeItemTransform.GetComponent<RectTransform>();
                timeItemRect.sizeDelta = new Vector2(timeItemRect.sizeDelta.x, timeItemHeight);

                dateItemHeight += timeItemHeight;
            }

            RectTransform dateContainersRect = dateContainersTransform.GetComponent<RectTransform>();
            dateContainersRect.sizeDelta = new Vector2(dateContainersRect.sizeDelta.x, dateItemHeight);

            RectTransform dateTextRect = dateItemTransform.Find("DateTextBackground/BackgroundImage/DateText").GetComponent<RectTransform>();
            float dateTextHeight = dateTextRect.sizeDelta.y;
            dateItemHeight += dateTextHeight;

            RectTransform dateItemRect = dateItemTransform.GetComponent<RectTransform>();
            dateItemRect.sizeDelta = new Vector2(dateItemRect.sizeDelta.x, dateItemHeight);
        }
    }

    private float GetInitialScrollPosition()
    {
        float dateTimeContainersHeight = dateTimeContainersRect.rect.height;
        float viewportHeight = scrollRect.GetComponent<RectTransform>().rect.height;

        if(dateTimeContainersHeight <= viewportHeight) {
            isScrollbarVisible = false;
            return 1.0f;
        } else {
            isScrollbarVisible = true;
        }

        string currentDate = System.DateTime.Now.ToString("yyyyMMdd");
        int currentHour = System.DateTime.Now.Hour;

        RectTransform dateItem = FindDateItem(currentDate);
        RectTransform timeItem = FindTimeItem(dateItem, currentHour);

        if (timeItem != null)
        {
            float timeItemPositionY = timeItem.anchoredPosition.y + timeItem.parent.GetComponent<RectTransform>().anchoredPosition.y;
            float scrollPosition = ((0 - timeItemPositionY) - dateTextHeightWithMargin) / (dateTimeContainersHeight - viewportHeight);
            return 1.0f - Mathf.Clamp(scrollPosition, 0f, 1f);
        } else {
            return 1.0f;
        }
    }

    private RectTransform FindDateItem(string dateName)
    {
        Transform dateItemTransform = dateTimeContainers.transform;
        for (int i = 0; i < dateItemTransform.childCount; i++)
        {
            Transform child = dateItemTransform.GetChild(i);
            RectTransform childRect = child.GetComponent<RectTransform>();

            if (childRect != null && child.name == dateName)
            {
                return childRect;
            }
        }
        return null;
    }

    private RectTransform FindTimeItem(RectTransform dateItem, int time)
    {
        if (dateItem == null) return null;

        Transform dateContainers = dateItem.Find("DateContainers");

        if (dateContainers == null) return null;

        for (int currentTime = time; currentTime < 24; currentTime++)
        {
            string timeName = "Time" + currentTime.ToString("00");

            for (int i = 0; i < dateContainers.childCount; i++)
            {
                Transform child = dateContainers.GetChild(i);
                RectTransform childRect = child.GetComponent<RectTransform>();

                if (childRect != null && child.name == timeName)
                {
                    return childRect;
                }
            }
        }

        return null;
    }

    private void SetInitialPosition(float initialPosition)
    {
        scrollRect.verticalNormalizedPosition = initialPosition;
        lastScrollbarValue = initialPosition;
        targetScrollPosition = initialPosition;
    }

    private void InitializeStickyDate()
    {
        for (int i = 0; i < dateTimeContainersChildCount; i++)
        {
            if (StickDateByIndex(i) == 0)
            {
                currentStickyIndex = i;
                return;
            }
        }
    }

    private int StickDateByIndex(int index)
    {
        Transform child = dateTimeContainersRect.GetChild(index);
        RectTransform dateItemRect = child.GetComponent<RectTransform>();
        RectTransform dateTextBackgroundRect = child.Find("DateTextBackground").GetComponent<RectTransform>();

        float itemTop = dateItemRect.anchoredPosition.y + dateTimeContainersRect.anchoredPosition.y;
        float itemBottom = itemTop - dateItemRect.sizeDelta.y;

        if ((itemBottom + dateTextHeightWithMargin < viewportTop) && (itemTop >= viewportTop)) {
            dateTextBackgroundRect.position = new Vector3(dateTextBackgroundRect.position.x, stickyDateHeight, dateTextBackgroundRect.position.z);
            return 0;
        } else if (itemTop < viewportTop) {
            dateTextBackgroundRect.anchoredPosition = new Vector2(dateTextBackgroundRect.anchoredPosition.x, 0);
            return -1;
        } else {

            return 1;
        }
    }

    private void ClearEventTextureCache()
    {
        for (int i = 0; i < eventTextureCache.Length; i++)
        {
            eventTextureCache[i] = null;
        }
    }

    public void DownloadNextImage()
    {
        if (downloadingImgIndex < imagesToDownloadCount)
        {
            VRCUrl imageUrl = imageUrls[downloadingImgIndex];
            imgDownloader.DownloadImage(imageUrl, null, this.GetComponent<UdonBehaviour>(), null);
            downloadingImgIndex++;
            SendCustomEventDelayedSeconds(nameof(DownloadNextImage), 5.1f);
        }
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        for (int i = 0; i < imageUrls.Length; i++)
        {
            if (result.Url.ToString() == imageUrls[i].Get())
            {
                eventTextureCache[i] = result.Result;
                UpdateTextureByImageIndex(i, true);
                break;
            }
        }
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        for (int i = 0; i < imageUrls.Length; i++)
        {
            if (result.Url.ToString() == imageUrls[i].Get())
            {
                eventTextureCache[i] = result.Result;
                UpdateTextureByImageIndex(i, false);
                Debug.LogError($"画像 {result.Url} のダウンロードに失敗しました。");
                break;
            }
        }
    }

    private void OnDestroy()
    {
        imgDownloader.Dispose();
    }

    private void UpdateTextureByImageIndex(int imageIndex, bool loadSuccess)
    {
        foreach (Transform dateItemTransform in dateTimeContainers.transform)
        {
            Transform dateContainersTransform = dateItemTransform.Find("DateContainers");
            foreach (Transform timeItemTransform in dateContainersTransform)
            {
                Transform timeContainersTransform = timeItemTransform.Find("TimeContainers");
                foreach (Transform eventItemTransform in timeContainersTransform)
                {
                    EventItemScript eventItemScript = eventItemTransform.GetComponent<EventItemScript>();
                    if (eventItemScript != null && eventItemScript.GetImageIndex() == imageIndex)
                    {
                        if(loadSuccess)
                        {
                            Texture2D newTexture = eventTextureCache[imageIndex] ?? failedThumbnailImage;
                            eventItemScript.SetThumbnailImage(newTexture, true);

                            float gridX = eventItemScript.GetGridX();
                            float gridY = eventItemScript.GetGridY();
                            Vector2 uvPosition = new Vector2(gridX * thumbnailWidth / originalImageWidth, gridY * thumbnailHeight / originalImageHeight);
                            Rect uvRect = new Rect(uvPosition, uvSize);
                            eventItemScript.GetThumbnailImage().uvRect = uvRect;
                        } else {
                            eventItemScript.SetThumbnailImage(failedThumbnailImage, false);
                        }
                    }
                }
            }
        }
    }

    public void OnClickReturnButton()
    {
        currentLerpTime = 0f;
        startPosition = scrollRect.verticalNormalizedPosition;
        initialVerticalPosition = GetInitialScrollPosition();
        returnButtonClicked = true;
        startFadeOut = true;
    }

    public void OnPointerEnterMain()
    {
        isPointerHoveringMain = true;
    }
    public void OnPointerExitMain()
    {
        isPointerHoveringMain = false;
    }

    private void Update()
    {
        if(isScrollbarVisible) {
            ReturnToInitialPosition(returnButtonClicked);

            if(isPointerHoveringMain) {
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
                StickDate();
                lastScrollbarValue = scrollRect.verticalNormalizedPosition;

                if(!isWheelScroll && !isStickScroll && !returnButtonClicked) {
                    targetScrollPosition = lastScrollbarValue;
                }
            }

            if (fadeAlpha == fadeMinAlpha && initialVerticalPosition != -1) {
                if (scrollRect.verticalNormalizedPosition < (initialVerticalPosition - threshold) || (initialVerticalPosition + threshold) < scrollRect.verticalNormalizedPosition) {
                    startFadeIn = true;
                    returnButton.SetActive(true);
                }
            }
            FadeReturnButton(startFadeIn, startFadeOut);
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
    }

    private void StickDate()
    {
        if(currentStickyIndex >= 0 && currentStickyIndex < dateTimeContainersChildCount)
        {
            int updateDirection = StickDateByIndex(currentStickyIndex);

            if (updateDirection != 0)
            {
                int newIndex = currentStickyIndex + updateDirection;
                if (newIndex >= 0 && newIndex < dateTimeContainersChildCount)
                {
                    if (StickDateByIndex(newIndex) == 0)
                    {
                        currentStickyIndex = newIndex;
                    }
                }
            }
        }
    }

    private void ReturnToInitialPosition(bool isButtonClicked)
    {
        if (isButtonClicked) {
            currentLerpTime += Time.deltaTime;

            if (currentLerpTime > lerpTime) {
                currentLerpTime = lerpTime;
                }

            lerpProgress = currentLerpTime / lerpTime;
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(startPosition, initialVerticalPosition, lerpProgress);

            if (Mathf.Abs(scrollRect.verticalNormalizedPosition - initialVerticalPosition) < 0.003 || currentLerpTime == lerpTime) {
                SetInitialPosition(initialVerticalPosition);
                StickDate();
                returnButtonClicked = false;
            }
        }
    }

    private void FadeReturnButton(bool isFadeInStart, bool isFadeOutStart)
    {
        if (isFadeInStart) {
            fadeAlpha += Time.deltaTime * fadeSpeed;
            fadeAlpha = Mathf.Clamp(fadeAlpha, fadeMinAlpha, fadeMaxAlpha);
            returnButtonImage.color = new Color(1f, 1f, 1f, fadeAlpha);

            if (fadeAlpha == fadeMaxAlpha) {
                startFadeIn  = false;
            }
        }

        if (isFadeOutStart) {
            fadeAlpha -= Time.deltaTime * fadeSpeed;
            fadeAlpha = Mathf.Clamp(fadeAlpha, fadeMinAlpha, fadeMaxAlpha);
            returnButtonImage.color = new Color(1f, 1f, 1f, fadeAlpha);

            if (fadeAlpha == fadeMinAlpha) {
                startFadeOut  = false;
                returnButton.SetActive(false);
            }
        }
    }
}
