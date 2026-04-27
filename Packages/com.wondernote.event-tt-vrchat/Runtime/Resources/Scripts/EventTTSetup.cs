
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;

[DisallowMultipleComponent]
[AddComponentMenu("設定")]
public class EventTTSetup : UdonSharpBehaviour
{
    [SerializeField] private bool useSpecificTags;
    [SerializeField] private int[] selectedTagIds;
    [SerializeField] private string calendarTitle;

    [SerializeField] private string calendarTitlePrefix;
    [SerializeField] private bool calendarTitlePrefixManual;

    [SerializeField] private EventTimetable timetable;
    [SerializeField] private ProximityToggle proximityToggle;
    [SerializeField] private GameObject titleSingle;
    [SerializeField] private GameObject titleDouble;
    [SerializeField] private GameObject prefixArea;
    [SerializeField] private Image prefixRibbon;
    [SerializeField] private TextMeshProUGUI prefixText;

    public bool UseSpecificTags => useSpecificTags;
    public int[] SelectedTagIds => selectedTagIds;
    public string CalendarTitle => calendarTitle;

    public string CalendarTitlePrefix => calendarTitlePrefix;
    public bool CalendarTitlePrefixManual => calendarTitlePrefixManual;

    public EventTimetable Timetable => timetable;
    public ProximityToggle ProximityToggle => proximityToggle;
    public GameObject TitleSingle => titleSingle;
    public GameObject TitleDouble => titleDouble;
    public GameObject PrefixArea => prefixArea;
    public Image PrefixRibbon => prefixRibbon;
    public TextMeshProUGUI PrefixText => prefixText;
}