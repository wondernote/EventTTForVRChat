#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

public static class EventTTUrlGenerator
{
    private const string TARGET_PREFAB_PATH = "Packages/com.wondernote.event-tt-vrchat/Runtime/EventTT.prefab";

    private const int SLOT_COUNT = 3;
    private const int DETAILED_PACKS_PER_SLOT = 512;
    private const int DETAILED_VIDEOS_PER_SLOT = 128;

    private const string PC_BASE_URL = "https://wondernote.net/api/event_tt/detailed_images_pc_";
    private const string ANDROID_BASE_URL = "https://wondernote.net/api/event_tt/detailed_images_android_";
    private const string VIDEO_BASE_URL = "https://wondernote.net/event_tt/video/";

    [MenuItem("Tools/Wonder Note/Generate EventTT URLs")]

    private static void Generate()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TARGET_PREFAB_PATH);
        if (prefabAsset == null)
        {
            Debug.LogError($"Prefab が見つかりません: {TARGET_PREFAB_PATH}");
            return;
        }

        if (PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.NotAPrefab)
        {
            Debug.LogError($"指定パスのアセットは prefab ではありません: {TARGET_PREFAB_PATH}");
            return;
        }

        Transform manager = prefabAsset.transform.Find("EventTT_Manager");
        if (manager == null)
        {
            Debug.LogError($"指定 prefab に EventTT_Manager がありません: {TARGET_PREFAB_PATH}");
            return;
        }

        EventTimetable timetable = manager.GetComponent<EventTimetable>();
        if (timetable == null)
        {
            Debug.LogError($"EventTT_Manager に EventTimetable がありません: {TARGET_PREFAB_PATH}");
            return;
        }

        Undo.RecordObject(timetable, "Generate EventTT URLs");

        VRCUrl[] pcUrls = BuildUrls(PC_BASE_URL);
        VRCUrl[] androidUrls = BuildUrls(ANDROID_BASE_URL);
        VRCUrl[] videoUrls = BuildVideoUrls();

        SetPrivateSerializedField(timetable, "pcDetailedImagesUrls", pcUrls);
        SetPrivateSerializedField(timetable, "androidDetailedImagesUrls", androidUrls);
        SetPrivateSerializedField(timetable, "detailedVideoUrls", videoUrls);

        EditorUtility.SetDirty(timetable);
        PrefabUtility.SavePrefabAsset(prefabAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"Detailed URLs generated and saved. Prefab={prefabAsset.name}, PC={pcUrls.Length}, Android={androidUrls.Length}, Video={videoUrls.Length}");
    }

    private static VRCUrl[] BuildUrls(string baseUrl)
    {
        int total = SLOT_COUNT * DETAILED_PACKS_PER_SLOT;
        VRCUrl[] urls = new VRCUrl[total];

        int i = 0;
        for (int slot = 1; slot <= SLOT_COUNT; slot++)
        {
            for (int packNo = 1; packNo <= DETAILED_PACKS_PER_SLOT; packNo++)
            {
                urls[i] = new VRCUrl($"{baseUrl}{slot}_{packNo}.wnpk");
                i++;
            }
        }

        return urls;
    }

    private static VRCUrl[] BuildVideoUrls()
    {
        int total = SLOT_COUNT * DETAILED_VIDEOS_PER_SLOT;
        VRCUrl[] urls = new VRCUrl[total];

        int i = 0;
        for (int slot = 1; slot <= SLOT_COUNT; slot++)
        {
            for (int videoId = 0; videoId < DETAILED_VIDEOS_PER_SLOT; videoId++)
            {
                urls[i] = new VRCUrl($"{VIDEO_BASE_URL}{slot}/{videoId}");
                i++;
            }
        }

        return urls;
    }

    private static void SetPrivateSerializedField(object target, string fieldName, object value)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (fi == null)
        {
            Debug.LogError($"Field not found: {fieldName}");
            return;
        }

        fi.SetValue(target, value);
    }
}
#endif