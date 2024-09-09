using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[CustomEditor(typeof(LightweightSettings))]
public class LightweightSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LightweightSettings settings = (LightweightSettings)target;
        settings.enableLightweight = EditorGUILayout.Toggle("軽量化", settings.enableLightweight);

        EditorGUILayout.HelpBox(
            "オンにすると、アセット容量が約5MBから約700KBに削減されます。ただし、以下の機能が使えません。\n" +
            "●日本語のデザインフォント\n" +
            "●リンクのコピー＆ペースト（画像・動画を除く）",
            MessageType.Info
        );

        if (GUI.changed)
        {
            settings.SetLightweightFontProperties();
            settings.ToggleTextLinkerCode();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
