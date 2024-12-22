using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using UnityEngine.TextCore;

#if UNITY_EDITOR
[CreateAssetMenu(fileName = "EventTT_LightweightSettings", menuName = "EventTT/Settings/LightweightSettings")]
public class LightweightSettings : ScriptableObject
{
    public TMP_FontAsset liberationSansFontAsset;
    public TMP_FontAsset liberationSansExtendedUnicodeFont;
    public TMP_FontAsset genJyuuGothicBoldFontAsset;
    public bool enableLightweight;

    public string GetScriptPath()
    {
            string[] scriptGuids = AssetDatabase.FindAssets("DetailsPanelController t:Script");
            if (scriptGuids.Length > 0) {
                return AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
            } else {
                Debug.LogError("DetailsPanelController.cs が見つかりません");
                return null;
            }
    }

    public string GetTextLinkerScriptPath()
    {
        string[] scriptGuids = AssetDatabase.FindAssets("TextLinker t:Script");
        if (scriptGuids.Length > 0) {
            return AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
        } else {
            Debug.LogError("TextLinker.cs が見つかりません");
            return null;
        }
    }

    public void SetLightweightFontProperties()
    {
        if (liberationSansFontAsset == null || liberationSansExtendedUnicodeFont == null || genJyuuGothicBoldFontAsset == null)
        {
            Debug.LogError("フォントアセットが設定されていません。");
            return;
        }

        TMP_FontAsset fallbackFont = enableLightweight ? liberationSansExtendedUnicodeFont : genJyuuGothicBoldFontAsset;

        liberationSansFontAsset.fallbackFontAssetTable.Clear();
        liberationSansFontAsset.fallbackFontAssetTable.Add(fallbackFont);

        FaceInfo faceInfo = liberationSansFontAsset.faceInfo;
        faceInfo.underlineOffset = enableLightweight ? -12 : 10;
        faceInfo.underlineThickness = enableLightweight ? 12 : 30;
        liberationSansFontAsset.faceInfo = faceInfo;
        EditorUtility.SetDirty(liberationSansFontAsset);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(liberationSansFontAsset));
    }

    public void ToggleTextLinkerCode()
    {
        string scrPath = GetScriptPath();

        string[] scriptLines = File.ReadAllLines(scrPath);

        using (StreamWriter writer = new StreamWriter(scrPath))
        {
            foreach (string line in scriptLines)
            {
                if (line.Contains("textLinker.SetLinkedFieldContainerPrefab(linkedFieldContainerPrefab);"))
                {
                    if (enableLightweight && !line.TrimStart().StartsWith("//"))
                    {
                        writer.WriteLine("// " + line);
                    }
                    else if (!enableLightweight && line.TrimStart().StartsWith("//"))
                    {
                        writer.WriteLine(line.Substring(3));
                    }
                    else
                    {
                        writer.WriteLine(line);
                    }
                }
                else if (line.Contains("ReplaceTagWithStyle(htmlText, \"<link\", \"</link>\""))
                {
                    if (enableLightweight)
                    {
                        writer.WriteLine(line.Replace("\"link\"", "\"link-lightweight\""));
                    }
                    else
                    {
                        writer.WriteLine(line.Replace("\"link-lightweight\"", "\"link\""));
                    }
                }
                else
                {
                    writer.WriteLine(line);
                }
            }
        }
        AssetDatabase.ImportAsset(scrPath);

        string textLinkerPath = GetTextLinkerScriptPath();

        string[] textLinkerLines = File.ReadAllLines(textLinkerPath);

        using (StreamWriter writer = new StreamWriter(textLinkerPath))
        {
            foreach (string line in textLinkerLines)
            {
                if (line.Contains("InitializeLinkedField();"))
                {
                    if (enableLightweight && !line.TrimStart().StartsWith("//"))
                    {
                        writer.WriteLine("// " + line);
                    }
                    else if (!enableLightweight && line.TrimStart().StartsWith("//"))
                    {
                        writer.WriteLine(line.Substring(3));
                    }
                    else
                    {
                        writer.WriteLine(line);
                    }
                }
                else
                {
                    writer.WriteLine(line);
                }
            }
        }
        AssetDatabase.ImportAsset(textLinkerPath);

    }
}
#endif
