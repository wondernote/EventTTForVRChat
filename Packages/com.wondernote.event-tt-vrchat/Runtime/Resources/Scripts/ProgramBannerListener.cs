
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ProgramBannerListener : UdonSharpBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private RawImage banner;
    [SerializeField] private Image glow;
    [SerializeField] private TextMeshProUGUI titleText;
    private LeftWingController wingController;
    private int programId = -1;
    private Color baseColor = Color.white;
    private const float BANNER_ON = 1.0f, BANNER_OFF = 0.30f, BANNER_HOVER = 0.80f;
    private const float GLOW_ON = 0.4f, GLOW_OFF = 0.10f, GLOW_HOVER = 0.30f;
    private Texture2D _ownedTex;

    public void SetInitInfo(LeftWingController controller, int id, string label, string startIso, string endIso)
    {
        wingController = controller;
        programId = id;
        string range = BuildDateRangeLabel(startIso, endIso);
        titleText.text = string.IsNullOrEmpty(range) ? label : (label + range);
    }

    private static string BuildDateRangeLabel(string startIso, string endIso)
    {
        string s = ToMonthDay(startIso);
        string e = ToMonthDay(endIso);

        if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(e))
        {
            return (s == e) ? $"（{s}）" : $"（{s}～{e}）";
        }
        if (!string.IsNullOrEmpty(s)) return $"（{s}～）";
        if (!string.IsNullOrEmpty(e)) return $"（～{e}）";
        return "";
    }

    private static string ToMonthDay(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";

        int firstDash = iso.IndexOf('-');
        if (firstDash < 0 || firstDash + 1 >= iso.Length) return "";

        int secondDash = iso.IndexOf('-', firstDash + 1);
        if (secondDash < 0 || secondDash + 1 >= iso.Length) return "";

        string mm = iso.Substring(firstDash + 1, secondDash - (firstDash + 1));

        string ddPart = iso.Substring(secondDash + 1);
        int digits = 0;
        while (digits < ddPart.Length && char.IsDigit(ddPart[digits])) digits++;
        if (digits == 0) return "";

        string dd = ddPart.Substring(0, digits);

        int m, d;
        if (!int.TryParse(mm, out m)) return "";
        if (!int.TryParse(dd, out d)) return "";
        if (m <= 0 || d <= 0) return "";

        return m.ToString() + "/" + d.ToString();
    }

    public void SetBannerTexture(Texture2D _texture)
    {
        _ownedTex = _texture;
        banner.texture = _texture;

        Rect currentRect = banner.uvRect;
        banner.uvRect = new Rect(currentRect.x, currentRect.y + currentRect.height, currentRect.width, -currentRect.height);
    }

    public void OnToggleChanged()
    {
        wingController.PlayClickSound();

        bool on = toggle.isOn;
        wingController.OnBannerClicked(programId, on);
        ApplyVisual(on);
    }

    public void OnPointerEnter()
    {
        wingController.PlayHoverSound();
        banner.color = WithAlpha(baseColor, BANNER_HOVER);
        glow.color = WithAlpha(baseColor, GLOW_HOVER);
    }

    public void OnPointerExit()
    {
        ApplyVisual(toggle.isOn);
    }

    private void ApplyVisual(bool on)
    {
        if(on) {
            banner.color = WithAlpha(baseColor, BANNER_ON);
            glow.color = WithAlpha(baseColor, GLOW_ON);
        } else {
            banner.color = WithAlpha(baseColor, BANNER_OFF);
            glow.color = WithAlpha(baseColor, GLOW_OFF);
        }
    }

    private static Color WithAlpha(Color rgb, float a)
    {
        rgb.a = a;
        return rgb;
    }

    public void Release()
    {
        banner.texture = null;

        if (_ownedTex != null) {
            Destroy((UnityEngine.Object)_ownedTex);
            _ownedTex = null;
        }
    }
}
