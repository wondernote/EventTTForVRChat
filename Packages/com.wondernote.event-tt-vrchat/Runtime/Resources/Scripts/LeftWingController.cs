
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using WonderNote.EventTimeTable;
using TMPro;
using VRC.SDK3.Data;
using System;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LeftWingController : UdonSharpBehaviour
{
    [Header("Wing UI")]
    [SerializeField] private RectTransform wingPanel;
    [SerializeField] private RectTransform panelBody;
    [SerializeField] private RectTransform tabButtonRect;
    [SerializeField] private RectTransform tabHoverAreaRect;
    [SerializeField] private Button tabUIButton;
    [SerializeField] private CanvasGroup panelCG;
    [SerializeField] private RectTransform arrowIcon;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private FlowWrapLayout catFlowWrapLayout;
    [SerializeField] private RectTransform sectionCategoryRect;
    [SerializeField] private RectTransform sectionTagRect;
    [SerializeField] private CanvasGroup mainPanelCanvasGroup;
    [SerializeField] private GameObject outsideCloseArea;

    private Vector2 _iconPosClosed, _iconPosOpen;
    private Vector2 _iconSizeBase;
    private bool _needsScrollbarFinalize = false;

    private float motionTime = 0.12f;
    private float panelWidth;
    private float tabWidth;
    private float _closedX;
    private float _targetX;
    private bool  _isOpen;

    private float tabHoverExtra = 54f;
    private float tabMotionTime = 0.10f;
    private float _baseTabWidth;
    private bool  _hover;

    private Color32 BTN_CLOSED_NORMAL = new Color32(255, 255, 255, 95);
    private Color32 BTN_CLOSED_HOVER = new Color32(255, 255, 255, 160);
    private Color32 BTN_CLOSED_DOWN = new Color32(255, 255, 255, 160);
    private Color32 BTN_OPEN_NORMAL = new Color32(255, 255, 255, 125);
    private Color32 BTN_OPEN_HOVER = new Color32(255, 255, 255, 190);
    private Color32 BTN_OPEN_DOWN = new Color32(255, 255, 255, 190);

    [Header("Filter Chips")]
    [SerializeField] private GameObject chipItemPrefab;
    [SerializeField] private GameObject tagChipItemPrefab;
    [SerializeField] private GameObject tagGroupItemPrefab;
    [SerializeField] private GameObject tagChildSectionPrefab;
    [SerializeField] private Transform catResetChipParent;
    [SerializeField] private Transform catFilterChipsParent;
    [SerializeField] private Transform tagResetChipParent;
    [SerializeField] private Transform tagFilterChipsParent;
    [SerializeField] private EventTimetable timetable;

    private DataList selectedCatIds = new DataList();
    private DataList selectedTagIds = new DataList();
    private DataList selectedProgIds = new DataList();
    private DataList selectedTypeIds = new DataList();
    private DataList selectedDeviceIds = new DataList();

    private const int ID_RESET = -1;
    private const int KIND_CATEGORY = 0;
    private const int KIND_TAG = 1;
    private const int KIND_PROGRAM = 2;
    private const int KIND_TYPE = 3;
    private const int KIND_DEVICE = 4;
    private const int TYPE_ONEOFF = 0;
    private const int TYPE_RECURRING = 1;
    private const int DEVICE_QUEST_SUPPORT = 1;

    private ChipListener catResetChipListener;
    private ChipListener tagResetChipListener;
    private ChipListener[] catChipListeners;
    private ChipListener[] tagChipListeners;
    private FlowWrapLayout[] tagRowFlowWrapLayouts;
    private TagGroupItemRefs[] tagGroupItemRefs;
    private TagGroupAccordion[] tagGroupAccordions;
    private DataList[] tagGroupTagIdsList;

    private Color32 COLOR_DEFAULT = new Color32(111, 111, 111, 255);

    [Header("Type Chips")]
    [SerializeField] private ChipListener typeResetChip;
    [SerializeField] private ChipListener typeSingleChip;
    [SerializeField] private ChipListener typeRecurringChip;

    [Header("Device Chips")]
    [SerializeField] private ChipListener deviceResetChip;
    [SerializeField] private ChipListener deviceQuestChip;

    [Header("Program Banners")]
    [SerializeField] private GameObject sectionPrograms;
    [SerializeField] private Transform progBannersParent;
    [SerializeField] private GameObject bannerItemPrefab;
    private ProgramBannerListener[] progBannerListeners;

    [Header("Scrollbar Settings")]
    [SerializeField] private ScrollInputHandler wingScrollHandler;
    [SerializeField] private ScrollRect wingScrollRect;
    [SerializeField] private GameObject wingContent;

    private bool _filtersBusy = false;
    private bool _tagGroupBuildReady = false;

    private const int TAG_GROUP_BUILD_FRAME_LIMIT_MS = 5;
    private const int TAG_GROUP_BUILD_START_DELAY_FRAMES = 1;
    private System.Diagnostics.Stopwatch _tagGroupBuildSw = new System.Diagnostics.Stopwatch();

    private bool _tagGroupBuildInProgress = false;

    private DataList _buildTags;
    private DataList _buildTagGroups;
    private DataList _buildCurrentChildren;
    private DataList _buildCurrentTagIds;
    private DataList _buildCurrentGroupTagIds;

    private int _buildParentIndex = 0;
    private int _buildChildIndex = 0;
    private int _buildTagIndex = 0;
    private int _buildRowIndex = 0;
    private int _buildChipIndex = 0;
    private int _buildRowStartIndex = 0;

    private bool _buildParentPrepared = false;
    private bool _buildChildPrepared = false;

    private TagGroupItemRefs _buildParentRefs;
    private Transform _buildChildrenRoot;
    private Transform _buildRowTagsRoot;
    private FlowWrapLayout _buildRowFlowWrap;

    private int _wakeTagChildrenIndex = 0;

    void Start()
    {
        tabWidth = tabButtonRect.sizeDelta.x;
        float bodyWidth = panelBody.sizeDelta.x;
        panelWidth = bodyWidth + tabWidth;
        _closedX = -bodyWidth;
        _baseTabWidth = tabButtonRect.sizeDelta.x;

        tabHoverAreaRect.anchoredPosition = new Vector2(tabButtonRect.anchoredPosition.x, tabHoverAreaRect.anchoredPosition.y);
        tabHoverAreaRect.sizeDelta = new Vector2(_baseTabWidth + tabHoverExtra, tabHoverAreaRect.sizeDelta.y);

        _iconPosClosed = arrowIcon.anchoredPosition;
        _iconPosOpen = new Vector2(0f, _iconPosClosed.y);
        _iconSizeBase = arrowIcon.sizeDelta;

        SetClosedImmediate();
    }

    void Update()
    {
        if (!wingPanel.gameObject.activeInHierarchy) return;

        float curX = wingPanel.anchoredPosition.x;
        if (!Mathf.Approximately(curX, _targetX))
        {
            float speed = (panelWidth - tabWidth) / Mathf.Max(0.001f, motionTime);

            float nextX = Mathf.MoveTowards(curX, _targetX, speed * Time.deltaTime);
            wingPanel.anchoredPosition = new Vector2(nextX, wingPanel.anchoredPosition.y);

            if (Mathf.Approximately(nextX, _targetX))
            {
                if (!_isOpen)
                {
                    panelCG.interactable = false;
                    panelCG.blocksRaycasts = false;

                    if (panelBody.gameObject.activeSelf) SendCustomEventDelayedFrames(nameof(DisableWingBodyAfterClose), 1);
                }
                else
                {
                    SendCustomEventDelayedFrames(nameof(WakeTagChildrenAfterOpen), 1);
                }
            }
        }

        float curW = tabButtonRect.sizeDelta.x;
        float targetW = _hover ? (_baseTabWidth + tabHoverExtra) : _baseTabWidth;
        if (!Mathf.Approximately(curW, targetW))
        {
            float dur = tabMotionTime;
            float step = (Mathf.Abs(targetW - curW) / Mathf.Max(0.001f, dur)) * Time.deltaTime;
            float nextW = Mathf.MoveTowards(curW, targetW, step);

            if (!Mathf.Approximately(nextW, curW))
            {
                tabButtonRect.sizeDelta = new Vector2(nextW, tabButtonRect.sizeDelta.y);
            }
        }

        float curIconW = arrowIcon.sizeDelta.x;
        float targetIconW = _hover ? (_iconSizeBase.x + 15f) : _iconSizeBase.x;

        if (!Mathf.Approximately(curIconW, targetIconW))
        {
            float dur = tabMotionTime;
            float step = (Mathf.Abs(targetIconW - curIconW) / Mathf.Max(0.001f, dur)) * Time.deltaTime;
            float nextIconW = Mathf.MoveTowards(curIconW, targetIconW, step);

            arrowIcon.sizeDelta = new Vector2(nextIconW, nextIconW);
        }

        wingScrollHandler.UpdateCustomScroll();
    }

    public void DisableWingBodyAfterClose()
    {
        if (_isOpen) return;

        SleepClosedTagChildrenForNextOpen();
        SetWingBodyEnabled(false);
    }

    public void WakeTagChildrenAfterOpen()
    {
        _wakeTagChildrenIndex = 0;
        WakeNextTagChildrenAfterOpen();
    }

    public void WakeNextTagChildrenAfterOpen()
    {
        if (!_isOpen) return;
        if (tagGroupAccordions == null) return;

        while (_wakeTagChildrenIndex < tagGroupAccordions.Length)
        {
            TagGroupAccordion accordion = tagGroupAccordions[_wakeTagChildrenIndex];
            _wakeTagChildrenIndex++;

            if (accordion != null) {
                accordion.WakeChildrenForPanelOpen();
                SendCustomEventDelayedFrames(nameof(WakeNextTagChildrenAfterOpen), 1);
                return;
            }
        }
    }

    private void SleepClosedTagChildrenForNextOpen()
    {
        if (tagGroupAccordions == null) return;

        for (int i = 0; i < tagGroupAccordions.Length; i++)
        {
            if (tagGroupAccordions[i] != null) {
                tagGroupAccordions[i].SleepChildrenIfClosedForPanelClose();
            }
        }
    }

    public void ToggleWing()
    {
        if (!_tagGroupBuildReady) return;

        audioManager.PlayWingSound();
        _hover = false;
        if (_isOpen) CloseWing();
        else OpenWing();
    }

    private void OpenWing()
    {
        _isOpen = true;

        _targetX = wingPanel.anchoredPosition.x;

        ApplyOpenVisuals();

    #if UNITY_ANDROID
    SendCustomEventDelayedFrames(nameof(BeginOpenWingMotion), 2);
    #else
    SendCustomEventDelayedFrames(nameof(BeginOpenWingMotion), 1);
    #endif
    }

    public void BeginOpenWingMotion()
    {
        if (!_isOpen) return;

        _targetX = 0f;

        if (_needsScrollbarFinalize) {
            ScheduleScrollbarFinalize();
        }
    }

    public void SetNeedsScrollbarFinalize()
    {
        _needsScrollbarFinalize = true;

        if (_isOpen) {
            ScheduleScrollbarFinalize();
        }
    }

    private void ScheduleScrollbarFinalize()
    {
        _needsScrollbarFinalize = false;
        SendCustomEventDelayedFrames(nameof(FinalizeScrollbarVisibility), 2);
    }

    private void CloseWing()
    {
        _isOpen = false;
        _targetX = _closedX;

        ApplyClosedVisuals();
    }

    public void SetClosedImmediate()
    {
        _isOpen = false;
        _targetX = _closedX;

        SnapPanelX(_closedX);
        panelCG.alpha = 1f;
        ApplyClosedVisuals();
        SetWingBodyEnabled(false);
    }

    public void SetModalBackdrop(bool allow)
    {
        if (allow && _isOpen) {
            outsideCloseArea.SetActive(true);
        } else {
            outsideCloseArea.SetActive(false);
        }
    }

    private void ApplyOpenVisuals()
    {
        SetWingBodyEnabled(true);

        panelCG.interactable = true;
        panelCG.blocksRaycasts = true;

        arrowIcon.localRotation = Quaternion.Euler(0, 0, 180);
        arrowIcon.anchoredPosition = _iconPosOpen;
        UpdateTintColors(tabUIButton, BTN_OPEN_NORMAL, BTN_OPEN_HOVER, BTN_OPEN_DOWN);

        mainPanelCanvasGroup.interactable = false;
        mainPanelCanvasGroup.blocksRaycasts = false;
        
        SetModalBackdrop(true);
    }

    private void ApplyClosedVisuals()
    {
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;

        _hover = false;
        arrowIcon.localRotation = Quaternion.identity;
        arrowIcon.anchoredPosition = _iconPosClosed;
        UpdateTintColors(tabUIButton, BTN_CLOSED_NORMAL, BTN_CLOSED_HOVER, BTN_CLOSED_DOWN);

        tabButtonRect.sizeDelta = new Vector2(_baseTabWidth, tabButtonRect.sizeDelta.y);

        mainPanelCanvasGroup.interactable = true;
        mainPanelCanvasGroup.blocksRaycasts = true;
        
        SetModalBackdrop(false);
    }

    private void UpdateTintColors(Button btn, Color32 normal, Color32 highlighted, Color32 pressed)
    {
        var cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = highlighted;
        cb.pressedColor = pressed;
        btn.colors = cb;
    }

    private void SnapPanelX(float x)
    {
        wingPanel.anchoredPosition = new Vector2(x, 0f);
    }

    public void HoverIn()
    {
        _hover = true;
    }

    public void HoverOut()
    {
        _hover = false;
    }

    public void PlayClickSound()
    {
        audioManager.PlayClickSound();
    }

    public void PlayHoverSound()
    {
        audioManager.PlayHoverSound();
    }

    public void OnPointerEnterWing()
    {
        wingScrollHandler.SetPointerHover(true);
    }
    public void OnPointerExitWing()
    {
        wingScrollHandler.SetPointerHover(false);
    }

    public void SetupChipsFromSchema(DataList categories, DataList tags, DataList tagGroups, DataList programs)
    {
        BuildResetChip(catResetChipParent, KIND_CATEGORY);
        BuildFilterChips(categories, catFilterChipsParent, KIND_CATEGORY, catFlowWrapLayout);
        SetupFixedTypeChips();
        SetupFixedDeviceChips();
        BuildResetChip(tagResetChipParent, KIND_TAG);
        BuildKeywordTagGroups(tags, tagGroups);
        BuildProgramBanners(programs);
    }

    public void FinalizeScrollbarVisibility()
    {
        RectTransform wingContentRect = wingContent.GetComponent<RectTransform>();
        float wingContentHeight = wingContentRect.rect.height;
        float wingViewportHeight = wingScrollRect.GetComponent<RectTransform>().rect.height;
        bool visible = wingContentHeight > wingViewportHeight;
        wingScrollHandler.SetScrollbarVisible(visible);

        if (visible) {
            wingScrollHandler.SyncToScrollPosition();
        }
    }

    private void BuildResetChip(Transform parent, int kind)
    {
        GameObject chipItem = Instantiate(chipItemPrefab, parent);
        chipItem.name = "Chip_Reset";

        TextMeshProUGUI labelText = chipItem.GetComponentInChildren<TextMeshProUGUI>();
        labelText.text = "すべて";

        ChipListener chipListener = chipItem.GetComponent<ChipListener>();
        chipListener.SetInitInfo(this, ID_RESET, kind, null, COLOR_DEFAULT);

        if (kind == KIND_CATEGORY) {
            catResetChipListener = chipListener;
        } else if (kind == KIND_TAG) {
            tagResetChipListener = chipListener;
        }
    }

    private void BuildFilterChips(DataList list, Transform parent, int kind, FlowWrapLayout flowForThisRow)
    {
        if (list == null || list.Count == 0) return;

        if (kind == KIND_CATEGORY) {
            catChipListeners = new ChipListener[list.Count];
        } else if (kind == KIND_TAG) {
            tagChipListeners = new ChipListener[list.Count];
        }

        int chipIndex = 0;

        for (int i = 0; i < list.Count; i++)
        {
            DataDictionary dict = list[i].DataDictionary;

            int id = (int)dict["id"].Double;
            string label = dict["label"].String;
            string slug = dict["slug"].String;

            Color chipColor = COLOR_DEFAULT;
            if (dict.ContainsKey("color")) {
                string color = dict["color"].String;
                chipColor = timetable.ParseHexColor(color);
            }

            GameObject chipItem = Instantiate(chipItemPrefab, parent);
            chipItem.name = "Chip_" + slug;

            TextMeshProUGUI labelText = chipItem.GetComponentInChildren<TextMeshProUGUI>();
            labelText.text = label;

            ChipListener chipListener = chipItem.GetComponent<ChipListener>();

            chipListener.SetInitInfo(this, id, kind, flowForThisRow, chipColor);

            if (kind == KIND_CATEGORY) {
                catChipListeners[chipIndex++] = chipListener;
            } else if (kind == KIND_TAG) {
                tagChipListeners[chipIndex++] = chipListener;
            }
        }
    }

    private void BuildKeywordTagGroups(DataList tags, DataList tagGroups)
    {
        int totalRowCount = 0;
        int totalTagChipCount = 0;

        for (int i = 0; i < tagGroups.Count; i++)
        {
            DataDictionary parentDict = tagGroups[i].DataDictionary;
            DataList children = parentDict["children"].DataList;
            totalRowCount += children.Count;

            for (int j = 0; j < children.Count; j++)
            {
                DataDictionary childDict = children[j].DataDictionary;
                totalTagChipCount += childDict["tag_ids"].DataList.Count;
            }
        }

        tagRowFlowWrapLayouts = new FlowWrapLayout[totalRowCount];
        tagChipListeners = new ChipListener[totalTagChipCount];
        tagGroupItemRefs = new TagGroupItemRefs[tagGroups.Count];
        tagGroupAccordions = new TagGroupAccordion[tagGroups.Count];
        tagGroupTagIdsList = new DataList[tagGroups.Count];

        ClearKeywordTagBuildState();

        _buildTags = tags;
        _buildTagGroups = tagGroups;
    }

    public void BeginTagGroupBuild()
    {
        if (_tagGroupBuildInProgress) return;
        if (_buildTags == null || _buildTagGroups == null) return;

        _tagGroupBuildInProgress = true;

        SendCustomEventDelayedFrames(nameof(BuildKeywordTagGroupsAsync), TAG_GROUP_BUILD_START_DELAY_FRAMES);
    }
    
    public void BuildKeywordTagGroupsAsync()
    {
        if (!_tagGroupBuildInProgress) return;
        if (_buildTags == null || _buildTagGroups == null)
        {
            ClearKeywordTagBuildState();
            return;
        }

        _tagGroupBuildSw.Restart();

        while (_buildParentIndex < _buildTagGroups.Count)
        {
            if (!_buildParentPrepared)
            {
                _buildRowStartIndex = _buildRowIndex;
                _buildCurrentGroupTagIds = new DataList();

                DataDictionary parentDict = _buildTagGroups[_buildParentIndex].DataDictionary;
                string parentLabel = parentDict["label"].String;
                string parentSlug = parentDict["slug"].String;

                GameObject parentItem = Instantiate(tagGroupItemPrefab, tagFilterChipsParent);
                parentItem.name = "TagGroup_" + parentSlug;

                _buildParentRefs = parentItem.GetComponent<TagGroupItemRefs>();

                tagGroupAccordions[_buildParentIndex] = _buildParentRefs.accordion;
                _buildParentRefs.accordion.SetWingController(this);
                _buildParentRefs.accordion.SetIsFirstGroup(_buildParentIndex == 0);

                _buildParentRefs.headerLabel.text = parentLabel;
                _buildParentRefs.headBadge.SetActive(false);
                _buildChildrenRoot = _buildParentRefs.childrenRoot.transform;

                _buildCurrentChildren = parentDict["children"].DataList;
                _buildChildIndex = 0;
                _buildTagIndex = 0;
                _buildParentPrepared = true;
                _buildChildPrepared = false;

                if (_tagGroupBuildSw.ElapsedMilliseconds > TAG_GROUP_BUILD_FRAME_LIMIT_MS)
                {
                    SendCustomEventDelayedFrames(nameof(BuildKeywordTagGroupsAsync), 1);
                    return;
                }
            }

            while (_buildChildIndex < _buildCurrentChildren.Count)
            {
                if (!_buildChildPrepared)
                {
                    DataDictionary childDict = _buildCurrentChildren[_buildChildIndex].DataDictionary;
                    string childLabel = childDict["label"].String;
                    string childSlug = childDict["slug"].String;

                    GameObject childSection = Instantiate(tagChildSectionPrefab, _buildChildrenRoot);
                    childSection.name = "TagChild_" + childSlug;

                    TagChildSectionRefs childRefs = childSection.GetComponent<TagChildSectionRefs>();

                    childRefs.childLabel.text = childLabel;
                    _buildRowTagsRoot = childRefs.rowTagsRoot;
                    _buildRowFlowWrap = childRefs.rowFlowWrap;

                    if (_buildRowIndex < tagRowFlowWrapLayouts.Length) {
                        tagRowFlowWrapLayouts[_buildRowIndex] = _buildRowFlowWrap;
                        _buildRowIndex++;
                    }

                    _buildCurrentTagIds = childDict["tag_ids"].DataList;
                    _buildTagIndex = 0;
                    _buildChildPrepared = true;

                    if (_tagGroupBuildSw.ElapsedMilliseconds > TAG_GROUP_BUILD_FRAME_LIMIT_MS)
                    {
                        SendCustomEventDelayedFrames(nameof(BuildKeywordTagGroupsAsync), 1);
                        return;
                    }
                }

                while (_buildTagIndex < _buildCurrentTagIds.Count)
                {
                    int tagId = (int)_buildCurrentTagIds[_buildTagIndex].Double;
                    _buildTagIndex++;

                    int tagIndex = FindTagIndexById(_buildTags, tagId);
                    if (tagIndex < 0) continue;

                    DataDictionary tagDict = _buildTags[tagIndex].DataDictionary;

                    int id = (int)tagDict["id"].Double;
                    string label = tagDict["label"].String;
                    string slug = tagDict["slug"].String;

                    _buildCurrentGroupTagIds.Add(new DataToken(id));

                    GameObject chipItem = Instantiate(tagChipItemPrefab, _buildRowTagsRoot);
                    chipItem.name = "Chip_" + slug;

                    TextMeshProUGUI labelText = chipItem.GetComponentInChildren<TextMeshProUGUI>();
                    labelText.text = label;

                    ChipListener chipListener = chipItem.GetComponent<ChipListener>();
                    chipListener.SetInitInfo(this, id, KIND_TAG, _buildRowFlowWrap, COLOR_DEFAULT);

                    if (_buildChipIndex < tagChipListeners.Length) {
                        tagChipListeners[_buildChipIndex] = chipListener;
                        _buildChipIndex++;
                    }

                    if (_tagGroupBuildSw.ElapsedMilliseconds > TAG_GROUP_BUILD_FRAME_LIMIT_MS)
                    {
                        SendCustomEventDelayedFrames(nameof(BuildKeywordTagGroupsAsync), 1);
                        return;
                    }
                }

                _buildChildIndex++;
                _buildChildPrepared = false;

                if (_tagGroupBuildSw.ElapsedMilliseconds > TAG_GROUP_BUILD_FRAME_LIMIT_MS)
                {
                    SendCustomEventDelayedFrames(nameof(BuildKeywordTagGroupsAsync), 1);
                    return;
                }
            }

            tagGroupItemRefs[_buildParentIndex] = _buildParentRefs;
            tagGroupTagIdsList[_buildParentIndex] = _buildCurrentGroupTagIds;

            _buildParentRefs.headBadge.SetActive(false);
            _buildParentRefs.badgeCountText.text = "0";

            int rowCountForThisGroup = _buildRowIndex - _buildRowStartIndex;

            FlowWrapLayout[] groupRows = new FlowWrapLayout[rowCountForThisGroup];
            for (int k = 0; k < rowCountForThisGroup; k++)
            {
                groupRows[k] = tagRowFlowWrapLayouts[_buildRowStartIndex + k];
            }

            _buildParentRefs.accordion.SetRowFlowWrapLayouts(groupRows);
            _buildParentRefs.accordion.SetOpen(false);

            _buildParentIndex++;
            _buildParentPrepared = false;
            _buildChildPrepared = false;

            if (_tagGroupBuildSw.ElapsedMilliseconds > TAG_GROUP_BUILD_FRAME_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(BuildKeywordTagGroupsAsync), 1);
                return;
            }
        }

        SleepClosedTagChildrenForNextOpen();
        _tagGroupBuildReady = true;
        ClearKeywordTagBuildState();
    }

    private void ClearKeywordTagBuildState()
    {
        _tagGroupBuildInProgress = false;

        _buildTags = null;
        _buildTagGroups = null;
        _buildCurrentChildren = null;
        _buildCurrentTagIds = null;
        _buildCurrentGroupTagIds = null;

        _buildParentIndex = 0;
        _buildChildIndex = 0;
        _buildTagIndex = 0;
        _buildRowIndex = 0;
        _buildChipIndex = 0;
        _buildRowStartIndex = 0;

        _buildParentPrepared = false;
        _buildChildPrepared = false;

        _buildParentRefs = null;
        _buildChildrenRoot = null;
        _buildRowTagsRoot = null;
        _buildRowFlowWrap = null;

        _tagGroupBuildSw.Reset();
    }

    private int FindTagIndexById(DataList tags, int tagId)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            DataDictionary dict = tags[i].DataDictionary;
            if ((int)dict["id"].Double == tagId) {
                return i;
            }
        }

        return -1;
    }

    private void RefreshTagGroupBadges()
    {
        if (tagGroupItemRefs == null || tagGroupTagIdsList == null) return;

        for (int i = 0; i < tagGroupItemRefs.Length; i++)
        {
            TagGroupItemRefs refs = tagGroupItemRefs[i];
            DataList groupTagIds = tagGroupTagIdsList[i];

            if (refs == null || groupTagIds == null) continue;

            int selectedCount = CountSelectedTagsInGroup(groupTagIds);

            refs.headBadge.SetActive(selectedCount > 0);
            refs.badgeCountText.text = selectedCount.ToString();
        }
    }

    private int CountSelectedTagsInGroup(DataList groupTagIds)
    {
        int count = 0;
        
        for (int i = 0; i < groupTagIds.Count; i++)
        {
            int groupTagId = (int)groupTagIds[i].Double;

            for (int j = 0; j < selectedTagIds.Count; j++)
            {
                if ((int)selectedTagIds[j].Double == groupTagId) {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private void RefreshTagAccordionHeights()
    {
        if (tagGroupAccordions == null) return;

        for (int i = 0; i < tagGroupAccordions.Length; i++)
        {
            if (tagGroupAccordions[i] != null) {
                tagGroupAccordions[i].RefreshHeightAfterChipStateChanged();
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionTagRect);
        SetNeedsScrollbarFinalize();
    }

    public void CloseOtherTagAccordions(TagGroupAccordion current)
    {
        if (tagGroupAccordions == null) return;

        for (int i = 0; i < tagGroupAccordions.Length; i++)
        {
            TagGroupAccordion accordion = tagGroupAccordions[i];
            if (accordion == current) continue;
            accordion.SetOpen(false);
        }
    }

    private void SetupFixedTypeChips()
    {
        typeResetChip.SetInitInfo(this, ID_RESET, KIND_TYPE, null, COLOR_DEFAULT);
        typeResetChip.SetOnWithoutNotify(true);

        typeSingleChip.SetInitInfo(this, TYPE_ONEOFF, KIND_TYPE, null, COLOR_DEFAULT);
        typeRecurringChip.SetInitInfo(this, TYPE_RECURRING, KIND_TYPE, null, COLOR_DEFAULT);
    }

    private void SetupFixedDeviceChips()
    {
        deviceResetChip.SetInitInfo(this, ID_RESET, KIND_DEVICE, null, COLOR_DEFAULT);
        deviceResetChip.SetOnWithoutNotify(true);

        deviceQuestChip.SetInitInfo(this, DEVICE_QUEST_SUPPORT, KIND_DEVICE, null, COLOR_DEFAULT);
    }

    private void BuildProgramBanners(DataList programs)
    {
        sectionPrograms.SetActive(false);
        if (programs == null || programs.Count == 0) return;
        sectionPrograms.SetActive(true);

        int count = programs.Count;
        progBannerListeners = new ProgramBannerListener[count];

        TextureFormat textureFormat = timetable.GetTextureFormat();

        for (int i = 0; i < count; i++)
        {
            var programDict = programs[i].DataDictionary;

            int id = (int)programDict["id"].Double;
            string label = programDict["label"].String;
            string slug = programDict["slug"].String;
            string start_at = programDict["start_at"].String;
            string end_at = programDict["end_at"].String;

            GameObject bannerItem = Instantiate(bannerItemPrefab, progBannersParent);
            bannerItem.name = "Banner_" + slug;

            ProgramBannerListener progBannerListener = bannerItem.GetComponent<ProgramBannerListener>();

            progBannerListener.SetInitInfo(this, id, label, start_at, end_at);

            progBannerListeners[i] = progBannerListener;

            int bannerWidth = (int)programDict["banner_width"].Double;
            int bannerHeight = (int)programDict["banner_height"].Double;

            if (!programDict["banner_index"].IsNull) {
                int bannerIndex = (int)programDict["banner_index"].Double;
                byte[] bytes = timetable.GetThumbnailBytesFromWnpk(bannerIndex);
                if (bytes != null) {
                    Texture2D newTexture = new Texture2D(bannerWidth, bannerHeight, textureFormat, false, false);
                    newTexture.LoadRawTextureData(bytes);

                    newTexture.Apply(false, true);

                    progBannerListener.SetBannerTexture(newTexture);
                }
            }
        }
    }

    public void OnChipChangedById(int kind, int id, bool on)
    {
        if (id == ID_RESET) {
            if (!on) {
                if (kind == KIND_CATEGORY) catResetChipListener.SetOnWithoutNotify(true);
                else if (kind == KIND_TAG) tagResetChipListener.SetOnWithoutNotify(true);
                else if (kind == KIND_TYPE) typeResetChip.SetOnWithoutNotify(true);
                else if (kind == KIND_DEVICE) deviceResetChip.SetOnWithoutNotify(true);
                return;
            }

            if (kind == KIND_CATEGORY) {
                selectedCatIds.Clear();

                for (int i = 0; i < catChipListeners.Length; i++) {
                    catChipListeners[i].SetOnWithoutNotify(false);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionCategoryRect);
                catFlowWrapLayout.Reflow();
            } else if (kind == KIND_TAG) {
                selectedTagIds.Clear();

                for (int i = 0; i < tagChipListeners.Length; i++) {
                    if (tagChipListeners[i] != null) {
                        tagChipListeners[i].SetOnWithoutNotify(false);
                    }
                }

                if (tagRowFlowWrapLayouts != null) {
                    for (int i = 0; i < tagRowFlowWrapLayouts.Length; i++) {
                        if (tagRowFlowWrapLayouts[i] != null) {
                            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)tagRowFlowWrapLayouts[i].transform);
                            tagRowFlowWrapLayouts[i].Reflow();
                        }
                    }
                }

                RefreshTagGroupBadges();
                RefreshTagAccordionHeights();
            } else if (kind == KIND_TYPE) {
                    selectedTypeIds.Clear();

                    typeSingleChip.SetOnWithoutNotify(false);
                    typeRecurringChip.SetOnWithoutNotify(false);
            } else if (kind == KIND_DEVICE) {
                    selectedDeviceIds.Clear();

                    deviceQuestChip.SetOnWithoutNotify(false);
            }

            Apply();
            return;
        }

        DataList dataList = null;

        if (kind == KIND_CATEGORY) {
            dataList = selectedCatIds;
        } else if (kind == KIND_TAG) {
            dataList = selectedTagIds;
        } else if (kind == KIND_PROGRAM) {
            dataList = selectedProgIds;
        } else if (kind == KIND_TYPE) {
            dataList = selectedTypeIds;
        } else if (kind == KIND_DEVICE) {
            dataList = selectedDeviceIds;
        } else {
            Debug.LogWarning($"Unknown kind: {kind}");
            return;
        }

        int idx = IndexOfInt(dataList, id);
        if (on && idx == -1) dataList.Add(new DataToken(id));
        if (!on && idx >= 0) dataList.RemoveAt(idx);

        if (kind == KIND_CATEGORY) {
            if (on) {
                if (catResetChipListener.GetToggleState()) {
                    catResetChipListener.SetOnWithoutNotify(false);
                }
            } else {
                if (selectedCatIds.Count == 0) catResetChipListener.SetOnWithoutNotify(true);
            }
        } else if (kind == KIND_TAG) {
            if (on) {
                if (tagResetChipListener.GetToggleState()) {
                    tagResetChipListener.SetOnWithoutNotify(false);
                }
            } else {
                if (selectedTagIds.Count == 0) tagResetChipListener.SetOnWithoutNotify(true);
            }

            RefreshTagGroupBadges();
            RefreshTagAccordionHeights();
        } else if (kind == KIND_TYPE) {
            if (on) {
                if (typeResetChip.GetToggleState()) {
                    typeResetChip.SetOnWithoutNotify(false);
                }
            } else {
                if (selectedTypeIds.Count == 0) typeResetChip.SetOnWithoutNotify(true);
            }
        } else if (kind == KIND_DEVICE) {
            if (on) {
                if (deviceResetChip.GetToggleState()) {
                    deviceResetChip.SetOnWithoutNotify(false);
                }
            } else {
                if (selectedDeviceIds.Count == 0) deviceResetChip.SetOnWithoutNotify(true);
            }
        }

        Apply();
    }

    private int IndexOfInt(DataList dataList, int v)
    {
        for (int i = 0; i < dataList.Count; i++) {
            if (dataList[i].Int == v) {
                return i;
            }
        }

        return -1;
    }

    public void OnBannerClicked(int programId, bool on)
    {
        if (programId < 0) return;

        int idx = IndexOfInt(selectedProgIds, programId);
        if (on && idx == -1) selectedProgIds.Add(new DataToken(programId));
        if (!on && idx >= 0) selectedProgIds.RemoveAt(idx);

        Apply();
    }

    private void Apply()
    {
        timetable.ApplyFiltersById(
            ToIntArray(selectedCatIds),
            ToIntArray(selectedTagIds),
            ToIntArray(selectedProgIds),
            ToIntArray(selectedTypeIds),
            ToIntArray(selectedDeviceIds)
            );
    }

    private int[] ToIntArray(DataList dataList)
    {
        if (dataList == null || dataList.Count == 0) {
            return new int[0];
        }

        int[] a = new int[dataList.Count];
        for (int i = 0; i < dataList.Count; i++) {
            a[i] = dataList[i].Int;
        }
        return a;
    }

    public void ClearOnReset()
    {
        ClearFilterChips();
        ClearProgramBanners();

        _tagGroupBuildReady = false;

        selectedCatIds = new DataList();
        selectedTagIds = new DataList();
        selectedProgIds = new DataList();
        selectedTypeIds = new DataList();
        selectedDeviceIds = new DataList();

        typeResetChip.SetOnWithoutNotify(true);
        typeSingleChip.SetOnWithoutNotify(false);
        typeRecurringChip.SetOnWithoutNotify(false);
        deviceResetChip.SetOnWithoutNotify(true);
        deviceQuestChip.SetOnWithoutNotify(false);

        catFlowWrapLayout.ArmReflowOnNextEnable();

        wingScrollRect.verticalNormalizedPosition = 1f;
        wingScrollHandler.SyncToScrollPosition();
    }

    private void ClearFilterChips()
    {
        ClearKeywordTagBuildState();

        DestroyChildren(catResetChipParent);
        DestroyChildren(catFilterChipsParent);
        DestroyChildren(tagResetChipParent);
        DestroyChildren(tagFilterChipsParent);

        catResetChipListener = null;
        tagResetChipListener = null;
        catChipListeners = null;
        tagChipListeners = null;
        tagRowFlowWrapLayouts = null;
        tagGroupItemRefs = null;
        tagGroupAccordions = null;
        tagGroupTagIdsList = null;
    }

    private static void DestroyChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy((UnityEngine.Object)parent.GetChild(i).gameObject);
        }
    }

    private void ClearProgramBanners()
    {
        for (int i = progBannersParent.childCount - 1; i >= 0; i--)
        {
            var child = progBannersParent.GetChild(i).gameObject;
            var listener = child.GetComponent<ProgramBannerListener>();
            if (listener != null) listener.Release();
            Destroy(child);
        }
        progBannerListeners = null;
    }

    private void SetWingBodyEnabled(bool on)
    {
        if (panelBody.gameObject.activeSelf != on) panelBody.gameObject.SetActive(on);
    }

    public void SetFiltersBusy(bool busy)
    {
        if (_filtersBusy == busy) return;
        _filtersBusy = busy;
        panelCG.blocksRaycasts = !busy;
    }

    public void OnOutsideCloseAreaClicked()
    {
        if (!_isOpen) return;
        ToggleWing();
    }
}
