
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EventItemScript : UdonSharpBehaviour
{
    [Header("Main Element Settings")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dateTimeText;
    [SerializeField] private Image dateTimeTextBackground;
    [SerializeField] private RawImage thumbnailImage;
    private int imageIndex;
    private int gridX;
    private int gridY;

    private Canvas canvas;
    private CanvasGroup mainPanelCanvasGroup;
    private GameObject detailsPanelPrefab;
    private GameObject detailsTextPrefab;
    private GameObject detailsImagePrefab;
    private GameObject videoPlayerPrefab;
    private GameObject linkedFieldContainerPrefab;

    private string title;
    private DateTime dateTime;
    private string summary;
    private string details;
    private Texture2D texture;
    private bool isImageSetSuccess;
    private string groupID;
    private int supportedModel;

    private float thumbnailWidth;
    private float thumbnailHeight;
    private Vector2 uvSize;

    private AudioManager audioManager;
    private bool isClicked = false;

    public void SetImageIndex(int index)
    {
        imageIndex = index;
    }
    public int GetImageIndex()
    {
        return imageIndex;
    }

    public void SetGridX(int x)
    {
        gridX = x;
    }
    public int GetGridX()
    {
        return gridX;
    }

    public void SetGridY(int y)
    {
        gridY = y;
    }
    public int GetGridY()
    {
        return gridY;
    }

    public void SetTitle(string _title)
    {
        title = _title;
        titleText.text = title;
        titleText.ForceMeshUpdate();
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

    public void SetThumbnailImage(Texture2D _texture, bool imageSetSuccess)
    {
        texture = _texture;
        isImageSetSuccess = imageSetSuccess;
        thumbnailImage.texture = texture;
    }
    public RawImage GetThumbnailImage()
    {
        return thumbnailImage;
    }

    public void SetDetails(string _summary, string _details, string _groupID, int _supportedModel, Canvas _canvas, GameObject _detailsPanelPrefab, GameObject _detailsTextPrefab, GameObject _detailsImagePrefab, GameObject _videoPlayerPrefab, GameObject _linkedFieldContainerPrefab, CanvasGroup _mainPanelCanvasGroup, float _thumbnailWidth, float _thumbnailHeight, Vector2 _uvSize, AudioManager _audioManager)
    {
        summary = _summary;
        details = _details;
        groupID = _groupID;
        supportedModel = _supportedModel;

        canvas = _canvas;
        mainPanelCanvasGroup = _mainPanelCanvasGroup;
        detailsPanelPrefab = _detailsPanelPrefab;
        detailsTextPrefab = _detailsTextPrefab;
        detailsImagePrefab = _detailsImagePrefab;
        videoPlayerPrefab = _videoPlayerPrefab;
        linkedFieldContainerPrefab = _linkedFieldContainerPrefab;
        audioManager = _audioManager;

        thumbnailWidth = _thumbnailWidth;
        thumbnailHeight = _thumbnailHeight;
        uvSize = _uvSize;
    }

    public void OnThumbnailClicked()
    {
        if(imageIndex >= 0) {
            GameObject detailsPanel = Instantiate(detailsPanelPrefab);
            detailsPanel.transform.SetParent(canvas.transform, false);

            DetailsPanelController detailsPanelController = detailsPanel.GetComponent<DetailsPanelController>();
            if (detailsPanelController != null)
            {
                detailsPanelController.SetEventDetails(title, dateTime, summary, details, texture, isImageSetSuccess, groupID, supportedModel, mainPanelCanvasGroup, gridX, gridY, thumbnailWidth, thumbnailHeight, uvSize, detailsTextPrefab, detailsImagePrefab, videoPlayerPrefab, linkedFieldContainerPrefab, audioManager);
            }

            mainPanelCanvasGroup.interactable = false;
            mainPanelCanvasGroup.blocksRaycasts = false;
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
}
