
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
    [SerializeField] private Button tabUIButton;
    [SerializeField] private CanvasGroup panelCG;
    [SerializeField] private RectTransform arrowIcon;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private FlowWrapLayout catFlowWrapLayout;
    [SerializeField] private RectTransform sectionCategoryRect;
    [SerializeField] private FlowWrapLayout tagFlowWrapLayout;
    [SerializeField] private RectTransform sectionTagRect;
    [SerializeField] private CanvasGroup mainPanelCanvasGroup;

    private Vector2 _iconPosClosed, _iconPosOpen;
    private bool _needsScrollbarFinalize = false;

    private float motionTime = 0.12f;
    private float panelWidth;
    private float tabWidth;
    private float _closedX;
    private float _targetX;
    private bool  _isOpen;

    private float tabHoverExtra = 10f;
    private float tabMotionTime = 0.04f;
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

    void Start()
    {
        tabWidth = tabButtonRect.sizeDelta.x;
        float bodyWidth = panelBody.sizeDelta.x;
        panelWidth = bodyWidth + tabWidth;
        _closedX = -bodyWidth;
        _baseTabWidth = tabButtonRect.sizeDelta.x;

        _iconPosClosed = arrowIcon.anchoredPosition;
        _iconPosOpen = new Vector2(0f, _iconPosClosed.y);

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

                    if (panelBody.gameObject.activeSelf) SetWingBodyEnabled(false);
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

        wingScrollHandler.UpdateCustomScroll();
    }

    public void ToggleWing()
    {
        audioManager.PlayWingSound();
        _hover = false;
        if (_isOpen) CloseWing();
        else OpenWing();
    }

    private void OpenWing()
    {
        _isOpen = true;
        _targetX = 0f;

        ApplyOpenVisuals();

        if (_needsScrollbarFinalize) {
            _needsScrollbarFinalize = false;
            SendCustomEventDelayedFrames(nameof(FinalizeScrollbarVisibility), 2);
        }
    }

    public void SetNeedsScrollbarFinalize()
    {
        _needsScrollbarFinalize = true;
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

    public void SetupChipsFromSchema(DataList categories, DataList tags, DataList programs)
    {
        BuildResetChip(catResetChipParent, KIND_CATEGORY);
        BuildFilterChips(categories, catFilterChipsParent, KIND_CATEGORY, catFlowWrapLayout);
        SetupFixedTypeChips();
        SetupFixedDeviceChips();
        // BuildResetChip(tagResetChipParent, KIND_TAG);
        // BuildFilterChips(tags, tagFilterChipsParent, KIND_TAG, tagFlowWrapLayout);
        BuildProgramBanners(programs);
    }

    public void FinalizeScrollbarVisibility()
    {
        RectTransform wingContentRect = wingContent.GetComponent<RectTransform>();
        float wingContentHeight = wingContentRect.rect.height;
        float wingViewportHeight = wingScrollRect.GetComponent<RectTransform>().rect.height;
        bool visible = wingContentHeight > wingViewportHeight;
        wingScrollHandler.SetScrollbarVisible(visible);
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

            if (!programDict["banner_base64"].IsNull) {
                string b64 = programDict["banner_base64"].String;
                if (!string.IsNullOrEmpty(b64)) {
                    byte[] bytes = Convert.FromBase64String(b64);

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
                    tagChipListeners[i].SetOnWithoutNotify(false);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionTagRect);
                tagFlowWrapLayout.Reflow();
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
        tagFlowWrapLayout.ArmReflowOnNextEnable();

        wingScrollRect.verticalNormalizedPosition = 1f;
        wingScrollHandler.SyncToScrollPosition();
    }

    private void ClearFilterChips()
    {
        DestroyChildren(catResetChipParent);
        DestroyChildren(catFilterChipsParent);
        DestroyChildren(tagResetChipParent);
        DestroyChildren(tagFilterChipsParent);

        catResetChipListener = null;
        tagResetChipListener = null;
        catChipListeners = null;
        tagChipListeners = null;
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
}
