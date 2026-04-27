
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
using System.Text;
using TMPro;
using WonderNote.EventTimeTable;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EventTimetable : UdonSharpBehaviour
{
    [Header("API Settings")]
    [SerializeField, HideInInspector] private VRCUrl pcTimetableApiUrl;
    [SerializeField, HideInInspector] private VRCUrl androidTimetableApiUrl;
    [SerializeField, HideInInspector] private int[] presetTagIds = new int[0];
    private VRCUrl activeTimetableApiUrl;

    [SerializeField, HideInInspector] private VRCUrl[] pcDetailedImagesUrls;
    [SerializeField, HideInInspector] private VRCUrl[] androidDetailedImagesUrls;
    private VRCUrl[] activeDetailedImagesUrls;

    [SerializeField, HideInInspector] private VRCUrl[] detailedVideoUrls;

    [Header("Event Item Settings")]
    [SerializeField] private GameObject dateTimeContainers;
    [SerializeField] private GameObject dateItemPrefab;
    [SerializeField] private GameObject timeItemPrefab;
    [SerializeField] private GameObject eventItemPrefab;
    private RectTransform dateTimeContainersRect;

    [Header("Default Image Settings")]
    [SerializeField] private Image backGroundImage;
    [SerializeField, HideInInspector] private Sprite backGroundSprite;
    [SerializeField] private Sprite parseErrorSprite;
    [SerializeField] private Sprite noEventsSprite;
    [SerializeField] private Sprite loadErrorSprite;
    [SerializeField] private Texture2D blankLogoImage;
    [SerializeField] private GameObject previewQuad;
    [SerializeField] private GameObject reflowCurtain;

    [Header("Scrollbar Settings")]
    [SerializeField] private ScrollInputHandler mainScrollHandler;
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
    private int currentStickyIndex = -1;

    private float lerpTime = 0.2f;
    private float currentLerpTime = 0f;
    private float lerpProgress;

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
    [SerializeField] private GameObject tagBadgePrefab;
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

    private DataList eventList = new DataList();
    private DataList detailedImagesList = new DataList();
    private int gridLayoutPaddingLeft = 37;
    private int gridLayoutPaddingRight = 37;
    private int gridLayoutPaddingTop = 25;
    private int gridLayoutPaddingBottom = 18;
    private TextureFormat textureFormat;

    private const int FRAME_PROCESS_LIMIT_MS = 5;
    private int jsonParseIndex = 0;
    private DataToken cachedDetailedJsonResult;
    private DataToken cachedJsonResult;    

    private System.Diagnostics.Stopwatch splitProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch jsonChunksParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch jsonParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch eventDisplayTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch updateHeightTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch splitDetailedProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch detailedJsonChunksParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch detailedJsonParseProcessTime = new System.Diagnostics.Stopwatch();

    private bool[] parseResult = { false, true };

    private int jsonChunkIndex = 0;
    private DataList jsonChunksList = new DataList();
    private DataList parsedChunksList = new DataList();
    private int splitStartIndex = 0;
    private string arrayContent;

    private int detailedJsonChunkIndex = 0;
    private DataList detailedJsonChunksList = new DataList();
    private DataList parsedDetailedChunksList = new DataList();
    private int detailedSplitStartIndex = 0;
    private string detailedArrayContent;

    private int dateItemIndex = 0;
    private int timeItemIndex = 0;
    private Transform[] dateItemsArray;
    private Transform dateItemTransform;
    private Transform dateContainersTransform;
    private Transform[] timeItemsArray;
    private float dateItemHeight;
    private int eventDisplayIndex = 0;

    private const int MAX_RUNTIME_TEXTURES = 1600;
    [HideInInspector] public Texture2D[] runtimeTextures;
    [HideInInspector] public int thumbnailTextureCount = 0;
    [HideInInspector] public int runtimeTextureCount = 0;

    private int appliesThisFrame;
    private byte[][] thumbnailBytes;
    private byte[] wnpkBytes;
    private int[] wnpkThumbOffsets;
    private int[] wnpkThumbLengths;
    private string[] parsedWnpkMetaJson;
    private int[] parsedWnpkOffsets;
    private int[] parsedWnpkLengths;
    private const int DETAILED_PACKS_PER_SLOT = 512;

    private string[] detailedImageIds;
    private byte[][] detailedImageBytes;
    private int detailedImageParseIndex = 0;
    private DataList fetchedDetailedImagesUrls = new DataList();
    private byte[] detailedWnpkBytes;
    private int[] detailedWnpkOffsets;
    private int[] detailedWnpkLengths;
    private VRCUrl requestedDetailedImagesUrl;
    private DetailsPanelController requestedDetailsPanel;

    [Header("Virtualization Settings")]
    [SerializeField] private GameObject proximityMessage;
    [SerializeField] private Image proximityImage;
    [SerializeField] private Sprite iosUnsupportedSprite;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private ProximityToggle proximityToggle;
    [SerializeField] private GameObject titleRoot;
    [SerializeField] private GameObject footerRoot;
    private RectTransform[] eventItemRects;
    private GameObject[] eventItemObjects;
    private int eventItemCount;
    private bool isLoaded = false;
    private Vector3[] _tmpCorners = new Vector3[4];

    [Header("Wing UI Settings")]
    [SerializeField] private LeftWingController wingController;
    [SerializeField] private GameObject wingPanel;
    private bool _schemaReady = false;
    private bool _eventLayoutReady = false;
    private bool _wingShown = false;
    private DataList schemaCategoriesList = new DataList();
    private DataList schemaTagsList = new DataList();
    private DataList schemaTagGroupsList = new DataList();
    private DataList schemaProgramsList = new DataList();
    private int[] eventCategoryIds;
    private int[][] eventTagIds;
    private int[] eventProgramIds;
    private EventItemScript[] eventItemScripts;
    private int[] eventTypeIds;
    private int[] eventSupportsMobile;

    private bool _ticksPaused = false;

    private float reflowBudgetMs = 3.5f;
    private int _rfActiveTicket = 0;
    private int _rfRunTicket = 0;
    private int _rf_di = 0;
    private int _rf_ti = 0;
    private float _rf_dateHeight = 0f;
    private int _rf_activeTimeCount = 0;
    private bool _rf_hasAnyDateActive = false;
    private System.Diagnostics.Stopwatch _rfSw = new System.Diagnostics.Stopwatch();

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

        previewQuad.SetActive(false);
        backGroundImage.sprite = backGroundSprite;
        SetScrollPositionImmediate(1.0f);
        returnButtonImage.color = new Color(1f, 1f, 1f, 0f);
        returnButton.SetActive(false);
        FooterText.text = "■イベントの登録はウェブサイト (https://wondernote.net/) から　■アセットのダウンロードはVCC・GitHub・BOOTHから　※詳しくは左記サイトをご覧ください";

        #if UNITY_IOS
            proximityImage.sprite = iosUnsupportedSprite;
        #endif

        ApplyOutsideView();
    }

    public void ApplyInsideView()
    {
        titleRoot.SetActive(true);
        footerRoot.SetActive(true);
        proximityMessage.SetActive(false);
        wingController.SetModalBackdrop(true);
    }

    public void ApplyOutsideView()
    {
        titleRoot.SetActive(false);
        footerRoot.SetActive(false);
        proximityMessage.SetActive(true);
        wingController.SetModalBackdrop(false);
    }

    public void BeginLoad()
    {
        if (isLoaded) return;

        isLoaded = true;
        loadingScreen.SetActive(true);
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
            StartWnpkParsing(download.ResultBytes);
        }
        else if (download.Url == requestedDetailedImagesUrl)
        {
            StartDetailedWnpkParsing(download.ResultBytes);
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (result.Url == activeTimetableApiUrl)
        {
            if (loadingScreen != null)
            {
                loadingScreen.SetActive(false);
            }
            backGroundImage.sprite = loadErrorSprite;
            Debug.LogError($"Error loading main timetable string from {result.Url}: {result.ErrorCode} - {result.Error}");
        }
        else if (result.Url == requestedDetailedImagesUrl)
        {
            Debug.LogError($"Error loading detailed_images pack from {result.Url}: {result.ErrorCode} - {result.Error}");
            ClearDetailedTemporaryState();
            requestedDetailedImagesUrl = VRCUrl.Empty;
            requestedDetailsPanel = null;
        }
    }

    private void StartWnpkParsing(byte[] packBytes)
    {
        if (!TryParseWnpkCommon(packBytes, "WNPK parse error"))
        {
            SetIsSuccess(false);
            SetIsEmpty(true);
            ContinueOnStringLoadSuccess();
            return;
        }

        wnpkBytes = packBytes;
        wnpkThumbOffsets = parsedWnpkOffsets;
        wnpkThumbLengths = parsedWnpkLengths;

        if (string.IsNullOrEmpty(parsedWnpkMetaJson[0]))
        {
            Debug.LogError("Timetable WNPK parse error: events section is missing.");
            SetIsSuccess(false);
            SetIsEmpty(true);
            ContinueOnStringLoadSuccess();
            return;
        }

        if (!string.IsNullOrEmpty(parsedWnpkMetaJson[1]))
        {
            ParseSchema(parsedWnpkMetaJson[1]);
        }
        else
        {
            Debug.LogError("Timetable WNPK parse warning: schema section is empty.");
        }

        StartJsonParsing(parsedWnpkMetaJson[0]);

        parsedWnpkMetaJson = null;
        parsedWnpkOffsets = null;
        parsedWnpkLengths = null;
    }

    private bool TryParseWnpkCommon(byte[] packBytes, string errorPrefix)
    {
        parsedWnpkMetaJson = null;
        parsedWnpkOffsets = null;
        parsedWnpkLengths = null;

        if (packBytes == null || packBytes.Length < 9)
        {
            Debug.LogError($"{errorPrefix}: pack is null or too small.");
            return false;
        }

        if (packBytes[0] != (byte)'W' || packBytes[1] != (byte)'N' || packBytes[2] != (byte)'P' || packBytes[3] != (byte)'K')
        {
            Debug.LogError($"{errorPrefix}: magic mismatch.");
            return false;
        }

        int cursor = 4;
        int version = packBytes[cursor];
        cursor += 1;

        int jsonSectionCount = ReadInt32LE(packBytes, cursor);
        cursor += 4;

        if (jsonSectionCount <= 0)
        {
            Debug.LogError($"{errorPrefix}: jsonSectionCount <= 0.");
            return false;
        }

        parsedWnpkMetaJson = new string[jsonSectionCount];

        for (int i = 0; i < jsonSectionCount; i++)
        {
            if (cursor + 4 > packBytes.Length)
            {
                Debug.LogError($"{errorPrefix}: missing json length for section #{i}.");
                return false;
            }

            int jsonLen = ReadInt32LE(packBytes, cursor);
            cursor += 4;

            if (jsonLen < 0 || cursor + jsonLen > packBytes.Length)
            {
                Debug.LogError($"{errorPrefix}: invalid json length for section #{i}.");
                return false;
            }

            byte[] jsonBytes = new byte[jsonLen];
            Array.Copy(packBytes, cursor, jsonBytes, 0, jsonLen);
            cursor += jsonLen;

            parsedWnpkMetaJson[i] = Encoding.UTF8.GetString(jsonBytes);
        }

        if (cursor + 4 > packBytes.Length)
        {
            Debug.LogError($"{errorPrefix}: missing image count.");
            return false;
        }

        int imgCount = ReadInt32LE(packBytes, cursor);
        cursor += 4;

        if (imgCount < 0)
        {
            Debug.LogError($"{errorPrefix}: imgCount < 0.");
            return false;
        }

        parsedWnpkOffsets = new int[imgCount];
        parsedWnpkLengths = new int[imgCount];

        for (int i = 0; i < imgCount; i++)
        {
            if (cursor + 4 > packBytes.Length)
            {
                Debug.LogError($"{errorPrefix}: missing length for image #{i}.");
                parsedWnpkMetaJson = null;
                parsedWnpkOffsets = null;
                parsedWnpkLengths = null;
                return false;
            }

            int len = ReadInt32LE(packBytes, cursor);
            cursor += 4;

            if (len < 0 || cursor + len > packBytes.Length)
            {
                Debug.LogError($"{errorPrefix}: invalid length for image #{i}.");
                parsedWnpkMetaJson = null;
                parsedWnpkOffsets = null;
                parsedWnpkLengths = null;
                return false;
            }

            parsedWnpkOffsets[i] = cursor;
            parsedWnpkLengths[i] = len;
            cursor += len;
        }

        return true;
    }

    private int ReadInt32LE(byte[] bytes, int offset)
    {
        int b0 = (int)bytes[offset + 0];
        int b1 = (int)bytes[offset + 1] << 8;
        int b2 = (int)bytes[offset + 2] << 16;
        int b3 = (int)bytes[offset + 3] << 24;
        return b0 | b1 | b2 | b3;
    }

    public byte[] GetThumbnailBytesFromWnpk(int index)
    {
        if (wnpkBytes == null || wnpkThumbOffsets == null || wnpkThumbLengths == null) return null;
        if (index < 0 || index >= wnpkThumbLengths.Length) return null;

        int len = wnpkThumbLengths[index];
        if (len <= 0) return null;

        int off = wnpkThumbOffsets[index];
        if (off < 0 || off + len > wnpkBytes.Length) return null;

        // byte[] dst = new byte[len];
        // Array.Copy(wnpkBytes, off, dst, 0, len);
        // return dst;

        // ------- StringLoading 改善までの処置（将来的に削除） ------- 
        byte[] base64Bytes = new byte[len];
        Array.Copy(wnpkBytes, off, base64Bytes, 0, len);

        string base64 = Encoding.UTF8.GetString(base64Bytes);
        if (string.IsNullOrEmpty(base64)) return null;

        return Convert.FromBase64String(base64);
        // ------- ここまで ------- 
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
            UpdateLoadingProgress(progress * 0.01f);

            if (splitProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(SplitJsonChunksAsync), 1);
                return;
            }
        }

        splitProcessTime.Stop();
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

            float progress = (float)jsonChunkIndex / jsonChunksListCount;
            UpdateLoadingProgress(0.01f + progress * 0.01f);

            if (jsonChunksParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseJsonChunksAsync), 1);
                return;
            }
        }

        cachedJsonResult = CombineChunksToCachedResult(parsedChunksList);
        jsonChunksParseProcessTime.Stop();
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

        if (thumbnailBytes == null || thumbnailBytes.Length != cachedJsonResultCount) {
            thumbnailBytes = new byte[cachedJsonResultCount][];
        }

        while (jsonParseIndex < cachedJsonResultCount)
        {
            var eventToken = cachedJsonResult.DataList[jsonParseIndex];
            int idx = jsonParseIndex;
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

                newEventDictionary.Add("category_label", eventDictionary["category_label"]);
                newEventDictionary.Add("category_color", eventDictionary["category_color"]);
                newEventDictionary.Add("category_id", new DataToken((int)eventDictionary["category_id"].Double));
                newEventDictionary.Add("tag_ids", eventDictionary["tag_ids"]);
                if (eventDictionary.ContainsKey("program_id") && !eventDictionary["program_id"].IsNull) {
                    newEventDictionary.Add("program_id", new DataToken((int)eventDictionary["program_id"].Double));
                } else {
                    newEventDictionary.Add("program_id", new DataToken(-1));
                }
                newEventDictionary.Add("is_recurring", new DataToken((int)eventDictionary["is_recurring"].Double));
                newEventDictionary.Add("supports_mobile", new DataToken((int)eventDictionary["supports_mobile"].Double));
                newEventDictionary.Add("detail_slot", new DataToken((int)eventDictionary["detail_slot"].Double));
                newEventDictionary.Add("detail_pack_no", new DataToken((int)eventDictionary["detail_pack_no"].Double));

                thumbnailBytes[idx] = GetThumbnailBytesFromWnpk((int)eventDictionary["thumbnail_index"].Double);

                eventList.Add(new DataToken(newEventDictionary));

            }
            else
            {
                Debug.LogError("An element in eventToken is not a DataDictionary.");
            }

            float progress = (float)jsonParseIndex / cachedJsonResultCount;
            UpdateLoadingProgress(0.02f + progress * 0.06f);

            if (jsonParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseThumbnailListAsync), 1);
                return;
            }
        }

        wnpkBytes = null;
        wnpkThumbOffsets = null;
        wnpkThumbLengths = null;

        jsonParseProcessTime.Stop();
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
        parseResult = new bool[]{ false, true };

        if (isJsonParsed)
        {
            if (hasEvents) {
                parseErrorSprite = null;
                loadErrorSprite = null;
                cachedJsonResult = default;
                runtimeTextures = new Texture2D[MAX_RUNTIME_TEXTURES];

                DisplayEventsAsync();
            } else {
                if (loadingScreen != null)
                {
                    loadingScreen.SetActive(false);
                }
                backGroundImage.sprite = noEventsSprite;
                return;
            }
        } else {
            if (loadingScreen != null)
            {
                loadingScreen.SetActive(false);
            }
            backGroundImage.sprite = parseErrorSprite;
            return;
        }
    }

    public void DisplayEventsAsync()
    {
        appliesThisFrame = 0;
        eventDisplayTime.Restart();

        int eventListCount = eventList.Count;

        if (eventItemScripts == null || eventItemScripts.Length != eventListCount)
        {
            eventItemScripts = new EventItemScript[eventListCount];
            eventCategoryIds = new int[eventListCount];
            eventTagIds = new int[eventListCount][];
            eventProgramIds = new int[eventListCount];
            eventTypeIds = new int[eventListCount];
            eventSupportsMobile = new int[eventListCount];
        }

        while (eventDisplayIndex < eventListCount)
        {
            if (appliesThisFrame >= 1) {
                SendCustomEventDelayedFrames(nameof(DisplayEventsAsync), 1);
                return;
            }

            DataDictionary eventData = eventList[eventDisplayIndex].DataDictionary;
            string title = eventData["title"].String;
            string categoryLabel = eventData["category_label"].String;
            Color categoryColor = ParseHexColor(eventData["category_color"].String);
            int idx = eventDisplayIndex;
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
                    eventItemScript.SetCategory(categoryLabel, categoryColor);

                    int thumbnailWidth = eventData["thumbnail_width"].Int;
                    int thumbnailHeight = eventData["thumbnail_height"].Int;

                    byte[] imageBytes = thumbnailBytes[idx];

                    if (imageBytes == null || imageBytes.Length == 0) {
                        eventItemScript.SetThumbnailImage(blankLogoImage, false);
                    } else {
                        Texture2D newTexture = new Texture2D(thumbnailWidth, thumbnailHeight, textureFormat, false, false);
                        newTexture.LoadRawTextureData(imageBytes);

                        newTexture.Apply(false, true);
                        appliesThisFrame++;

                        if (TryRegisterRuntimeTexture(newTexture)) {
                            eventItemScript.SetThumbnailImage(newTexture, true);
                            thumbnailTextureCount++;
                        } else {
                            eventItemScript.SetThumbnailImage(blankLogoImage, false);
                        }
                    }

                    thumbnailBytes[idx] = null;

                    int contentID = eventData["content_id"].Int;
                    string summary = eventData["summary"].String;
                    string details = !eventData["details"].IsNull ? eventData["details"].String : null;
                    string groupId = !eventData["group_id"].IsNull ? eventData["group_id"].String : null;
                    int supportedModel = eventData["supported_model"].Int;
                    int detailSlot = eventData.ContainsKey("detail_slot") ? eventData["detail_slot"].Int : 0;
                    int detailPackNo = eventData.ContainsKey("detail_pack_no") ? eventData["detail_pack_no"].Int : 0;

                    DataList tagsList = new DataList();
                    if (eventData.ContainsKey("tag_ids") && eventData["tag_ids"].TokenType == TokenType.DataList) {
                        tagsList = eventData["tag_ids"].DataList;
                    }
                    int[] tagIds = new int[tagsList.Count];
                    for (int ti = 0; ti < tagsList.Count; ti++)
                        tagIds[ti] = (int)tagsList[ti].Double;
                    eventTagIds[idx] = tagIds;
                    string[] tagLabels = BuildTagLabels(tagsList);

                    eventItemScript.SetDetails(contentID, summary, details, groupId, supportedModel, detailSlot, detailPackNo, tagLabels, canvas, detailsPanelPrefab, tagBadgePrefab, detailsTextPrefab, detailsImagePrefab, videoPlayerPrefabs, linkedFieldContainerPrefab, mainPanelCanvasGroup, audioManager, this);
                    eventItemScript.SetProximityToggle(proximityToggle);

                    int catId = -1;
                    if (eventData.ContainsKey("category_id") && !eventData["category_id"].IsNull) {
                        catId = eventData["category_id"].Int;
                    }
                    eventCategoryIds[idx] = catId;

                    int programId = -1;
                    if (eventData.ContainsKey("program_id") && !eventData["program_id"].IsNull) {
                        programId = eventData["program_id"].Int;
                    }
                    eventProgramIds[idx] = programId;

                    int isRecurring = 0;
                    if (eventData.ContainsKey("is_recurring") && !eventData["is_recurring"].IsNull) {
                        isRecurring = eventData["is_recurring"].Int;
                    }
                    eventTypeIds[idx] = (isRecurring == 1) ? 1 : 0;

                    int sup = 0;
                    if (eventData.ContainsKey("supports_mobile") && !eventData["supports_mobile"].IsNull) {
                        sup = eventData["supports_mobile"].Int;
                    }
                    eventSupportsMobile[idx] = sup;

                    eventItemScripts[idx] = eventItemScript;
                }
            }
            else
            {
                Debug.LogError($"Event date time format is invalid: {eventData["datetime"].String}");
            }

            float progress = (float)eventDisplayIndex / eventListCount;
            UpdateLoadingProgress(0.08f + progress * 0.90f);

            if (eventDisplayTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(DisplayEventsAsync), 1);
                return;
            }
        }

        eventDisplayTime.Stop();
        StartUpdateItemHeights();
    }

    public Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.white;

        if (hex[0] == '#') hex = hex.Substring(1);
        if (hex.Length == 6) hex += "FF";
        if (hex.Length != 8) return Color.white;

        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
        int a = Convert.ToInt32(hex.Substring(6, 2), 16);
        return new Color(r/255f, g/255f, b/255f, a/255f);
    }

    public void OnScrollValueChanged()
    {
        VirtualizeItems();
    }

    private void VirtualizeItems()
    {
        if (eventItemRects == null || eventItemObjects == null) return;

        Rect vpRect = viewport.rect;

        for (int i = 0; i < eventItemCount; i++)
        {
            RectTransform itemRT = eventItemRects[i];
            GameObject itemGO = eventItemObjects[i];
            if (itemRT == null || itemGO == null) continue;

            Transform p = itemGO.transform;
            Transform timeItem = null;
            while (p != null)
            {
                if (p.name.StartsWith("Time")) {
                    timeItem = p; break;
                }
                p = p.parent;
            }
            if (timeItem == null || !timeItem.gameObject.activeInHierarchy) continue;

            Vector3[] corners = _tmpCorners;
            itemRT.GetLocalCorners(corners);

            bool visible = false;
            for (int c = 0; c < 4; c++)
            {
                Vector3 world = itemRT.TransformPoint(corners[c]);
                Vector3 localPos = viewport.InverseTransformPoint(world);
                if (localPos.x >= vpRect.xMin && localPos.x <= vpRect.xMax && localPos.y >= vpRect.yMin && localPos.y <= vpRect.yMax)
                {
                    visible = true;
                    break;
                }
            }

            var script = itemGO.GetComponent<EventItemScript>();
            if (script != null) script.SetVisualEnabled(visible);
        }
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

                InitializePlaceholderPool(timeContainersTransform, itemsPerRow);

                int lastItemCount = childCount % itemsPerRow;
                int numOfBlanks = (lastItemCount == 0) ? 0 : itemsPerRow - lastItemCount;
                FillWithPlaceholders(timeContainersTransform, childCount, numOfBlanks);

                RectTransform timeTextRect = timeItemTransform.Find("TimeText").GetComponent<RectTransform>();
                float timeTextHeight = timeTextRect.sizeDelta.y;
                timeItemHeight += timeTextHeight;

                RectTransform timeItemRect = timeItemTransform.GetComponent<RectTransform>();
                timeItemRect.sizeDelta = new Vector2(timeItemRect.sizeDelta.x, timeItemHeight);

                dateItemHeight += timeItemHeight;
                timeItemIndex++;

                processedTimeItems++;

                float progress = 0.98f + ((float)processedTimeItems / totalTimeItems) * 0.01f;
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

        updateHeightTime.Stop();
        dateTimeContainers.SetActive(true);
        UpdateLoadingProgress(0.99f);
        SendCustomEventDelayedFrames(nameof(ShowLoading100AndPrepareDisplay), 1);

        dateItemsArray = null;
        timeItemsArray = null;
    }

    public void ShowLoading100AndPrepareDisplay()
    {
        UpdateLoadingProgress(1);
        SendCustomEventDelayedSeconds(nameof(PrepareEventDisplay), 0.15f);
    }

    private void InitializePlaceholderPool(Transform timeContainers, int itemsPerRow)
    {
        int cap = Mathf.Max(0, itemsPerRow - 1);
        if (cap == 0) return;

        for (int i = 0; i < cap; i++)
        {
            GameObject eventItem = Instantiate(eventItemPrefab, timeContainers);
            EventItemScript eventItemScript = eventItem.GetComponent<EventItemScript>();
            if (eventItemScript != null) eventItemScript.SetupAsPlaceholder(blankLogoImage);
            eventItem.SetActive(false);
        }
    }

    private void FillWithPlaceholders(Transform timeContainers, int realChildCount, int blanksNum)
    {
        int used = 0;
        for (int c = 0; c < timeContainers.childCount && used < blanksNum; c++)
        {
            var child = timeContainers.GetChild(c);
            EventItemScript eventItemScript = child.GetComponent<EventItemScript>();
            if (eventItemScript != null && eventItemScript.IsPlaceholder) {
                child.SetSiblingIndex(realChildCount + used);
                child.gameObject.SetActive(true);
                used++;
            }
        }
    }

    public void PrepareEventDisplay()
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
        mainScrollHandler.SyncToScrollPosition();
        InitializeStickyDate();

        if (loadingScreen != null && scrollViewCanvasGroup!= null)
        {
            loadingScreen.SetActive(false);
            wingController.BeginTagGroupBuild();
            _eventLayoutReady = true;
            TryShowWingPanel();

            scrollViewCanvasGroup.alpha = 1;
            scrollViewCanvasGroup.interactable = true;
            scrollViewCanvasGroup.blocksRaycasts = true;
        }

        proximityToggle.OnLoadComplete();
        eventList.Clear();
        parsedChunksList = null;

        CaptureInitialOrderKeys();
        CacheAllEventItemsForVirtualization();
        VirtualizeItems();
    }

    private void CaptureInitialOrderKeys()
    {
        Transform dateItemTransform = dateTimeContainers.transform;
        int dateCount = dateItemTransform.childCount;

        for (int di = 0; di < dateCount; di++)
        {
            Transform dateItem = dateItemTransform.GetChild(di);
            Transform dateContainers = dateItem.Find("DateContainers");
            if (dateContainers == null) continue;

            int timeCount = dateContainers.childCount;
            for (int ti = 0; ti < timeCount; ti++)
            {
                Transform timeItem = dateContainers.GetChild(ti);
                if (timeItem == null) continue;

                Transform timeContainers = timeItem.Find("TimeContainers");
                if (timeContainers == null) continue;

                int stable = 0;
                int childCount = timeContainers.childCount;

                for (int c = 0; c < childCount; c++)
                {
                    Transform child = timeContainers.GetChild(c);
                    var eventItemScript = child.GetComponent<EventItemScript>();
                    if (eventItemScript == null) continue;
                    if (eventItemScript.IsPlaceholder) continue;

                    eventItemScript.SetStableIndex(stable++);
                }
            }
        }
    }

    private void CacheAllEventItemsForVirtualization()
    {
        var scripts = dateTimeContainers.GetComponentsInChildren<EventItemScript>(true);

        int n = scripts.Length;
        eventItemCount = n;
        eventItemObjects = new GameObject[n];
        eventItemRects = new RectTransform[n];

        for (int i = 0; i < n; i++)
        {
            var script = scripts[i];
            if (script == null) continue;
            eventItemObjects[i] = script.gameObject;
            eventItemRects[i] = script.GetComponent<RectTransform>();
        }
    }

    private void ParseSchema(string json)
    {
        if (!VRCJson.TryDeserializeFromJson(json, out DataToken root) || root.TokenType != TokenType.DataDictionary)
        {
            Debug.LogError("Schema JSON parse failed.");
            return;
        }

        var rootDict = root.DataDictionary;

        if (rootDict.ContainsKey("categories") && rootDict["categories"].TokenType == TokenType.DataList) {
            schemaCategoriesList = rootDict["categories"].DataList;
        } else {
            Debug.LogError("Schema JSON: 'categories' is missing or not a list.");
        }

        if (rootDict.ContainsKey("tags") && rootDict["tags"].TokenType == TokenType.DataList) {
            schemaTagsList = rootDict["tags"].DataList;
        } else {
            Debug.LogError("Schema JSON: 'tags' is missing or not a list.");
        }

        if (rootDict.ContainsKey("tag_groups") && rootDict["tag_groups"].TokenType == TokenType.DataList) {
            schemaTagGroupsList = rootDict["tag_groups"].DataList;
        } else {
            Debug.LogError("Schema JSON: 'tag_groups' is missing or not a list.");
        }

        if (rootDict.ContainsKey("programs") && rootDict["programs"].TokenType == TokenType.DataList) {
            schemaProgramsList = rootDict["programs"].DataList;
        } else {
            Debug.LogError("Schema JSON: 'programs' is missing or not a list.");
        }

        wingController.SetupChipsFromSchema(schemaCategoriesList, schemaTagsList, schemaTagGroupsList, schemaProgramsList);

        schemaCategoriesList = new DataList();
        schemaTagGroupsList = new DataList();
        schemaProgramsList = new DataList();

        _schemaReady = true;

        SendCustomEventDelayedFrames(nameof(TryShowWingPanel), 1);
    }

    private string[] BuildTagLabels(DataList tagIdsList)
    {
        string[] tagLabels = new string[tagIdsList.Count];

        for (int i = 0; i < tagIdsList.Count; i++)
        {
            int tagId = (int)tagIdsList[i].Double;
            tagLabels[i] = GetTagLabelById(tagId);
        }

        return tagLabels;
    }

    private string GetTagLabelById(int tagId)
    {
        for (int i = 0; i < schemaTagsList.Count; i++)
        {
            DataDictionary tagDict = schemaTagsList[i].DataDictionary;
            if (tagDict["id"].IsNull) continue;
            if ((int)tagDict["id"].Double != tagId) continue;
            if (tagDict["label"].IsNull) return "";
            return tagDict["label"].String;
        }

        return "";
    }

    public void TryShowWingPanel()
    {
        if (_wingShown) return;
        if (!_schemaReady || !_eventLayoutReady) return;

        wingPanel.SetActive(true);
        _wingShown = true;

        wingController.SetNeedsScrollbarFinalize();
    }

    private void StartDetailedWnpkParsing(byte[] packBytes)
    {
        if (!TryParseWnpkCommon(packBytes, "Detailed WNPK parse error"))
        {
            ClearDetailedTemporaryState();
            requestedDetailedImagesUrl = VRCUrl.Empty;
            requestedDetailsPanel = null;
            return;
        }

        detailedWnpkBytes = packBytes;
        detailedWnpkOffsets = parsedWnpkOffsets;
        detailedWnpkLengths = parsedWnpkLengths;

        StartDetailedJsonParsing(parsedWnpkMetaJson[0]);

        parsedWnpkMetaJson = null;
        parsedWnpkOffsets = null;
        parsedWnpkLengths = null;
    }

    private void StartDetailedJsonParsing(string jsonResponse)
    {
        detailedArrayContent = null;
        detailedSplitStartIndex = 0;
        detailedJsonChunkIndex = 0;
        detailedImageParseIndex = 0;
        cachedDetailedJsonResult = default;

        int start = jsonResponse.IndexOf('[');
        int end = jsonResponse.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            detailedArrayContent = jsonResponse.Substring(start + 1, end - start - 1);
            SplitDetailedJsonChunksAsync();
        }
        else
        {
            Debug.LogError("Failed to find valid detailed WNPK meta JSON array.");
            ClearDetailedTemporaryState();
            requestedDetailedImagesUrl = VRCUrl.Empty;
            requestedDetailsPanel = null;
        }
    }

    public void SplitDetailedJsonChunksAsync()
    {
        splitDetailedProcessTime.Restart();

        while (detailedSplitStartIndex < detailedArrayContent.Length)
        {
            int nextComma = detailedArrayContent.IndexOf("},", detailedSplitStartIndex);
            bool isLastElement = nextComma == -1;

            int endIndex = isLastElement ? detailedArrayContent.Length : nextComma + 1;
            string element = detailedArrayContent.Substring(detailedSplitStartIndex, endIndex - detailedSplitStartIndex).Trim();

            if (!element.StartsWith("{")) element = $"{{{element}";
            if (!element.EndsWith("}")) element = $"{element}}}";

            detailedJsonChunksList.Add(new DataToken(element));
            detailedSplitStartIndex = endIndex + 1;

            if (splitDetailedProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(SplitDetailedJsonChunksAsync), 1);
                return;
            }
        }

        splitDetailedProcessTime.Stop();
        ParseDetailedJsonChunksAsync();
    }

    public void ParseDetailedJsonChunksAsync()
    {
        detailedJsonChunksParseProcessTime.Restart();

        while (detailedJsonChunkIndex < detailedJsonChunksList.Count)
        {
            string chunk = detailedJsonChunksList[detailedJsonChunkIndex].String;

            if (VRCJson.TryDeserializeFromJson(chunk, out DataToken result))
            {
                parsedDetailedChunksList.Add(result);
            }
            else
            {
                Debug.LogError($"Failed to parse detailed JSON chunk {detailedJsonChunkIndex}/{detailedJsonChunksList.Count}.");
            }

            detailedJsonChunkIndex++;

            if (detailedJsonChunksParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseDetailedJsonChunksAsync), 1);
                return;
            }
        }

        cachedDetailedJsonResult = CombineChunksToCachedResult(parsedDetailedChunksList);
        detailedJsonChunksParseProcessTime.Stop();
        ParseDetailedImagesJson();
    }

    public void ParseDetailedImagesJson()
    {
        detailedJsonParseProcessTime.Restart();

        if (cachedDetailedJsonResult.TokenType == TokenType.DataList)
        {
            int count = cachedDetailedJsonResult.DataList.Count;

            if (detailedImageParseIndex == 0) {
                int oldCount = (detailedImageIds != null) ? detailedImageIds.Length : 0;

                string[] oldIds = detailedImageIds;
                byte[][] oldBytes = detailedImageBytes;

                detailedImageIds = new string[oldCount + count];
                detailedImageBytes = new byte[oldCount + count][];

                for (int i = 0; i < oldCount; i++)
                {
                    detailedImageIds[i] = oldIds[i];
                    detailedImageBytes[i] = oldBytes[i];
                }
            }

            int baseIndex = detailedImageIds.Length - count;

            while (detailedImageParseIndex < count)
            {
                int packIndex = detailedImageParseIndex;
                var detailedImageToken = cachedDetailedJsonResult.DataList[packIndex];
                int idx = baseIndex + packIndex;
                detailedImageParseIndex++;

                if (detailedImageToken.TokenType == TokenType.DataDictionary)
                {
                    var detailedImageDict = detailedImageToken.DataDictionary;

                    string imageId = detailedImageDict["image_id"].String;
                    detailedImageIds[idx] = imageId;

                    DataDictionary newDetailedImgDictionary = new DataDictionary();
                    newDetailedImgDictionary.Add("content_id", new DataToken((int)detailedImageDict["content_id"].Double));
                    newDetailedImgDictionary.Add("image_id", detailedImageDict["image_id"]);
                    newDetailedImgDictionary.Add("width", new DataToken((int)detailedImageDict["width"].Double));
                    newDetailedImgDictionary.Add("height", new DataToken((int)detailedImageDict["height"].Double));
                    detailedImageBytes[idx] = GetDetailedBytesFromWnpk(packIndex);
                    detailedImagesList.Add(new DataToken(newDetailedImgDictionary));
                }
                else
                {
                    Debug.LogError("An element in detailed WNPK meta is not a DataDictionary.");
                }

                if (detailedJsonParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
                {
                    SendCustomEventDelayedFrames(nameof(ParseDetailedImagesJson), 1);
                    return;
                }
            }

            RegisterFetchedDetailedImagesUrl(requestedDetailedImagesUrl);

            ClearDetailedTemporaryState();

            if (requestedDetailsPanel != null) {
                requestedDetailsPanel.OnDetailedImagesLoaded();
            }

            requestedDetailedImagesUrl = VRCUrl.Empty;
            requestedDetailsPanel = null;
        } else {
            Debug.LogError("Detailed WNPK meta root is not a DataList.");
            ClearDetailedTemporaryState();
            requestedDetailedImagesUrl = VRCUrl.Empty;
            requestedDetailsPanel = null;
            return;
        }
    }
    
    private byte[] GetDetailedBytesFromWnpk(int index)
    {
        if (detailedWnpkBytes == null || detailedWnpkOffsets == null || detailedWnpkLengths == null) return null;
        if (index < 0 || index >= detailedWnpkLengths.Length) return null;

        int len = detailedWnpkLengths[index];
        if (len <= 0) return null;

        int off = detailedWnpkOffsets[index];
        if (off < 0 || off + len > detailedWnpkBytes.Length) return null;

        byte[] dst = new byte[len];
        Array.Copy(detailedWnpkBytes, off, dst, 0, len);
        return dst;
    }

    private void ClearDetailedTemporaryState()
    {
        detailedWnpkBytes = null;
        detailedWnpkOffsets = null;
        detailedWnpkLengths = null;

        detailedArrayContent = null;
        detailedSplitStartIndex = 0;
        detailedJsonChunkIndex = 0;
        detailedImageParseIndex = 0;
        cachedDetailedJsonResult = default;

        if (detailedJsonChunksList != null) detailedJsonChunksList.Clear();
        if (parsedDetailedChunksList != null) parsedDetailedChunksList.Clear();
    }

    public void RequestDetailedImages(DetailsPanelController detailsPanel, int detailSlot, int detailPackNo)
    {
        if (detailsPanel == null) return;

        VRCUrl detailedImagesUrl = GetDetailedImagesUrl(detailSlot, detailPackNo);
        if (VRCUrl.IsNullOrEmpty(detailedImagesUrl))
        {
            Debug.LogError($"Detailed images url not found. slot={detailSlot}, packNo={detailPackNo}");
            return;
        }

        if (IsDetailedImagesUrlFetched(detailedImagesUrl))
        {
            detailsPanel.OnDetailedImagesLoaded();
            return;
        }

        requestedDetailsPanel = detailsPanel;
        requestedDetailedImagesUrl = detailedImagesUrl;

        ClearDetailedTemporaryState();

        VRCStringDownloader.LoadUrl(detailedImagesUrl, this.GetComponent<UdonBehaviour>());
    }

    private VRCUrl GetDetailedImagesUrl(int detailSlot, int detailPackNo)
    {
        if (detailSlot < 1 || detailSlot > 3) return VRCUrl.Empty;
        if (detailPackNo < 1 || detailPackNo > DETAILED_PACKS_PER_SLOT) return VRCUrl.Empty;
        if (activeDetailedImagesUrls == null) return VRCUrl.Empty;

        int index = (detailSlot - 1) * DETAILED_PACKS_PER_SLOT + (detailPackNo - 1);
        if (index < 0 || index >= activeDetailedImagesUrls.Length) return VRCUrl.Empty;

        return activeDetailedImagesUrls[index];
    }

    public DataList GetCurrentDetailedImagesList()
    {
        return detailedImagesList;
    }

    private void RegisterFetchedDetailedImagesUrl(VRCUrl url)
    {
        if (IsDetailedImagesUrlFetched(url)) return;

        fetchedDetailedImagesUrls.Add(new DataToken(url.Get()));
    }

    private bool IsDetailedImagesUrlFetched(VRCUrl url)
    {
        string targetUrl = url.Get();
        for (int i = 0; i < fetchedDetailedImagesUrls.Count; i++)
        {
            if (fetchedDetailedImagesUrls[i].String == targetUrl) {
                return true;
            }
        }

        return false;
    }

    public byte[] GetDetailedImageBytes(string imageId)
    {
        for (int i = 0; i < detailedImageIds.Length; i++)
        {
            if (detailedImageIds[i] == imageId) {
                return detailedImageBytes[i];
            }

        }
        return null;
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
            SetScrollbarVisibility(false);
            ResetReturnButtonImmediate();
            return 1.0f;
        } else {
            SetScrollbarVisibility(true);
        }

        string currentDate = System.DateTime.Now.ToString("yyyyMMdd");
        int currentHour = System.DateTime.Now.Hour;

        RectTransform dateItem = FindDateItem(currentDate);
        RectTransform timeItem = FindTimeItem(dateItem, currentHour);

        if (timeItem != null) {
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

            if (childRect != null && child.name == dateName && child.gameObject.activeInHierarchy)
            {
                return childRect;
            }
        }
        return null;
    }

    private RectTransform FindTimeItem(RectTransform dateItem, int time)
    {
        if (dateItem == null) return null;
        if (!dateItem.gameObject.activeInHierarchy) return null;

        Transform dateContainers = dateItem.Find("DateContainers");

        if (dateContainers == null) return null;

        for (int currentTime = time; currentTime < 24; currentTime++)
        {
            string timeName = "Time" + currentTime.ToString("00");

            for (int i = 0; i < dateContainers.childCount; i++)
            {
                Transform child = dateContainers.GetChild(i);
                RectTransform childRect = child.GetComponent<RectTransform>();

                if (childRect != null && child.name == timeName && child.gameObject.activeInHierarchy)
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

    public void OnClickReturnButton()
    {
        currentLerpTime = 0f;
        startPosition = scrollRect.verticalNormalizedPosition;
        initialVerticalPosition = GetInitialScrollPosition();
        returnButtonClicked = true;
        startFadeOut = true;
        mainScrollHandler.SetReturnButtonClicked(true);
    }

    public void OnPointerEnterMain()
    {
        mainScrollHandler.SetPointerHover(true);
    }
    public void OnPointerExitMain()
    {
        mainScrollHandler.SetPointerHover(false);
    }

    private void Update()
    {
        if (_ticksPaused) return;
        if (!isScrollbarVisible && !startFadeIn && !startFadeOut && !returnButtonClicked) return;

        if(isScrollbarVisible) {
            ReturnToInitialPosition(returnButtonClicked);

            mainScrollHandler.UpdateCustomScroll();
            bool isWheelScroll = mainScrollHandler.GetIsWheelScroll();
            bool isStickScroll = mainScrollHandler.GetIsStickScroll();

            if (scrollRect.verticalNormalizedPosition != lastScrollbarValue) {
                StickDate();
                float v = scrollRect.verticalNormalizedPosition;
                lastScrollbarValue = v;

                if(!isWheelScroll && !isStickScroll && !returnButtonClicked) {
                    SetScrollPositionImmediate(v);
                    mainScrollHandler.SyncToScrollPosition();
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

    private void StickDate()
    {
        if(currentStickyIndex >= 0 && currentStickyIndex < dateTimeContainersChildCount)
        {
            int updateDirection = StickDateByIndex(currentStickyIndex);

            if (updateDirection != 0)
            {
                int newIndex = currentStickyIndex + updateDirection;
                while (newIndex >= 0 && newIndex < dateTimeContainersChildCount)
                {
                    Transform child = dateTimeContainersRect.GetChild(newIndex);
                    if (!child.gameObject.activeSelf)
                    {
                        newIndex += updateDirection;
                        continue;
                    }

                    int result = StickDateByIndex(newIndex);

                    if ((updateDirection == 1 && result == -1) || (updateDirection == -1 && result == 1))
                    {
                        return;
                    }

                    if (result == 0)
                    {
                        currentStickyIndex = newIndex;
                        return;
                    }

                    newIndex += updateDirection;
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
                mainScrollHandler.SetReturnButtonClicked(false);
                mainScrollHandler.SyncToScrollPosition();
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

    public void ApplyFiltersById(int[] selectedCategoryIds, int[] selectedTagIds, int[] selectedProgramIds, int[] selectedTypeIds, int[] selectedDeviceIds)
    {
        if (eventItemScripts == null || eventItemScripts.Length == 0) return;

        bool isAllCat = (selectedCategoryIds == null || selectedCategoryIds.Length == 0);
        bool isAllTag = (selectedTagIds == null || selectedTagIds.Length == 0);
        bool isAllProg = (selectedProgramIds == null || selectedProgramIds.Length == 0);
        bool isAllType = (selectedTypeIds == null || selectedTypeIds.Length == 0);
        bool isAllDev = (selectedDeviceIds == null || selectedDeviceIds.Length == 0);

        int n = eventItemScripts.Length;

        if (isAllCat && isAllTag && isAllProg && isAllType && isAllDev) {
            for (int i = 0; i < n; i++)
            {
                if (eventItemScripts[i] != null) eventItemScripts[i].ApplyMatchState(true);
            }
        } else {
            for (int i = 0; i < n; i++)
            {
                int catId = (eventCategoryIds != null && i < eventCategoryIds.Length) ? eventCategoryIds[i] : -1;
                int[] myTags = (eventTagIds != null && i < eventTagIds.Length) ? eventTagIds[i] : null;
                int progId = (eventProgramIds != null && i < eventProgramIds.Length) ? eventProgramIds[i] : -1;
                int typeVal = (eventTypeIds != null && i < eventTypeIds.Length) ? eventTypeIds[i] : -1;
                int supMob = (eventSupportsMobile != null && i < eventSupportsMobile.Length) ? eventSupportsMobile[i] : -1;

                bool isCategoryMatch = isAllCat || (catId >= 0 && ContainsInt(selectedCategoryIds, catId));
                bool isTagMatch = isAllTag || ContainsAllInts(myTags, selectedTagIds);
                bool isProgMatch = isAllProg || (progId >= 0 && ContainsInt(selectedProgramIds, progId));
                bool isTypeMatch = isAllType || (typeVal >= 0 && ContainsInt(selectedTypeIds, typeVal));
                bool isDevMatch = isAllDev || (supMob >= 0 && ContainsInt(selectedDeviceIds, supMob));

                bool match = isCategoryMatch && isTagMatch && isProgMatch && isTypeMatch && isDevMatch;
                if (eventItemScripts[i] != null) {
                    eventItemScripts[i].ApplyMatchState(match);
                }
            }
        }

        StartReflowFilteredLayout();
    }

    private void StartReflowFilteredLayout()
    {
        _rfActiveTicket++;
        _rfRunTicket = _rfActiveTicket;

        wingController.SetFiltersBusy(true);
        ShowCurtain();

        _rf_di = 0;
        _rf_ti = 0;
        _rf_dateHeight = 0f;
        _rf_activeTimeCount = 0;
        _rf_hasAnyDateActive = false;

        SendCustomEventDelayedFrames(nameof(ReflowFilteredLayout_Step), 0);
    }

    public void ReflowFilteredLayout_Step()
    {
        if (_rfRunTicket != _rfActiveTicket) return;

        Transform dateRoot = dateTimeContainers.transform;
        int dateCount = dateRoot.childCount;

        _rfSw.Restart();

        while (_rf_di < dateCount)
        {
            Transform dateItem = dateRoot.GetChild(_rf_di);
            Transform dateContainers = dateItem.Find("DateContainers");
            RectTransform dateContainersRect = dateContainers.GetComponent<RectTransform>();
            RectTransform dateItemRect = dateItem.GetComponent<RectTransform>();

            int timeCount = dateContainers.childCount;

            while (_rf_ti < timeCount)
            {
                Transform timeItem = dateContainers.GetChild(_rf_ti);

                bool timeVisible;
                float timeItemHeight;
                ProcessOneTimeItem(timeItem, out timeVisible, out timeItemHeight);

                if (timeVisible) {
                    _rf_dateHeight += timeItemHeight;
                    _rf_activeTimeCount++;
                }

                _rf_ti++;

                dateContainersRect.sizeDelta = new Vector2(dateContainersRect.sizeDelta.x, _rf_dateHeight);

                if (_rfSw.Elapsed.TotalMilliseconds >= reflowBudgetMs) {
                    _rfSw.Stop();
                    SendCustomEventDelayedFrames(nameof(ReflowFilteredLayout_Step), 1);
                    return;
                }
            }

            if (_rf_activeTimeCount == 0) {
                dateItem.gameObject.SetActive(false);
            } else {
                if (!dateItem.gameObject.activeSelf) dateItem.gameObject.SetActive(true);

                RectTransform dateTextRect = dateItem.Find("DateTextBackground/BackgroundImage/DateText").GetComponent<RectTransform>();
                float dateTextHeight = dateTextRect.sizeDelta.y;

                float dateHeightTotal = _rf_dateHeight + dateTextHeight;
                dateItemRect.sizeDelta = new Vector2(dateItemRect.sizeDelta.x, dateHeightTotal);

                _rf_hasAnyDateActive = true;
            }

            _rf_di++;
            _rf_ti = 0;
            _rf_dateHeight = 0f;
            _rf_activeTimeCount = 0;
        }

        _rfSw.Stop();
        wingController.SetFiltersBusy(false);
        HideCurtainNextFrame();

        LayoutRebuilder.ForceRebuildLayoutImmediate(dateTimeContainersRect);

        if (!_rf_hasAnyDateActive) {
            backGroundImage.sprite = noEventsSprite;
            ApplyNoContentScrollState();

            eventItemCount = 0;
            eventItemRects = null;
            eventItemObjects = null;
            return;
        } else {
            backGroundImage.sprite = backGroundSprite;
            returnButton.SetActive(true);
        }

        SendCustomEventDelayedFrames(nameof(ResetScrollAndVirtualizeAfterLayout), 2);
    }

    private void ProcessOneTimeItem(Transform timeItem, out bool timeVisible, out float timeItemHeightOut)
    {
        RectTransform timeItemRect = timeItem.GetComponent<RectTransform>();
        Transform timeContainers = timeItem.Find("TimeContainers");
        RectTransform timeContainersRect = timeContainers.GetComponent<RectTransform>();
        GridLayoutGroup grid = timeContainers.GetComponent<GridLayoutGroup>();

        int childCount = timeContainers.childCount;
        for (int c = 0; c < childCount; c++)
        {
            var child = timeContainers.GetChild(c);
            EventItemScript eventItemScript = child.GetComponent<EventItemScript>();
            if (eventItemScript != null && eventItemScript.IsPlaceholder) child.gameObject.SetActive(false);
        }

        Transform[] matchChildren = new Transform[childCount];
        int[] matchKeys = new int[childCount];
        int matchCount = 0;
        int maxKey = -1;

        for (int c = 0; c < childCount; c++)
        {
            var child = timeContainers.GetChild(c);
            var eventItemScript = child.GetComponent<EventItemScript>();
            if (eventItemScript == null || eventItemScript.IsPlaceholder) continue;

            bool show = eventItemScript.IsRealAndMatch();
            child.gameObject.SetActive(show);

            if (show) {
                int key = eventItemScript.GetStableIndex();
                matchChildren[matchCount] = child;
                matchKeys[matchCount] = key;
                if (key > maxKey) maxKey = key;
                matchCount++;
            }
        }

        if (matchCount == 0) {
            timeContainersRect.sizeDelta = new Vector2(timeContainersRect.sizeDelta.x, 0f);
            timeItem.gameObject.SetActive(false);
            timeItemRect.sizeDelta = new Vector2(timeItemRect.sizeDelta.x, 0f);

            timeVisible = false;
            timeItemHeightOut = 0f;
            return;
        }

        if (!timeItem.gameObject.activeSelf) timeItem.gameObject.SetActive(true);

        int nextIndex = 0;
        for (int key = 0; key <= maxKey; key++)
        {
            for (int i = 0; i < matchCount; i++)
            {
                if (matchKeys[i] == key) {
                    matchChildren[i].SetSiblingIndex(nextIndex++);
                }
            }
        }

        int realChildCount = matchCount;
        float containerWidth = timeContainersRect.rect.width - gridLayoutPaddingLeft - gridLayoutPaddingRight;
        int itemsPerRow = Mathf.Max(1, Mathf.FloorToInt((containerWidth + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)));

        int lastRowCount = realChildCount % itemsPerRow;
        int blanks = (lastRowCount == 0) ? 0 : (itemsPerRow - lastRowCount);
        FillWithPlaceholders(timeContainers, realChildCount, blanks);

        int totalCells = realChildCount + blanks;
        int rows = (totalCells == 0) ? 0 : Mathf.CeilToInt((float)totalCells / itemsPerRow);

        float timeItemHeight = 0f;
        if (rows > 0) {
            timeItemHeight = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y + gridLayoutPaddingTop + gridLayoutPaddingBottom;

            bool allTextsOneLine = true;
            int startIdxInLastRow = Mathf.Max(0, totalCells - itemsPerRow);
            int seenReal = 0;

            for (int c = 0; c < timeContainers.childCount; c++)
            {
                var child = timeContainers.GetChild(c);
                var eventItemScript = child.GetComponent<EventItemScript>();
                if (eventItemScript == null || eventItemScript.IsPlaceholder || !child.gameObject.activeSelf) continue;

                if (seenReal >= startIdxInLastRow)
                {
                    if (eventItemScript.GetTextLineCount() == 2) { allTextsOneLine = false; break; }
                }
                seenReal++;
            }
            if (allTextsOneLine) timeItemHeight -= 24f;
        }

        timeContainersRect.sizeDelta = new Vector2(timeContainersRect.sizeDelta.x, timeItemHeight);

        RectTransform timeTextRect = timeItem.Find("TimeText").GetComponent<RectTransform>();
        float timeTextHeight = timeTextRect.sizeDelta.y;
        timeItemHeight += timeTextHeight;

        timeItemRect.sizeDelta = new Vector2(timeItemRect.sizeDelta.x, timeItemHeight);

        timeVisible = true;
        timeItemHeightOut = timeItemHeight;
    }

    private void ShowCurtain()
    {
        reflowCurtain.SetActive(true);
    }

    private void HideCurtainNextFrame()
    {
        SendCustomEventDelayedFrames(nameof(_HideCurtainNow), 2);
    }

    public void _HideCurtainNow()
    {
        reflowCurtain.SetActive(false);
    }

    private bool ContainsInt(int[] arr, int value)
    {
        if (arr == null) return false;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == value) return true;
        return false;
    }

    private bool ContainsAllInts(int[] source, int[] required)
    {
        if (required == null || required.Length == 0) return true;
        if (source == null || source.Length == 0) return false;

        for (int i = 0; i < required.Length; i++)
        {
            bool found = false;

            for (int j = 0; j < source.Length; j++)
            {
                if (source[j] == required[i]) {
                    found = true;
                    break;
                }
            }

            if (!found) return false;
        }

        return true;
    }

    private void ApplyNoContentScrollState()
    {
        SetScrollbarVisibility(false);
        initialVerticalPosition = -1f;
        ResetReturnButtonImmediate();

        SetScrollPositionImmediate(1.0f);
        mainScrollHandler.SyncToScrollPosition();
    }

    private void SetScrollbarVisibility(bool visible)
    {
        isScrollbarVisible = visible;
        mainScrollHandler.SetScrollbarVisible(visible);
    }

    private void SetScrollPositionImmediate(float v)
    {
        scrollRect.verticalNormalizedPosition = v;
        targetScrollPosition = v;
        lastScrollbarValue = v;
    }

    public void ResetScrollAndVirtualizeAfterLayout()
    {
        float v = GetInitialScrollPosition();
        initialVerticalPosition = v;
        SetInitialPosition(v);
        StickDate();
        ResetReturnButtonImmediate();

        mainScrollHandler.SyncToScrollPosition();

        CacheOnlyActiveRealMatchesForVirtualization();
        VirtualizeItems();
    }

    private void ResetReturnButtonImmediate()
    {
        startFadeIn  = false;
        startFadeOut = false;
        fadeAlpha = fadeMinAlpha;
        returnButtonImage.color = new Color(1f, 1f, 1f, fadeAlpha);
        returnButton.SetActive(false);
        returnButtonClicked = false;
    }

    private void CacheOnlyActiveRealMatchesForVirtualization()
    {
        var scriptsAll = dateTimeContainers.GetComponentsInChildren<EventItemScript>(true);

        int cap = scriptsAll.Length;
        GameObject[] objs = new GameObject[cap];
        RectTransform[] rts = new RectTransform[cap];
        int k = 0;

        for (int i = 0; i < scriptsAll.Length; i++)
        {
            var eventItemScript = scriptsAll[i];
            if (eventItemScript == null) continue;

            bool include = eventItemScript.IsRealAndMatch() || (eventItemScript.IsPlaceholder && eventItemScript.gameObject.activeSelf);
            if (!include) continue;

            objs[k] = eventItemScript.gameObject;
            rts[k] = eventItemScript.GetComponent<RectTransform>();
            k++;
        }

        eventItemCount = k;
        eventItemObjects = new GameObject[k];
        eventItemRects = new RectTransform[k];

        for (int i = 0; i < k; i++)
        {
            eventItemObjects[i] = objs[i];
            eventItemRects[i] = rts[i];
        }
    }

    public bool TryRegisterRuntimeTexture(Texture2D tex)
    {
        if (runtimeTextureCount >= MAX_RUNTIME_TEXTURES)
        {
            Destroy((UnityEngine.Object)tex);
            return false;
        }
        runtimeTextures[runtimeTextureCount++] = tex;
        return true;
    }

    public void PauseAllTicksForProximity()
    {
        _ticksPaused = true;
    }
    public void ResumeAllTicksForProximity()
    {
        _ticksPaused = false;
    }

    public void ResetTimetable()
    {
        if (!isLoaded) return;
        isLoaded = false;

        var panels = canvas.GetComponentsInChildren<DetailsPanelController>(true);
        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].CloseDetails();
        }

        for (int i = 0; i < runtimeTextureCount; i++)
        {
            var tex = runtimeTextures[i];
            if (tex != null) Destroy((UnityEngine.Object)tex);
            runtimeTextures[i] = null;
        }

        runtimeTextures = null;
        runtimeTextureCount = 0;
        thumbnailTextureCount = 0;

        int childCnt = dateTimeContainers.transform.childCount;
        for (int i = childCnt - 1; i >= 0; i--)
        {
            Destroy(dateTimeContainers.transform.GetChild(i).gameObject);
        }

        wingController.ClearOnReset();

        eventItemCount = 0;
        eventItemRects = null;
        eventItemObjects = null;

        dateTimeContainersRect = null;
        dateTimeContainersChildCount = 0;
        currentStickyIndex = -1;
        initialVerticalPosition = -1;
        totalTimeItems = 0;
        processedTimeItems = 0;

        eventList = new DataList();
        detailedImagesList = new DataList();

        thumbnailBytes = null;
        wnpkBytes = null;
        wnpkThumbOffsets = null;
        wnpkThumbLengths = null;
        detailedImageBytes = null;
        detailedImageIds = null;
        detailedWnpkBytes = null;
        detailedWnpkOffsets = null;
        detailedWnpkLengths = null;

        jsonParseIndex = 0;
        eventDisplayIndex = 0;

        splitStartIndex = 0;
        jsonChunkIndex = 0;
        parsedChunksList = new DataList();
        jsonChunksList = new DataList();

        detailedArrayContent = null;
        detailedSplitStartIndex = 0;
        detailedJsonChunkIndex = 0;
        detailedImageParseIndex = 0;
        cachedDetailedJsonResult = default;

        cachedJsonResult = default;
        parsedDetailedChunksList = new DataList();
        detailedJsonChunksList = new DataList();

        requestedDetailedImagesUrl = VRCUrl.Empty;
        requestedDetailsPanel = null;
        fetchedDetailedImagesUrls = new DataList();

        backGroundImage.sprite = backGroundSprite;
        ApplyOutsideView();

        SetScrollPositionImmediate(1.0f);
        _ticksPaused = false;

        scrollViewCanvasGroup.alpha = 0f;
        scrollViewCanvasGroup.interactable = false;
        scrollViewCanvasGroup.blocksRaycasts = false;

        ResetReturnButtonImmediate();

        loadingProgressBar.value = 0f;
        loadingProgressText.text = "0%";

        wingPanel.SetActive(false);
        wingController.SetClosedImmediate();
        _schemaReady = false;
        _eventLayoutReady = false;
        _wingShown = false;

        eventItemScripts = null;
        eventCategoryIds = null;
        eventTagIds = null;
        eventProgramIds = null;
        eventTypeIds = null;
        eventSupportsMobile = null;

        schemaCategoriesList = new DataList();
        schemaTagsList = new DataList();
        schemaTagGroupsList = new DataList();
        schemaProgramsList = new DataList();
    }
}
