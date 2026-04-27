
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using VRC.SDK3.Data;
using WonderNote.EventTimeTable;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EventItemScript : UdonSharpBehaviour
{
    [Header("Main Element Settings")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dateTimeText;
    [SerializeField] private Image dateTimeTextBackground;
    [SerializeField] private TextMeshProUGUI categoryLabelText;
    [SerializeField] private Image categoryLabelTextBackground;
    [SerializeField] private RawImage thumbnailImage;
    [SerializeField] private Button thumbnailButton;

    private Canvas canvas;
    private CanvasGroup mainPanelCanvasGroup;
    private GameObject detailsPanelPrefab;
    private GameObject tagBadgePrefab;
    private GameObject detailsTextPrefab;
    private GameObject detailsImagePrefab;
    private GameObject[] videoPlayerPrefabs;
    private GameObject linkedFieldContainerPrefab;

    private int contentID;
    private string title;
    private DateTime dateTime;
    private string summary;
    private string details;
    private Texture2D texture;
    private bool isThumbnailSet = false;
    private string groupID;
    private int supportedModel;
    private int detailSlot;
    private int detailPackNo;
    private string[] tagLabels;
    private string categoryLabel;
    private Color categoryColor;

    private AudioManager audioManager;
    private bool isClicked = false;
    private int _stableIndex = -1;

    private EventTimetable eventTimetable;
    private ProximityToggle proximityToggle;

    private bool _isMatch = true;
    private bool _isPlaceholder = false;
    public bool IsPlaceholder => _isPlaceholder;

    private bool _visualOn = true;

    public void SetTitle(string _title)
    {
        title = _title;
        titleText.text = title;
        titleText.ForceMeshUpdate(true);
    }

    public int GetTextLineCount()
    {
        int textLineCount = titleText.textInfo.lineCount;
        return textLineCount;
    }

    public void SetDateTime(DateTime _dateTime)
    {
        dateTime = _dateTime;
        if (dateTime != DateTime.MinValue) {
            dateTimeText.text = dateTime.ToString("HH:mm～");
        } else {
            dateTimeText.text = "";
            dateTimeTextBackground.enabled = false;
        }
    }

    public void SetCategory(string _categoryLabel, Color _categoryColor)
    {
        categoryLabel = _categoryLabel;
        categoryLabelText.text = categoryLabel;
        categoryLabelText.ForceMeshUpdate(true);

        categoryColor = _categoryColor;
        Color categoryColorUI = _categoryColor;
        categoryColorUI.a = 225f / 255f;
        categoryLabelTextBackground.color = categoryColorUI;

        if (categoryLabel == "ノンジャンル") {
            categoryLabelText.color = new Color32(50, 50, 50, 255);
        } else {
            categoryLabelText.color = new Color32(255, 255, 255, 255);
        }
    }

    public void SetCategoryHidden()
    {
        categoryLabel = string.Empty;
        categoryLabelText.text = categoryLabel;
        categoryLabelText.gameObject.SetActive(false);
        categoryLabelTextBackground.gameObject.SetActive(false);
    }

    public void SetThumbnailImage(Texture2D _texture, bool status)
    {
        texture = _texture;
        isThumbnailSet = status;
        thumbnailImage.texture = texture;

        if (status) {
        Rect currentRect = thumbnailImage.uvRect;
        thumbnailImage.uvRect = new Rect(currentRect.x, currentRect.y + currentRect.height, currentRect.width, -currentRect.height);
        } else {
            thumbnailButton.interactable = false;
        }
    }

    public void SetDetails(int _contentID, string _summary, string _details, string _groupID, int _supportedModel, int _detailSlot, int _detailPackNo, string[] _tagLabels, Canvas _canvas, GameObject _detailsPanelPrefab, GameObject _tagBadgePrefab, GameObject _detailsTextPrefab, GameObject _detailsImagePrefab, GameObject[] _videoPlayerPrefabs, GameObject _linkedFieldContainerPrefab, CanvasGroup _mainPanelCanvasGroup, AudioManager _audioManager, EventTimetable timetable)
    {
        contentID = _contentID;
        summary = _summary;
        details = _details;
        groupID = _groupID;
        supportedModel = _supportedModel;
        detailSlot = _detailSlot;
        detailPackNo = _detailPackNo;
        tagLabels = _tagLabels;
        canvas = _canvas;
        mainPanelCanvasGroup = _mainPanelCanvasGroup;
        detailsPanelPrefab = _detailsPanelPrefab;
        tagBadgePrefab = _tagBadgePrefab;
        detailsTextPrefab = _detailsTextPrefab;
        detailsImagePrefab = _detailsImagePrefab;
        videoPlayerPrefabs = _videoPlayerPrefabs;
        linkedFieldContainerPrefab = _linkedFieldContainerPrefab;
        audioManager = _audioManager;
        eventTimetable = timetable;
    }

    public void OnThumbnailClicked()
    {
        if(isThumbnailSet) {
            GameObject detailsPanel = Instantiate(detailsPanelPrefab);
            detailsPanel.transform.SetParent(canvas.transform, false);

            DetailsPanelController detailsPanelController = detailsPanel.GetComponent<DetailsPanelController>();
            if (detailsPanelController != null)
            {
                TextureFormat textureFormat = eventTimetable.GetTextureFormat(); 

                detailsPanelController.SetEventDetails(title, dateTime, categoryLabel, categoryColor, summary, details, texture, groupID, supportedModel, tagLabels, mainPanelCanvasGroup, tagBadgePrefab, detailsTextPrefab, detailsImagePrefab, videoPlayerPrefabs, linkedFieldContainerPrefab, audioManager, textureFormat, eventTimetable);

                if (detailPackNo > 0) {
                    eventTimetable.RequestDetailedImages(detailsPanelController, detailSlot, detailPackNo);
                }
            }

            mainPanelCanvasGroup.interactable = false;
            mainPanelCanvasGroup.blocksRaycasts = false;

            CanvasGroup dpGroup = detailsPanel.GetComponent<CanvasGroup>();
            if (proximityToggle != null && dpGroup != null)
            {
                proximityToggle.RegisterDetailsPanel(dpGroup);
            }
        }

        isClicked = false;
    }

    public void OnPointerDown()
    {
        if (!isClicked && audioManager != null)
        {
            audioManager.PlayClickSound();
            isClicked = true;
        }
    }

    public void OnThumbnailHovered()
    {
        if (audioManager != null)
        {
            audioManager.PlayHoverSound();
        }
    }

    public void SetProximityToggle(ProximityToggle toggle)
    {
        proximityToggle = toggle;
    }

    public void ApplyMatchState(bool isMatch)
    {
        _isMatch = isMatch;
    }

    public void SetupAsPlaceholder(Texture2D blankLogo)
    {
        _isPlaceholder = true;
        SetTitle("");
        SetDateTime(DateTime.MinValue);
        SetCategoryHidden();
        SetThumbnailImage(blankLogo, false);
        thumbnailImage.raycastTarget = false;
    }

    public void SetStableIndex(int idx)
    {
        _stableIndex = idx;
    }

    public int GetStableIndex()
    {
        return _stableIndex;
    }

    public bool IsRealAndMatch()
    {
        return !_isPlaceholder && _isMatch;
    }

    public void SetVisualEnabled(bool on)
    {
        if (on == _visualOn) return;
        _visualOn = on;

        thumbnailImage.enabled = on;
        dateTimeTextBackground.enabled = on;
        categoryLabelTextBackground.enabled = on;
        titleText.enabled = on;
        dateTimeText.enabled = on;
        categoryLabelText.enabled = on;

        bool thumbnailInteractive = on && !_isPlaceholder && isThumbnailSet;
        thumbnailButton.interactable = thumbnailInteractive;
        thumbnailImage.raycastTarget = thumbnailInteractive;

        dateTimeTextBackground.raycastTarget = on;
        categoryLabelTextBackground.raycastTarget = on;
        titleText.raycastTarget = on;
        dateTimeText.raycastTarget = on;
        categoryLabelText.raycastTarget = on;
    }
}
