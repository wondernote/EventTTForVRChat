
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ProximityToggle : UdonSharpBehaviour
{
    [SerializeField] private CanvasGroup scrollViewCanvasGroup;
    [SerializeField] private EventTimetable eventTimetable;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private GameObject proximityMessage;
    [SerializeField] private GameObject returnButton;

    private CanvasGroup detailsPanelGroup;
    private bool initialized = false;
    private bool loadCompleted = false;

    private bool cleanupScheduled = false;

    void Start()
    {
        SendCustomEventDelayedFrames(nameof(EnableTriggerCollider), 10);
    }

    public void EnableTriggerCollider()
    {
        triggerCollider.enabled = true;
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        proximityMessage.SetActive(false);

        cleanupScheduled = false;

        if (initialized) {
            if (!loadCompleted) return;

            returnButton.SetActive(true);
            SetCanvasGroupVisible(scrollViewCanvasGroup, true);
            SetCanvasGroupVisible(detailsPanelGroup, true);
        } else {
            Initialize();

        };
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        proximityMessage.SetActive(true);

        if (initialized) {
            if (!loadCompleted) proximityMessage.SetActive(false);

            returnButton.SetActive(false);
            SetCanvasGroupVisible(scrollViewCanvasGroup, false);
            SetCanvasGroupVisible(detailsPanelGroup, false);

            if (!cleanupScheduled) {
                cleanupScheduled = true;
                SendCustomEventDelayedSeconds(nameof(DoCleanupIfStillOutside), 900f);
            }
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

    public void OnLoadComplete()
    {
        loadCompleted = true;
    }

    public void DoCleanupIfStillOutside()
    {
        if (!cleanupScheduled) return;
        cleanupScheduled = false;

        eventTimetable.ResetTimetable();

        initialized = false;
        loadCompleted = false;
        proximityMessage.SetActive(true);
    }
}
