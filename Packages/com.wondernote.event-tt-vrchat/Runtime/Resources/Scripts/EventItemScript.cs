
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
    [SerializeField] private RawImage thumbnailImage;
    [SerializeField] private Button thumbnailButton;

    private Canvas canvas;
    private CanvasGroup mainPanelCanvasGroup;
    private GameObject detailsPanelPrefab;
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

    private AudioManager audioManager;
    private bool isClicked = false;

    private EventTimetable eventTimetable;
    private ProximityToggle proximityToggle;

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

    public void SetDetails(int _contentID, string _summary, string _details, string _groupID, int _supportedModel, Canvas _canvas, GameObject _detailsPanelPrefab, GameObject _detailsTextPrefab, GameObject _detailsImagePrefab, GameObject[] _videoPlayerPrefabs, GameObject _linkedFieldContainerPrefab, CanvasGroup _mainPanelCanvasGroup, AudioManager _audioManager, EventTimetable timetable)
    {
        contentID = _contentID;
        summary = _summary;
        details = _details;
        groupID = _groupID;
        supportedModel = _supportedModel;
        canvas = _canvas;
        mainPanelCanvasGroup = _mainPanelCanvasGroup;
        detailsPanelPrefab = _detailsPanelPrefab;
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
                DataList detailedImgsByContentList = eventTimetable.GetDetailedImgsByContent(contentID);
                TextureFormat textureFormat = eventTimetable.GetTextureFormat();

                detailsPanelController.SetEventDetails(title, dateTime, summary, details, texture, groupID, supportedModel, mainPanelCanvasGroup, detailsTextPrefab, detailsImagePrefab, videoPlayerPrefabs, linkedFieldContainerPrefab, audioManager, detailedImgsByContentList, textureFormat, eventTimetable);
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
}
