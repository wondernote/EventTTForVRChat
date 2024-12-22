
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
    [SerializeField] private VRCUrl pcTimetableApiUrl;
    [SerializeField] private VRCUrl androidTimetableApiUrl;
    private VRCUrl activeTimetableApiUrl;

    [SerializeField] private VRCUrl[] pcDetailedImagesUrls;
    [SerializeField] private VRCUrl[] androidDetailedImagesUrls;
    private VRCUrl[] activeDetailedImagesUrls;

    [SerializeField] private VRCUrl[] detailedVideoUrls;

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
    [SerializeField] private Texture2D blankLogoImage;

    [Header("Scrollbar Settings")]
    [SerializeField] private CanvasGroup scrollViewCanvasGroup;
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
    [SerializeField] private GameObject[] videoPlayerPrefabs;
    [SerializeField] private GameObject linkedFieldContainerPrefab;
    [SerializeField] private TextMeshProUGUI FooterText;

    [Header("Audio Settings")]
    [SerializeField] private AudioManager audioManager;

    [Header("Loading Settings")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private TextMeshProUGUI loadingProgressText;
    [SerializeField] private Slider loadingProgressBar;
    private int totalTimeItems = 0;
    private int processedTimeItems = 0;

    private int loadingChunkIndex = 0;
    private int detailedImagesChunkCount = 0;
    private DataList eventList = new DataList();
    private DataList detailedImagesList = new DataList();
    private int gridLayoutPaddingLeft = 37;
    private int gridLayoutPaddingRight = 37;
    private int gridLayoutPaddingTop = 25;
    private int gridLayoutPaddingBottom = 18;
    private bool isPointerHoveringMain = false;
    private TextureFormat textureFormat;

    private const int FRAME_PROCESS_LIMIT_MS = 20;
    private int jsonParseIndex = 0;
    private int imgsJsonParseIndex ;
    private DataToken cachedJsonResultImgs;
    private DataToken cachedJsonResult;

    private System.Diagnostics.Stopwatch splitProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch jsonChunksParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch jsonParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch eventDisplayTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch updateHeightTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch splitImgsProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch imgsJsonChunksParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch imgsJsonParseProcessTime = new System.Diagnostics.Stopwatch();

    private bool[] parseResult = { false, true };

    private int jsonChunkIndex = 0;
    private DataList jsonChunksList = new DataList();
    private DataList parsedChunksList = new DataList();
    private int splitStartIndex = 0;
    private string arrayContent;

    private int imgsJsonChunkIndex;
    private DataList imgsJsonChunksList = new DataList();
    private DataList parsedChunksImgsList = new DataList();
    private int splitStartIndex_imgs;
    private string arrayContent_imgs;

    private int dateItemIndex = 0;
    private int timeItemIndex = 0;
    private Transform[] dateItemsArray;
    private Transform dateItemTransform;
    private Transform dateContainersTransform;
    private Transform[] timeItemsArray;
    private float dateItemHeight;

    private int eventDisplayIndex = 0;

    private void Start()
    {
        #if UNITY_ANDROID
            textureFormat = TextureFormat.ETC_RGB4Crunched;
            activeDetailedImagesUrls = androidDetailedImagesUrls;
            activeTimetableApiUrl = androidTimetableApiUrl;
        #else
            textureFormat = TextureFormat.DXT1Crunched;
            activeDetailedImagesUrls = pcDetailedImagesUrls;
            activeTimetableApiUrl = pcTimetableApiUrl;
        #endif

        backGroundImage.sprite = backGroundSprite;
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        scrollRect.verticalNormalizedPosition = 1.0f;
        targetScrollPosition = 1.0f;
        returnButtonImage.color = new Color(1f, 1f, 1f, 0f);
        returnButton.SetActive(false);
        FooterText.text = "■イベントの登録はウェブサイト (https://wondernote.net/) から　■アセットのダウンロードはVCC・GitHub・BOOTHから　※詳しくは左記サイトをご覧ください";
        FetchTimetableInfo();
    }

    private void FetchTimetableInfo()
    {
        VRCStringDownloader.LoadUrl(activeTimetableApiUrl, this.GetComponent<UdonBehaviour>());
    }

    public override void OnStringLoadSuccess(IVRCStringDownload download)
    {
        if (download.Url == activeTimetableApiUrl)
        {
            StartJsonParsing(download.Result);
        }
        else
        {
            StartImgsJsonParsing(download.Result);
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (result.Url == activeTimetableApiUrl)
        {
            if (loadingScreen != null)
            {
                Destroy(loadingScreen);
                loadingScreen = null;
            }
            backGroundImage.sprite = loadErrorSprite;
            Debug.LogError($"Error loading main timetable string from {result.Url}: {result.ErrorCode} - {result.Error}");
        }
        else
        {
            Debug.LogError($"Error loading detailed_images string from {result.Url}: {result.ErrorCode} - {result.Error}");
        }
    }

    private void StartJsonParsing(string jsonResponse)
    {
        int start = jsonResponse.IndexOf('[');
        int end = jsonResponse.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            arrayContent = jsonResponse.Substring(start + 1, end - start - 1);
            SplitJsonChunksAsync();
        }
        else
        {
            Debug.LogError("Failed to find valid JSON array in response.");
        }
    }

    public void SplitJsonChunksAsync()
    {
        splitProcessTime.Restart();

        int arrayContentLength = arrayContent.Length;

        while (splitStartIndex < arrayContentLength)
        {
            int nextComma = arrayContent.IndexOf("},", splitStartIndex);
            bool isLastElement = nextComma == -1;

            int endIndex = isLastElement ? arrayContentLength : nextComma + 1;
            string element = arrayContent.Substring(splitStartIndex, endIndex - splitStartIndex).Trim();

            element = element.Replace("<a ", "<link ").Replace("<\\/a>", "<\\/link>");

            if (!element.StartsWith("{")) element = $"{{{element}";
            if (!element.EndsWith("}")) element = $"{element}}}";

            jsonChunksList.Add(new DataToken(element));
            splitStartIndex = endIndex + 1;

            float progress = (float)splitStartIndex / arrayContentLength;
            UpdateLoadingProgress(progress * 0.02f);

            if (splitProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(SplitJsonChunksAsync), 1);
                return;
            }
        }

        splitProcessTime = null;
        ParseJsonChunksAsync();
    }

    public void ParseJsonChunksAsync()
    {
        jsonChunksParseProcessTime.Restart();

        int jsonChunksListCount = jsonChunksList.Count;

        while (jsonChunkIndex < jsonChunksListCount)
        {
            string chunk = jsonChunksList[jsonChunkIndex].String;

            if (VRCJson.TryDeserializeFromJson(chunk, out DataToken result))
            {
                parsedChunksList.Add(result);
            }
            else
            {
                Debug.LogError($"Failed to parse JSON chunk {jsonChunkIndex}/{jsonChunksListCount}.");
            }

            jsonChunkIndex++;

            float progress = (float)jsonChunkIndex /jsonChunksListCount;
            UpdateLoadingProgress(0.02f + progress * 0.02f);

            if (jsonChunksParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseJsonChunksAsync), 1);
                return;
            }
        }

        cachedJsonResult = CombineChunksToCachedResult(parsedChunksList);
        jsonChunksParseProcessTime = null;
        ParseEventJson();
    }

    private DataToken CombineChunksToCachedResult(DataList chunksList)
    {
        var dataList = new DataList();
        for (int i = 0; i < chunksList.Count; i++)
        {
            DataToken chunk = chunksList[i];
            if (chunk.TokenType == TokenType.DataDictionary)
            {
                dataList.Add(chunk);
            }
            else
            {
                Debug.LogError($"Parsed chunk {i} is not a DataDictionary. Skipping...");
            }
        }
        return new DataToken(dataList);
    }

    private void ParseEventJson()
    {
        if (cachedJsonResult.TokenType == TokenType.DataList)
        {
            if (cachedJsonResult.DataList.Count == 0)
            {
                Debug.LogError("event is empty.");
                SetIsSuccess(true);
                UpdateLoadingProgress(1);
                ContinueOnStringLoadSuccess();
            }
            else
            {
                parsedChunksList.Clear();
                parsedChunksList = null;
                jsonChunksList.Clear();
                jsonChunksList = null;
                ParseThumbnailListAsync();
            }
        }
        else
        {
            Debug.LogError("Root element of JSON is not a DataList.");
        }
    }

    public void ParseThumbnailListAsync()
    {
        jsonParseProcessTime.Restart();

        int cachedJsonResultCount = cachedJsonResult.DataList.Count;

        while (jsonParseIndex < cachedJsonResultCount)
        {
            var eventToken = cachedJsonResult.DataList[jsonParseIndex];
            jsonParseIndex++;

            if (eventToken.TokenType == TokenType.DataDictionary)
            {
                var eventDictionary = eventToken.DataDictionary;

                DataDictionary newEventDictionary = new DataDictionary();
                newEventDictionary.Add("content_id", new DataToken((int)eventDictionary["content_id"].Double));
                newEventDictionary.Add("title", eventDictionary["title"]);
                newEventDictionary.Add("summary", eventDictionary["summary"]);
                newEventDictionary.Add("details", eventDictionary["details"]);
                newEventDictionary.Add("supported_model", new DataToken((int)eventDictionary["supported_model"].Double));
                newEventDictionary.Add("group_id", eventDictionary["group_id"]);
                newEventDictionary.Add("datetime", eventDictionary["datetime"]);
                newEventDictionary.Add("thumbnail_width", new DataToken((int)eventDictionary["thumbnail_width"].Double));
                newEventDictionary.Add("thumbnail_height", new DataToken((int)eventDictionary["thumbnail_height"].Double));
                newEventDictionary.Add("thumbnailBase64Image", new DataToken(eventDictionary["thumbnailBase64Image"].String));

                eventList.Add(new DataToken(newEventDictionary));

                if (jsonParseIndex == 1)
                {
                    detailedImagesChunkCount = (int)eventDictionary["detailedImagesChunkCount"].Double;
                }
            }
            else
            {
                Debug.LogError("An element in eventToken is not a DataDictionary.");
            }

            float progress = (float)jsonParseIndex / cachedJsonResultCount;
            UpdateLoadingProgress(0.04f + progress * 0.47f);

            if (jsonParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseThumbnailListAsync), 1);
                return;
            }
        }

        jsonParseProcessTime = null;
        SetIsSuccess(true);
        SetIsEmpty(false);
        ContinueOnStringLoadSuccess();
    }

    private void SetIsSuccess(bool value)
    {
        parseResult[0] = value;
    }
    private void SetIsEmpty(bool value)
    {
        parseResult[1] = value;
    }
    private bool GetIsSuccess()
    {
        return parseResult[0];
    }

    private bool GetIsEmpty()
    {
        return parseResult[1];
    }

    private void ContinueOnStringLoadSuccess()
    {
        bool isJsonParsed = GetIsSuccess();
        bool hasEvents = !GetIsEmpty();
        parseResult = null;

        if (isJsonParsed)
        {
            if (hasEvents) {
                parseErrorSprite = null;
                noEventsSprite = null;
                loadErrorSprite = null;

                DisplayEventsAsync();
            } else {
                if (loadingScreen != null)
                {
                    Destroy(loadingScreen);
                    loadingScreen = null;
                }
                backGroundImage.sprite = noEventsSprite;
                return;
            }
        } else {
            if (loadingScreen != null)
            {
                Destroy(loadingScreen);
                loadingScreen = null;
            }
            backGroundImage.sprite = parseErrorSprite;
            return;
        }
    }

    public void DisplayEventsAsync()
    {
        eventDisplayTime.Restart();

        int eventListCount = eventList.Count;

        while (eventDisplayIndex < eventListCount)
        {
            DataDictionary eventData = eventList[eventDisplayIndex].DataDictionary;
            string title = eventData["title"].String;
            eventDisplayIndex++;

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
                    eventItemScript.SetTitle(title);
                    eventItemScript.SetDateTime(dateTime);

                    int thumbnailWidth = eventData["thumbnail_width"].Int;
                    int thumbnailHeight = eventData["thumbnail_height"].Int;

                    string base64ThumbnailImage = eventData["thumbnailBase64Image"].String;
                    byte[] imageBytes = Convert.FromBase64String(base64ThumbnailImage);

                    Texture2D newTexture = new Texture2D(thumbnailWidth, thumbnailHeight, textureFormat, true, false);
                    newTexture.LoadRawTextureData(imageBytes);
                    newTexture.Apply();
                    eventItemScript.SetThumbnailImage(newTexture, true);

                    int contentID = eventData["content_id"].Int;
                    string summary = eventData["summary"].String;
                    string details = !eventData["details"].IsNull ? eventData["details"].String : null;
                    string groupId = !eventData["group_id"].IsNull ? eventData["group_id"].String : null;
                    int supportedModel = eventData["supported_model"].Int;

                    eventItemScript.SetDetails(contentID, summary, details, groupId, supportedModel, canvas, detailsPanelPrefab, detailsTextPrefab, detailsImagePrefab, videoPlayerPrefabs, linkedFieldContainerPrefab, mainPanelCanvasGroup, audioManager, this);
                }
            }
            else
            {
                Debug.LogError($"Event date time format is invalid: {eventData["datetime"].String}");
            }

            float progress = (float)eventDisplayIndex / eventListCount;
            UpdateLoadingProgress(0.51f + progress * 0.47f);

            if (eventDisplayTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(DisplayEventsAsync), 1);
                return;
            }
        }

        eventDisplayTime = null;
        StartUpdateItemHeights();
    }

    private void StartUpdateItemHeights()
    {
        dateTimeContainers.SetActive(false);

        dateItemIndex = 0;
        timeItemIndex = 0;

        Transform dateTimeContainersTransform = dateTimeContainers.transform;
        dateItemsArray = new Transform[dateTimeContainersTransform.childCount];
        for (int i = 0; i < dateTimeContainersTransform.childCount; i++)
        {
            dateItemsArray[i] = dateTimeContainersTransform.GetChild(i);
        }

        UpdateItemHeightsAsync();
    }

    public void UpdateItemHeightsAsync()
    {
        updateHeightTime.Restart();

        if (dateItemIndex == 0)
        {
            foreach (var dateItemTransform in dateItemsArray)
            {
                var dateContainersTransform = dateItemTransform.Find("DateContainers");
                totalTimeItems += dateContainersTransform.childCount;
            }
        }

        while (dateItemIndex < dateItemsArray.Length)
        {
            if (timeItemIndex == 0)
            {
                dateItemTransform = dateItemsArray[dateItemIndex];
                dateItemHeight = 0f;

                dateContainersTransform = dateItemTransform.Find("DateContainers");
                timeItemsArray = new Transform[dateContainersTransform.childCount];
                for (int i = 0; i < dateContainersTransform.childCount; i++)
                {
                    timeItemsArray[i] = dateContainersTransform.GetChild(i);
                }
            }

            while (timeItemIndex < timeItemsArray.Length)
            {
                Transform timeItemTransform = timeItemsArray[timeItemIndex];
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
                timeItemIndex++;

                processedTimeItems++;

                float progress = 0.98f + ((float)processedTimeItems / totalTimeItems) * 0.02f;
                UpdateLoadingProgress(progress);

                if (updateHeightTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
                {
                    SendCustomEventDelayedFrames(nameof(UpdateItemHeightsAsync), 1);
                    return;
                }
            }

            RectTransform dateContainersRect = dateContainersTransform.GetComponent<RectTransform>();
            dateContainersRect.sizeDelta = new Vector2(dateContainersRect.sizeDelta.x, dateItemHeight);

            RectTransform dateTextRect = dateItemTransform.Find("DateTextBackground/BackgroundImage/DateText").GetComponent<RectTransform>();
            float dateTextHeight = dateTextRect.sizeDelta.y;
            dateItemHeight += dateTextHeight;

            RectTransform dateItemRect = dateItemTransform.GetComponent<RectTransform>();
            dateItemRect.sizeDelta = new Vector2(dateItemRect.sizeDelta.x, dateItemHeight);

            dateItemIndex++;
            timeItemIndex = 0;
        }

        updateHeightTime = null;
        dateTimeContainers.SetActive(true);
        UpdateLoadingProgress(1);
        PrepareEventDisplay();

        dateItemsArray = null;
        timeItemsArray = null;
    }

    private void PrepareEventDisplay()
    {
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

        if (loadingScreen != null && scrollViewCanvasGroup!= null)
        {
            Destroy(loadingScreen);
            loadingScreen = null;

            scrollViewCanvasGroup.alpha = 1;
            scrollViewCanvasGroup.interactable = true;
            scrollViewCanvasGroup.blocksRaycasts = true;
        }

        eventList.Clear();
        parsedChunksList = null;
        LoadNextDetailedImage();
    }

    private void StartImgsJsonParsing(string jsonResponse)
    {
        arrayContent_imgs = "";
        splitStartIndex_imgs = 0;
        imgsJsonChunkIndex = 0;
        imgsJsonParseIndex = 0;

        int start = jsonResponse.IndexOf('[');
        int end = jsonResponse.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            arrayContent_imgs = jsonResponse.Substring(start + 1, end - start - 1);
            SplitImgsJsonChunksAsync();
        }
        else
        {
            Debug.LogError("Failed to find valid Images JSON array in response.");
        }
    }

    public void SplitImgsJsonChunksAsync()
    {
        splitImgsProcessTime.Restart();

        while (splitStartIndex_imgs < arrayContent_imgs.Length)
        {
            int nextComma = arrayContent_imgs.IndexOf("},", splitStartIndex_imgs);
            bool isLastElement = nextComma == -1;

            int endIndex = isLastElement ? arrayContent_imgs.Length : nextComma + 1;
            string element = arrayContent_imgs.Substring(splitStartIndex_imgs, endIndex - splitStartIndex_imgs).Trim();

            if (!element.StartsWith("{")) element = $"{{{element}";
            if (!element.EndsWith("}")) element = $"{element}}}";

            imgsJsonChunksList.Add(new DataToken(element));
            splitStartIndex_imgs = endIndex + 1;

            if (splitImgsProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(SplitImgsJsonChunksAsync), 1);
                return;
            }
        }

        ParseImgsJsonChunksAsync();
    }

    public void ParseImgsJsonChunksAsync()
    {
        imgsJsonChunksParseProcessTime.Restart();

        while (imgsJsonChunkIndex < imgsJsonChunksList.Count)
        {
            string chunk = imgsJsonChunksList[imgsJsonChunkIndex].String;

            if (VRCJson.TryDeserializeFromJson(chunk, out DataToken result))
            {
                parsedChunksImgsList.Add(result);
            }
            else
            {
                Debug.LogError($"Failed to parse Images chunk {imgsJsonChunkIndex}/{imgsJsonChunksList.Count}.");
            }

            imgsJsonChunkIndex++;

            if (imgsJsonChunksParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseImgsJsonChunksAsync), 1);
                return;
            }
        }

        cachedJsonResultImgs = CombineChunksToCachedResult(parsedChunksImgsList);
        ParseDetailedImagesJson();
    }

    public void ParseDetailedImagesJson()
    {
        imgsJsonParseProcessTime.Restart();

        if (cachedJsonResultImgs.TokenType == TokenType.DataList)
        {
            while (imgsJsonParseIndex < cachedJsonResultImgs.DataList.Count)
            {
                var detailedImageToken = cachedJsonResultImgs.DataList[imgsJsonParseIndex];
                imgsJsonParseIndex++;

                if (detailedImageToken.TokenType == TokenType.DataDictionary)
                {
                    var detailedImageDict = detailedImageToken.DataDictionary;
                    DataDictionary newDetailedImgDictionary = new DataDictionary();
                    newDetailedImgDictionary.Add("content_id", new DataToken((int)detailedImageDict["content_id"].Double));
                    newDetailedImgDictionary.Add("image_id", detailedImageDict["image_id"]);
                    newDetailedImgDictionary.Add("width", new DataToken((int)detailedImageDict["width"].Double));
                    newDetailedImgDictionary.Add("height", new DataToken((int)detailedImageDict["height"].Double));
                    newDetailedImgDictionary.Add("base64DetailedImage", new DataToken(detailedImageDict["base64DetailedImage"].String));
                    detailedImagesList.Add(new DataToken(newDetailedImgDictionary));
                }
                else
                {
                    Debug.LogError("An element in detailed images is not a DataDictionary.");
                }

                if (imgsJsonParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
                {
                    SendCustomEventDelayedFrames(nameof(ParseDetailedImagesJson), 1);
                    return;
                }
            }

            cachedJsonResultImgs = default;
            loadingChunkIndex++;

            imgsJsonChunksList.Clear();
            parsedChunksImgsList.Clear();
            LoadNextDetailedImage();
        }
        else
        {
            Debug.LogError("Detailed images JSON root is not a DataList.");
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

    private void LoadNextDetailedImage()
    {
        if (loadingChunkIndex < detailedImagesChunkCount)
        {
            VRCUrl detailedImagesUrl = activeDetailedImagesUrls[loadingChunkIndex];
            VRCStringDownloader.LoadUrl(detailedImagesUrl, this.GetComponent<UdonBehaviour>());
            return;
        }

        splitImgsProcessTime = null;
        imgsJsonChunksParseProcessTime = null;
        imgsJsonParseProcessTime = null;
        activeDetailedImagesUrls = null;
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

    private void UpdateLoadingProgress(float progress)
    {
        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = progress;
        }

        if (loadingProgressText != null)
        {
            loadingProgressText.text = Mathf.CeilToInt(progress * 100f) + "%";
        }
    }

    public DataList GetDetailedImgsByContent(int contentID)
    {
        var detailedImgsByContentList = new DataList();

        for (int i = 0; i < detailedImagesList.Count; i++)
        {
            var detailedImgsDataDictionary = detailedImagesList[i];
            if (detailedImgsDataDictionary.TokenType == TokenType.DataDictionary)
            {
                var detailedImgsDict = detailedImgsDataDictionary.DataDictionary;
                if (detailedImgsDict["content_id"].Int == contentID)
                {
                    detailedImgsByContentList.Add(detailedImgsDict);
                }
            }
        }

        return detailedImgsByContentList;
    }

    public TextureFormat GetTextureFormat()
    {
        return textureFormat;
    }

    public VRCUrl FindMatchingVRCUrl(string targetUrl)
    {
        foreach (VRCUrl url in detailedVideoUrls)
        {
            if (url.Get() == targetUrl)
            {
                return url;
            }
        }

        return VRCUrl.Empty;
    }
}
