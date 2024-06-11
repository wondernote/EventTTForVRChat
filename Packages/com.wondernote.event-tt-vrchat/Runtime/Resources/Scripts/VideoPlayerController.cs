
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.Base;
using VRC.SDK3.Components.Video;
using TMPro;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VideoPlayerController : UdonSharpBehaviour
{
    [Header("Video Player Settings")]
    [SerializeField] private BaseVRCVideoPlayer videoPlayer;
    [SerializeField] private GameObject videoUrlBackground;

    [Header("Input Field Settings")]
    [SerializeField] private VRCUrlInputField videoUrlInputField;
    [SerializeField] private Text placeholderText;
    private VRCUrl lastUrl;

    [Header("Playback Settings")]
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject replayButton;
    private bool isPlaying = false;

    [Header("SeekBar Settings")]
    [SerializeField] private Slider seekBarSlider;
    [SerializeField] private TextMeshProUGUI timeCodeText;
    private float videoDuration;
    private string formatVideoDuration;
    private bool isUpdating = false;
    private bool hasVideoEnded = false;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private GameObject highVolumeIcon;
    [SerializeField] private GameObject lowVolumeIcon;
    [SerializeField] private GameObject muteIcon;
    private bool isMuted = false;
    private float previousVolume = 0.5f;
    private bool volumeIconClicked = false;

    [Header("Video UI Settings")]
    [SerializeField] private GameObject playerUI;
    [SerializeField] private CanvasGroup playerUICanvasGroup;
    private float fadeDuration = 0.1f;
    private float fadeProgress = 0.0f;
    private bool isFadingIn = false;
    private bool isFadingOut = false;
    private bool isInitialPhaseOver;
    private bool isHovered = false;

    private void Start()
    {
        lastUrl = VRCUrl.Empty;
    }

    public void OnSelect()
    {
        placeholderText.text = "";
    }

    public void OnURLChanged()
    {
        SetPlaceholder();

        VRCUrl currentUrl = videoUrlInputField.GetUrl();
        if ((currentUrl.Get() != lastUrl.Get()) && (currentUrl.Get().StartsWith("https://youtu.be/") || currentUrl.Get().StartsWith("https://www.youtube.com/")))
        {
            videoPlayer.PlayURL(currentUrl);
            lastUrl = currentUrl;
        }
    }

    public void OnDeselect()
    {
        SetPlaceholder();
    }

    private void SetPlaceholder()
    {
        if (string.IsNullOrEmpty(videoUrlInputField.GetUrl().Get()))
        {
            placeholderText.text = "再生するには上記のURLをコピーしてここに貼り付けてください";
        }
    }

    public override void OnVideoStart()
    {
        if (!isPlaying) {
            videoDuration = videoPlayer.GetDuration();
            formatVideoDuration = FormatTime(videoDuration);

            videoUrlBackground.SetActive(false);
            playerUI.SetActive(true);
            isPlaying = true;
            SetUIButton(isPlaying);
            hasVideoEnded = false;

            if (isHovered) {
                OnScreenPointerEnter();
            } else {
                OnScreenPointerExit();
            }

            isInitialPhaseOver = false;
            SendCustomEventDelayedSeconds(nameof(SetInitialPhaseOver), 3f);
        }
    }

    public override void OnVideoEnd()
    {
        isPlaying = false;
        SetReplayButton();
        hasVideoEnded = true;
        timeCodeText.text = formatVideoDuration + " / " + formatVideoDuration;
    }

    public override void OnVideoError(VideoError videoError)
    {
        videoUrlBackground.SetActive(true);
        playerUI.SetActive(false);
        isPlaying = false;
        hasVideoEnded = true;
    }

    private void Update()
    {
        if (isPlaying) {
            UpdateSeekBar();
        }

        if (isInitialPhaseOver && isPlaying) {
            if (isFadingIn) {
                fadeProgress += Time.deltaTime / fadeDuration;
                playerUICanvasGroup.alpha = Mathf.Clamp01(fadeProgress);

                if (fadeProgress >= 1.0f) {
                    isFadingIn = false;
                }
            }

            if (isFadingOut) {
                fadeProgress -= Time.deltaTime / fadeDuration;
                playerUICanvasGroup.alpha = Mathf.Clamp01(fadeProgress);

                if (fadeProgress <= 0.0f) {
                    isFadingOut = false;
                }
            }
        } else {
            playerUICanvasGroup.alpha = 1.0f;
        }
    }

    public void OnScreenPointerEnter()
    {
        isFadingOut = false;
        isFadingIn = true;
        fadeProgress = playerUICanvasGroup.alpha;
        isHovered = true;
    }

    public void OnScreenPointerExit()
    {
        isFadingIn = false;
        isFadingOut = true;
        fadeProgress = playerUICanvasGroup.alpha;
        isHovered = false;
    }

    public void SetInitialPhaseOver()
    {
        isInitialPhaseOver = true;
    }

    public void OnPauseClicked()
    {
        isPlaying = false;
        videoPlayer.Pause();
        SetUIButton(isPlaying);
    }

    public void OnPlayClicked()
    {
        isPlaying = true;
        videoPlayer.Play();
        SetUIButton(isPlaying);
    }

    public void OnUIBackgroundClicked()
    {
        if (hasVideoEnded) {
            return;
        }

        if (isPlaying) {
            OnPauseClicked();
        } else {
            OnPlayClicked();
        }
    }

    public void OnReplayClicked()
    {
        isPlaying = true;
        videoPlayer.Play();
        SetUIButton(isPlaying);
        hasVideoEnded = false;
    }

    private void SetUIButton(bool playing)
    {
        playButton.SetActive(!playing);
        pauseButton.SetActive(playing);
        replayButton.SetActive(false);
    }

    private void SetReplayButton()
    {
        playButton.SetActive(false);
        pauseButton.SetActive(false);
        replayButton.SetActive(true);
    }

    public void OnSeekBarChanged()
    {
        if (!isUpdating) {
            float seekTime = seekBarSlider.value * videoPlayer.GetDuration();

            if (hasVideoEnded) {
                OnReplayClicked();
            }

            videoPlayer.SetTime(seekTime);
        }
    }

    private void UpdateSeekBar()
    {
        float currentTime = videoPlayer.GetTime();
        isUpdating = true;
        seekBarSlider.value = currentTime / videoDuration;
        isUpdating = false;
        timeCodeText.text = FormatTime(currentTime) + " / " + formatVideoDuration;
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60F);
        int seconds = (int)time - minutes * 60;
        return $"{minutes:0}:{seconds:00}";
    }

    public void OnVolumeChanged()
    {
        float volume = volumeSlider.value;
        audioSource.volume = volume;

        muteIcon.SetActive(volume == 0);
        lowVolumeIcon.SetActive(volume > 0 && volume <= 0.5);
        highVolumeIcon.SetActive(volume > 0.5);

        if(!volumeIconClicked && isMuted){
            isMuted = !isMuted;
        }

        volumeIconClicked = false;
    }

    public void BeginVolumeSliderDrag()
    {
        previousVolume = volumeSlider.value;
    }

    public void EndVolumeSliderDrag()
    {
        if(audioSource.volume == 0){
            isMuted = true;
        }
    }

    public void OnVolumeIconClicked()
    {
        isMuted = !isMuted;
        volumeIconClicked = true;

        if (isMuted) {
            previousVolume = volumeSlider.value;
            volumeSlider.value = 0;
        } else {
            volumeSlider.value = previousVolume;
        }
    }
}
