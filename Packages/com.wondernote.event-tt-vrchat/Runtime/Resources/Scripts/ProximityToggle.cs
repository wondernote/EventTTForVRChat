
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ProximityToggle : UdonSharpBehaviour
{
    [SerializeField] private CanvasGroup scrollViewCanvasGroup;
    [SerializeField] private EventTimetable eventTimetable;
    [SerializeField] private SphereCollider triggerCollider;
    [SerializeField] private BoxCollider uiCanvasCollider;
    [SerializeField] private GameObject returnButton;
    [SerializeField] private GameObject wingPanel;
    private CanvasGroup detailsPanelGroup;
    private bool initialized = false;
    private bool loadCompleted = false;

    private const float CleanupDelaySeconds = 900f;
    private const float CleanupTimeToleranceSeconds = 1f;
    private bool isLocalPlayerInside = false;
    private const float InvalidCleanupDueTime = -1f;
    private float cleanupDueTime = InvalidCleanupDueTime;

    public SphereCollider TriggerCollider => triggerCollider;

    void Start()
    {
        SetUiCanvasColliderEnabled(false);

        #if !UNITY_IOS
            SendCustomEventDelayedFrames(nameof(EnableTriggerCollider), 10);
        #endif
    }

    public void EnableTriggerCollider()
    {
        triggerCollider.enabled = true;
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        SetUiCanvasColliderEnabled(true);
        eventTimetable.ApplyInsideView();

        isLocalPlayerInside = true;
        cleanupDueTime = InvalidCleanupDueTime;

        if (initialized) {
            if (!loadCompleted) return;

            eventTimetable.ResumeAllTicksForProximity();
            returnButton.SetActive(true);
            SetCanvasGroupVisible(scrollViewCanvasGroup, true);
            SetCanvasGroupVisible(detailsPanelGroup, true);
            wingPanel.SetActive(true);
        } else {
            Initialize();

        };
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        isLocalPlayerInside = false;
        eventTimetable.OnPointerExitMain();
        SetUiCanvasColliderEnabled(false);
        eventTimetable.ApplyOutsideView();

        if (initialized) {
            if (!loadCompleted) eventTimetable.ApplyInsideView();

            eventTimetable.PauseAllTicksForProximity();
            returnButton.SetActive(false);
            SetCanvasGroupVisible(scrollViewCanvasGroup, false);
            SetCanvasGroupVisible(detailsPanelGroup, false);
            wingPanel.SetActive(false);

            cleanupDueTime = Time.time + CleanupDelaySeconds;
            SendCustomEventDelayedSeconds(nameof(DoCleanupIfStillOutside), CleanupDelaySeconds);
        }
    }

    private void Initialize()
    {
        initialized = true;
        eventTimetable.BeginLoad();
    }

    public void RegisterDetailsPanel(CanvasGroup panel)
    {
        detailsPanelGroup = panel;
    }

    private void SetCanvasGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts  = visible;
    }

    private void SetUiCanvasColliderEnabled(bool enabled)
    {
        uiCanvasCollider.enabled = enabled;
    }

    public void OnLoadComplete()
    {
        loadCompleted = true;
    }

    public void DoCleanupIfStillOutside()
    {
        if (isLocalPlayerInside) return;
        if (cleanupDueTime < 0f) return;
        if (Time.time + CleanupTimeToleranceSeconds < cleanupDueTime) return;

        cleanupDueTime = InvalidCleanupDueTime;

        eventTimetable.ResetTimetable();

        initialized = false;
        loadCompleted = false;
        SetUiCanvasColliderEnabled(false);
        eventTimetable.ApplyOutsideView();
    }
}
