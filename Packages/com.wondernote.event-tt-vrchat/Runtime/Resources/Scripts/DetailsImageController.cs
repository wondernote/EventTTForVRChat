
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Image;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DetailsImageController : UdonSharpBehaviour
{
    [Header("Image Settings")]
    [SerializeField] private RawImage detailsImage;
    [SerializeField] private GameObject initialImage;

    [Header("Input Field Settings")]
    [SerializeField] private VRCUrlInputField imageUrlInputField;
    [SerializeField] private Text placeholderText;

    private VRCImageDownloader imgDownloader;

    private void Start()
    {
        imgDownloader = new VRCImageDownloader();
    }

    public void OnSelect()
    {
        placeholderText.text = "";
    }

    public void OnURLChanged()
    {
        VRCUrl imageUrl = imageUrlInputField.GetUrl();

        if (imageUrl.Equals(VRCUrl.Empty)) {
            placeholderText.text = "URLを貼ると表示";
            return;
        }
        if (!imageUrl.Get().StartsWith("https://assets.wondernote.net/content-details/")) {
            return;
        }

        imgDownloader.DownloadImage(imageUrl, null, this.GetComponent<UdonBehaviour>(), null);
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        Destroy(initialImage);
        detailsImage.texture = result.Result;
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        Debug.LogError($"画像 {result.Url} のダウンロードに失敗しました。");
    }

    public void OnDeselect()
    {
        SetPlaceholder();
    }

    private void SetPlaceholder()
    {
        if (string.IsNullOrEmpty(imageUrlInputField.GetUrl().Get()))
        {
            placeholderText.text = "URLを貼ると表示";
        }
    }
}
