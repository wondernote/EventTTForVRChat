#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using VRC.SDKBase;

[CustomEditor(typeof(EventTTSetup))]
public class EventTTSetupEditor : Editor
{
    private const string TagMasterJsonPath = "Packages/com.wondernote.event-tt-vrchat/Editor/Data/filter_tags_master.json";
    private const string SearchIconPath = "Packages/com.wondernote.event-tt-vrchat/Editor/Images/search_icon.png";

    private const string PC_BASE_URL = "https://wondernote.net/api/event_tt/timetable_pc.wnpk";
    private const string ANDROID_BASE_URL = "https://wondernote.net/api/event_tt/timetable_android.wnpk";

    private const string DEFAULT_TITLE = "イベントカレンダー";
    private const int TITLE_PREFIX_MAX_CHARS = 25;
    private const float TITLE_CHARACTER_SPACING_WIDTH_SCALE = 0.01f;
    private const string TITLE_INFO_TOOLTIP = "自動で入力されますが、手動で変更することもできます。";

    private static readonly Color DefaultRootAccentColor = new Color(0.90f, 0.45f, 0.10f, 1f);
    private static readonly Color PresetRootAccentColor = new Color(0.22f, 0.68f, 0.32f, 1f);

    private TagMasterRoot _tagMaster;
    private bool _loaded;

    private readonly HashSet<string> _expandedRootKeys = new HashSet<string>();
    private string _searchQuery = "";

    private class ToggleRowRefs
    {
        public Toggle Toggle;
        public Label Label;
    }

    private class EditorRootGroup
    {
        public string Key;
        public string Label;
        public Color AccentColor;
        public List<EditorChildGroup> Children = new List<EditorChildGroup>();
    }

    private class EditorChildGroup
    {
        public string Key;
        public string Label;
        public int[] TagIds = new int[0];
        public List<TagMasterTag> Tags = new List<TagMasterTag>();
        public bool ShowCheckbox;
        public bool ShowTags;
    }

    private class EditorTagSearchItem
    {
        public TagMasterTag Tag;
    }

    public override VisualElement CreateInspectorGUI()
    {
        EventTTSetup setup = (EventTTSetup)target;

        EnsureTagMasterLoaded();
        EventTimetable timetable = setup.Timetable;
        ProximityToggle proximityToggle = setup.ProximityToggle;
        GameObject titleSingle = setup.TitleSingle;
        GameObject titleDouble = setup.TitleDouble;
        GameObject prefixArea = setup.PrefixArea;
        UnityEngine.UI.Image prefixRibbon = setup.PrefixRibbon;
        TextMeshProUGUI prefixText = setup.PrefixText;

        VisualElement root = new VisualElement();
        root.style.paddingTop = 2;
        root.style.paddingBottom = 4;

        // // 開発用：内部参照を手動で再設定したい時だけ一時的に表示
        // root.Add(CreateDevelopmentReferenceSection());
        // root.Add(CreateSpacer(8));

        if (timetable == null)
        {
            root.Add(new HelpBox(
                "子オブジェクト内に EventTimetable が見つかりませんでした。Prefab 構造を確認してください。",
                HelpBoxMessageType.Error
            ));
            return root;
        }

        if (proximityToggle == null)
        {
            root.Add(new HelpBox(
                "EventTTSetup の ProximityToggle 参照が未設定です。",
                HelpBoxMessageType.Error
            ));
            return root;
        }

        SphereCollider triggerCollider = proximityToggle.TriggerCollider;
        if (triggerCollider == null)
        {
            root.Add(new HelpBox(
                "ProximityToggle の TriggerCollider 参照が未設定です。",
                HelpBoxMessageType.Error
            ));
            return root;
        }

        if (_tagMaster == null)
        {
            root.Add(new HelpBox(
                "filter_tags_master.json の読み込みに失敗しました。JSON の配置パスと内容を確認してください。",
                HelpBoxMessageType.Error
            ));
            return root;
        }

        HashSet<int> selectedSet = new HashSet<int>(setup.SelectedTagIds ?? Array.Empty<int>());

        string calendarTitle = string.IsNullOrEmpty(setup.CalendarTitle) ? DEFAULT_TITLE : setup.CalendarTitle;
        string calendarTitlePrefix = setup.CalendarTitlePrefix ?? "";
        bool calendarTitlePrefixManual = setup.CalendarTitlePrefixManual;

        float proximityRadius = Mathf.Clamp(triggerCollider.radius, 1f, 9999f);
        bool useSpecificTags = setup.UseSpecificTags;

        List<EditorRootGroup> editorRootGroups = BuildEditorRootGroups();

        HashSet<string> validRootKeys = new HashSet<string>(editorRootGroups.Select(g => g.Key));
        _expandedRootKeys.RemoveWhere(key => !validRootKeys.Contains(key));

        ToggleRowRefs showAllRowRefs;
        ToggleRowRefs showSpecificRowRefs;

        FloatField radiusField = null;
        Label titleFixedLabel = null;
        TextField titlePrefixField = null;
        VisualElement titlePrefixRow = null;
        Button clearSelectionsButton = null;
        VisualElement guideMessageBox = null;
        VisualElement specificSettingsBox = null;
        TextField searchField = null;
        Label searchPlaceholderLabel = null;
        VisualElement searchResultsSection = null;
        VisualElement classificationSection = null;

        Dictionary<string, ToggleRowRefs> childRowRefsByKey = new Dictionary<string, ToggleRowRefs>();
        Dictionary<int, Button> tagButtons = new Dictionary<int, Button>();
        Dictionary<string, VisualElement> rootContentByKey = new Dictionary<string, VisualElement>();
        Dictionary<string, Label> rootArrowLabelByKey = new Dictionary<string, Label>();
        Dictionary<string, Label> rootCountLabelByKey = new Dictionary<string, Label>();

        root.Add(CreateSpacer(8));
        root.Add(CreateSectionLabel("◆ 表示される範囲"));
        root.Add(CreateSpacer(6));

        VisualElement rangeRow = new VisualElement();
        rangeRow.style.flexDirection = FlexDirection.Row;
        rangeRow.style.alignItems = Align.Center;
        rangeRow.style.marginBottom = 4;
        rangeRow.style.marginLeft = 4;

        Label rangeLabel = CreateOptionLabel("半径");
        rangeLabel.style.flexGrow = 0;
        rangeLabel.style.marginRight = 4;
        ApplyOptionLabelStyle(rangeLabel, true);

        radiusField = new FloatField();
        radiusField.isDelayed = true;
        radiusField.value = proximityRadius;
        radiusField.style.width = 48;
        radiusField.style.minWidth = 48;
        radiusField.style.maxWidth = 48;
        radiusField.style.marginLeft = 0;
        radiusField.style.marginRight = 4;

        Label meterLabel = new Label("m");
        meterLabel.style.marginLeft = 0;
        meterLabel.style.color = GetSelectedTextColor();

        rangeRow.Add(rangeLabel);
        rangeRow.Add(radiusField);
        rangeRow.Add(meterLabel);

        root.Add(rangeRow);

        root.Add(CreateSpacer(8));
        root.Add(CreateSectionLabel("◆ 表示したいイベント"));
        root.Add(CreateSpacer(6));

        VisualElement eventModeContainer = new VisualElement();
        eventModeContainer.style.flexDirection = FlexDirection.Column;
        root.Add(eventModeContainer);

        VisualElement showAllRow = CreateToggleRow("すべて", !useSpecificTags, out showAllRowRefs);
        VisualElement showSpecificRow = CreateToggleRow("選ぶ", useSpecificTags, out showSpecificRowRefs);

        eventModeContainer.Add(showAllRow);
        eventModeContainer.Add(showSpecificRow);

        guideMessageBox = CreateGuideMessageBox("下の一覧から、表示したいイベントのキーワード（特徴）を選んでください。");
        guideMessageBox.style.display = useSpecificTags ? DisplayStyle.Flex : DisplayStyle.None;
        root.Add(guideMessageBox);

        specificSettingsBox = CreateSpecificSettingsBox();
        specificSettingsBox.style.display = useSpecificTags ? DisplayStyle.Flex : DisplayStyle.None;
        root.Add(specificSettingsBox);

        VisualElement titleSection = CreateInnerSection();

        VisualElement titleHeaderRow = new VisualElement();
        titleHeaderRow.style.flexDirection = FlexDirection.Row;
        titleHeaderRow.style.alignItems = Align.Center;

        Label titleSectionLabel = CreateSubLabel("表示タイトル");
        Label titleInfoIcon = CreateInfoTooltipIcon(TITLE_INFO_TOOLTIP);

        VisualElement titleHeaderSpacer = new VisualElement();
        titleHeaderSpacer.style.flexGrow = 1;

        clearSelectionsButton = new Button(() =>
        {
            selectedSet.Clear();
            calendarTitlePrefix = "";
            calendarTitlePrefixManual = false;

            RefreshSelectionUi();
            SaveAll();
        });
        clearSelectionsButton.text = "すべて解除";

        titleHeaderRow.Add(titleSectionLabel);
        titleHeaderRow.Add(titleInfoIcon);
        titleHeaderRow.Add(titleHeaderSpacer);
        titleHeaderRow.Add(clearSelectionsButton);

        titleSection.Add(titleHeaderRow);
        titleSection.Add(CreateSpacer(4));

        titleFixedLabel = CreateStaticTitleLabel(DEFAULT_TITLE);
        titleSection.Add(titleFixedLabel);

        titlePrefixRow = new VisualElement();
        titlePrefixRow.style.flexDirection = FlexDirection.Row;
        titlePrefixRow.style.alignItems = Align.Center;

        titlePrefixField = new TextField();
        titlePrefixField.label = string.Empty;
        titlePrefixField.isDelayed = true;
        titlePrefixField.maxLength = TITLE_PREFIX_MAX_CHARS;
        titlePrefixField.style.minWidth = 140;
        titlePrefixField.style.maxWidth = 260;
        titlePrefixField.style.minHeight = 26;
        titlePrefixField.style.flexGrow = 0;
        titlePrefixField.style.flexShrink = 1;
        titlePrefixField.style.marginRight = 8;
        titlePrefixField.style.fontSize = 15;

        Label prefixSuffixLabel = new Label(DEFAULT_TITLE);
        prefixSuffixLabel.style.flexShrink = 0;
        prefixSuffixLabel.style.fontSize = 15;
        prefixSuffixLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        titlePrefixRow.Add(titlePrefixField);
        titlePrefixRow.Add(prefixSuffixLabel);

        titleSection.Add(titlePrefixRow);
        specificSettingsBox.Add(titleSection);

        VisualElement searchSection = CreateInnerSection();
        searchSection.style.marginTop = 10;
        searchSection.style.alignItems = Align.FlexEnd;

        Texture2D searchIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SearchIconPath);

        VisualElement searchRow = new VisualElement();
        searchRow.style.flexDirection = FlexDirection.Row;
        searchRow.style.alignItems = Align.Center;
        searchRow.style.alignSelf = Align.FlexEnd;
        searchRow.style.maxWidth = 260;

        VisualElement searchFieldWrap = new VisualElement();
        searchFieldWrap.style.position = Position.Relative;
        searchFieldWrap.style.flexGrow = 0;
        searchFieldWrap.style.flexShrink = 0;
        searchFieldWrap.style.minWidth = 180;
        searchFieldWrap.style.maxWidth = 220;

        searchField = new TextField();
        searchField.label = string.Empty;
        searchField.value = _searchQuery;
        searchField.style.flexGrow = 0;
        searchField.style.flexShrink = 0;
        searchField.style.minWidth = 180;
        searchField.style.maxWidth = 220;
        searchField.style.minHeight = 26;
        searchField.style.fontSize = 12;
        searchField.maxLength = 15;

        searchPlaceholderLabel = new Label("キーワード検索");
        searchPlaceholderLabel.style.position = Position.Absolute;
        searchPlaceholderLabel.style.left = 6;
        searchPlaceholderLabel.style.right = 24;
        searchPlaceholderLabel.style.top = 5;
        searchPlaceholderLabel.style.fontSize = 12;
        searchPlaceholderLabel.style.color = GetUnselectedTextColor();
        searchPlaceholderLabel.style.opacity = 0.85f;
        searchPlaceholderLabel.style.display = string.IsNullOrEmpty(_searchQuery) ? DisplayStyle.Flex : DisplayStyle.None;
        searchPlaceholderLabel.pickingMode = PickingMode.Ignore;

        searchFieldWrap.Add(searchField);
        searchFieldWrap.Add(searchPlaceholderLabel);

        if (searchIconTexture != null)
        {
            Image searchIconImage = new Image();
            searchIconImage.image = searchIconTexture;
            searchIconImage.scaleMode = ScaleMode.ScaleToFit;
            searchIconImage.style.position = Position.Absolute;
            searchIconImage.style.right = 6;
            searchIconImage.style.top = 6;
            searchIconImage.style.width = 14;
            searchIconImage.style.height = 14;
            searchIconImage.style.opacity = 0.85f;
            searchIconImage.pickingMode = PickingMode.Ignore;

            searchFieldWrap.Add(searchIconImage);
        }

        searchRow.Add(searchFieldWrap);

        searchField.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement searchTextInput = searchField.Q(className: "unity-text-input");
            if (searchTextInput != null)
            {
                searchTextInput.style.paddingRight = 26;
                searchTextInput.style.fontSize = 12;
            }
        });

        searchSection.Add(searchRow);
        specificSettingsBox.Add(searchSection);

        searchResultsSection = CreateInnerSection();
        searchResultsSection.style.marginTop = 6;
        specificSettingsBox.Add(searchResultsSection);

        classificationSection = CreateInnerSection();
        classificationSection.style.marginTop = 6;
        specificSettingsBox.Add(classificationSection);

        foreach (EditorRootGroup rootGroup in editorRootGroups)
        {
            classificationSection.Add(CreateSpacer(8));

            Label countLabel;
            Label arrowLabel;
            VisualElement rootHeader = CreateRootGroupHeader(rootGroup.Label, rootGroup.AccentColor, out countLabel, out arrowLabel);
            rootArrowLabelByKey[rootGroup.Key] = arrowLabel;
            rootCountLabelByKey[rootGroup.Key] = countLabel;

            string currentRootKey = rootGroup.Key;
            rootHeader.RegisterCallback<MouseUpEvent>(_ =>
            {
                if (_expandedRootKeys.Contains(currentRootKey))
                {
                    _expandedRootKeys.Remove(currentRootKey);
                }
                else
                {
                    _expandedRootKeys.Add(currentRootKey);
                }

                RefreshAccordionUi();
            });

            classificationSection.Add(rootHeader);

            VisualElement rootContent = new VisualElement();
            rootContent.style.display = _expandedRootKeys.Contains(rootGroup.Key) ? DisplayStyle.Flex : DisplayStyle.None;
            rootContent.style.flexDirection = FlexDirection.Column;
            rootContentByKey[rootGroup.Key] = rootContent;

            rootContent.Add(CreateSpacer(5));

            foreach (EditorChildGroup childGroup in rootGroup.Children)
            {
                EditorChildGroup currentChild = childGroup;

                bool allSelected = AreAllTagIdsSelected(currentChild.TagIds, selectedSet);

                VisualElement childRow = CreateChildGroupRow(currentChild.Label, currentChild.ShowCheckbox, allSelected, out ToggleRowRefs childRowRefs);
                childRowRefsByKey[currentChild.Key] = childRowRefs;

                if (currentChild.ShowCheckbox && childRowRefs.Toggle != null)
                {
                    childRowRefs.Toggle.RegisterValueChangedCallback(evt =>
                    {
                        SetTagIdSelection(currentChild.TagIds, selectedSet, evt.newValue);
                        calendarTitlePrefixManual = false;

                        RefreshSelectionUi();
                        SaveAll();
                    });
                }

                rootContent.Add(childRow);

                if (currentChild.ShowTags)
                {
                    rootContent.Add(CreateSpacer(7));

                    VisualElement chipsWrap = new VisualElement();
                    chipsWrap.style.flexDirection = FlexDirection.Row;
                    chipsWrap.style.flexWrap = Wrap.Wrap;
                    chipsWrap.style.marginBottom = 6;

                    foreach (TagMasterTag tag in currentChild.Tags)
                    {
                        TagMasterTag currentTag = tag;

                        Button chip = CreateTagChip(currentTag.label, currentTag.id);
                        tagButtons[currentTag.id] = chip;
                        chipsWrap.Add(chip);
                    }

                    rootContent.Add(chipsWrap);
                }
                else
                {
                    rootContent.Add(CreateSpacer(6));
                }
            }

            classificationSection.Add(rootContent);
        }

        radiusField.RegisterValueChangedCallback(evt =>
        {
            proximityRadius = Mathf.Clamp(evt.newValue, 1f, 9999f);
            if (!Mathf.Approximately(radiusField.value, proximityRadius))
            {
                radiusField.SetValueWithoutNotify(proximityRadius);
            }
            SaveAll();
        });

        showAllRowRefs.Toggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                if (!showSpecificRowRefs.Toggle.value)
                {
                    showAllRowRefs.Toggle.SetValueWithoutNotify(true);
                }
                return;
            }

            useSpecificTags = false;
            calendarTitlePrefixManual = false;
            showSpecificRowRefs.Toggle.SetValueWithoutNotify(false);
            RefreshEventModeUi();
            RefreshSelectionUi();
            SaveAll();
        });

        showSpecificRowRefs.Toggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                if (!showAllRowRefs.Toggle.value)
                {
                    showSpecificRowRefs.Toggle.SetValueWithoutNotify(true);
                }
                return;
            }

            useSpecificTags = true;
            calendarTitlePrefixManual = false;
            showAllRowRefs.Toggle.SetValueWithoutNotify(false);
            RefreshEventModeUi();
            RefreshSelectionUi();
            SaveAll();
        });

        titlePrefixField.RegisterValueChangedCallback(evt =>
        {
            calendarTitlePrefix = NormalizeTitlePrefix(evt.newValue);
            titlePrefixField.SetValueWithoutNotify(calendarTitlePrefix);
            calendarTitlePrefixManual = true;
            RefreshTitleUi();
            SaveAll();
        });

        searchField.RegisterValueChangedCallback(evt =>
        {
            _searchQuery = evt.newValue ?? "";
            searchPlaceholderLabel.style.display = string.IsNullOrEmpty(_searchQuery) ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshSearchResultsUi();
        });

        RefreshEventModeUi();
        RefreshAccordionUi();
        RefreshSelectionUi();

        return root;

        Button CreateTagChip(string label, int tagId, Action onClicked = null, bool alignSelfStart = false)
        {
            Button chip = new Button();
            chip.text = label;

            if (alignSelfStart)
            {
                chip.style.alignSelf = Align.FlexStart;
            }

            chip.style.marginRight = 6;
            chip.style.marginBottom = 6;
            chip.style.paddingLeft = 5;
            chip.style.paddingRight = 5;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.height = 20;
            chip.style.fontSize = 12;
            chip.style.color = GetSelectedTextColor();
            chip.style.unityFontStyleAndWeight = FontStyle.Normal;

            chip.RegisterCallback<MouseEnterEvent>(_ =>
            {
                ApplyTagChipStyle(chip, selectedSet.Contains(tagId), true);
            });

            chip.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                ApplyTagChipStyle(chip, selectedSet.Contains(tagId), false);
            });

            chip.clicked += () =>
            {
                if (selectedSet.Contains(tagId))
                {
                    selectedSet.Remove(tagId);
                }
                else
                {
                    selectedSet.Add(tagId);
                }

                onClicked?.Invoke();

                calendarTitlePrefixManual = false;
                RefreshSelectionUi();
                SaveAll();
            };

            ApplyTagChipStyle(chip, selectedSet.Contains(tagId), false);

            return chip;
        }

        void RefreshEventModeUi()
        {
            showAllRowRefs.Toggle.SetValueWithoutNotify(!useSpecificTags);
            showSpecificRowRefs.Toggle.SetValueWithoutNotify(useSpecificTags);

            ApplyOptionLabelStyle(showAllRowRefs.Label, !useSpecificTags);
            ApplyOptionLabelStyle(showSpecificRowRefs.Label, useSpecificTags);

            guideMessageBox.style.display = useSpecificTags ? DisplayStyle.Flex : DisplayStyle.None;
            specificSettingsBox.style.display = useSpecificTags ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void RefreshAccordionUi()
        {
            foreach (EditorRootGroup rootGroup in editorRootGroups)
            {
                bool isExpanded = _expandedRootKeys.Contains(rootGroup.Key);

                if (rootContentByKey.TryGetValue(rootGroup.Key, out VisualElement content))
                {
                    content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (rootArrowLabelByKey.TryGetValue(rootGroup.Key, out Label arrowLabel))
                {
                    arrowLabel.text = isExpanded ? "▼" : "▶";
                }
            }
        }

        void RefreshSearchResultsUi()
        {
            string normalizedQuery = NormalizeSearchQuery(_searchQuery);
            bool isSearching = !string.IsNullOrEmpty(normalizedQuery);

            classificationSection.style.display = isSearching ? DisplayStyle.None : DisplayStyle.Flex;
            searchResultsSection.style.display = isSearching ? DisplayStyle.Flex : DisplayStyle.None;

            searchResultsSection.Clear();

            if (!isSearching)
            {
                return;
            }

            searchResultsSection.Add(CreateSubLabel("検索結果"));
            searchResultsSection.Add(CreateSpacer(4));

            List<EditorTagSearchItem> matches = BuildTagSearchItems(editorRootGroups, normalizedQuery);

            if (matches.Count == 0)
            {
                Label emptyLabel = new Label("一致するキーワードはありません");
                emptyLabel.style.color = GetUnselectedTextColor();
                searchResultsSection.Add(emptyLabel);
                return;
            }

            VisualElement resultsWrap = new VisualElement();
            resultsWrap.style.flexDirection = FlexDirection.Row;
            resultsWrap.style.flexWrap = Wrap.Wrap;
            resultsWrap.style.alignItems = Align.FlexStart;

            foreach (EditorTagSearchItem item in matches)
            {
                Button chip = CreateTagChip(
                    item.Tag.label,
                    item.Tag.id,
                    () =>
                    {
                        _searchQuery = "";
                        searchField.SetValueWithoutNotify("");
                        searchPlaceholderLabel.style.display = DisplayStyle.Flex;
                    },
                    true
                );

                resultsWrap.Add(chip);
            }

            searchResultsSection.Add(resultsWrap);
        }

        void RefreshSelectionUi()
        {
            foreach (EditorRootGroup rootGroup in editorRootGroups)
            {
                foreach (EditorChildGroup childGroup in rootGroup.Children)
                {
                    bool allSelected = AreAllTagIdsSelected(childGroup.TagIds, selectedSet);

                    if (childRowRefsByKey.TryGetValue(childGroup.Key, out ToggleRowRefs childRowRefs))
                    {
                        if (childRowRefs.Toggle != null)
                        {
                            childRowRefs.Toggle.SetValueWithoutNotify(allSelected);
                        }

                        ApplyChildGroupLabelStyle(childRowRefs.Label, allSelected);
                    }

                    foreach (TagMasterTag tag in childGroup.Tags)
                    {
                        if (tagButtons.TryGetValue(tag.id, out Button button))
                        {
                            ApplyTagChipStyle(button, selectedSet.Contains(tag.id), false);
                        }
                    }
                }

                if (rootCountLabelByKey.TryGetValue(rootGroup.Key, out Label countLabel))
                {
                    if (rootGroup.Key == "root:preset")
                    {
                        countLabel.text = "";
                        countLabel.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        int selectedCount = CountSelectedTagIds(rootGroup.Children.SelectMany(c => c.TagIds).Distinct(), selectedSet);
                        countLabel.text = selectedCount > 0 ? selectedCount.ToString() : "";
                        countLabel.style.display = selectedCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }

            RefreshSearchResultsUi();
            RefreshTitleUi();
        }

        void RefreshTitleUi()
        {
            if (selectedSet.Count == 0)
            {
                calendarTitlePrefix = "";
                calendarTitlePrefixManual = false;
            }
            else if (!calendarTitlePrefixManual)
            {
                calendarTitlePrefix = BuildAutoTitlePrefix();
            }

            titleFixedLabel.text = DEFAULT_TITLE;
            titlePrefixField.SetValueWithoutNotify(calendarTitlePrefix);

            bool hasSelection = selectedSet.Count > 0;

            titleFixedLabel.style.display = hasSelection ? DisplayStyle.None : DisplayStyle.Flex;
            titlePrefixRow.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
            clearSelectionsButton.SetEnabled(hasSelection);

            calendarTitle = BuildFinalCalendarTitle();
        }

        void SaveAll()
        {
            if (selectedSet.Count == 0)
            {
                calendarTitlePrefix = "";
                calendarTitlePrefixManual = false;
            }
            else if (!calendarTitlePrefixManual)
            {
                calendarTitlePrefix = BuildAutoTitlePrefix();
            }

            calendarTitle = BuildFinalCalendarTitle();

            string normalizedPrefix = NormalizeTitlePrefix(calendarTitlePrefix);
            bool hasCustomTitle = useSpecificTags && selectedSet.Count > 0 && !string.IsNullOrEmpty(normalizedPrefix);

            int[] sortedIds = selectedSet.OrderBy(id => id).ToArray();
            int[] appliedIds = useSpecificTags ? sortedIds : Array.Empty<int>();

            var undoTargets = new List<UnityEngine.Object> { timetable, setup, triggerCollider };
            undoTargets.Add(titleSingle);
            undoTargets.Add(titleDouble);
            undoTargets.Add(prefixRibbon);
            undoTargets.Add(prefixText);
            Undo.RecordObjects(undoTargets.ToArray(), "Update EventTT setup");

            SetPrivateSerializedField(setup, "calendarTitle", calendarTitle);
            SetPrivateSerializedField(setup, "useSpecificTags", useSpecificTags);
            SetPrivateSerializedField(setup, "selectedTagIds", sortedIds);
            SetPrivateSerializedField(setup, "calendarTitlePrefix", calendarTitlePrefix);
            SetPrivateSerializedField(setup, "calendarTitlePrefixManual", calendarTitlePrefixManual);

            SetPrivateSerializedField(timetable, "presetTagIds", sortedIds);
            SetPrivateSerializedField(timetable, "pcTimetableApiUrl", new VRCUrl(BuildUrl(PC_BASE_URL, appliedIds)));
            SetPrivateSerializedField(timetable, "androidTimetableApiUrl", new VRCUrl(BuildUrl(ANDROID_BASE_URL, appliedIds)));

            titleSingle.SetActive(!hasCustomTitle);
            titleDouble.SetActive(hasCustomTitle);
            EditorUtility.SetDirty(titleSingle);
            EditorUtility.SetDirty(titleDouble);

            float characterSpacing = hasCustomTitle ? GetTitleCharacterSpacing(normalizedPrefix) : 0f;
            prefixText.characterSpacing = characterSpacing;
            prefixText.text = hasCustomTitle ? normalizedPrefix : "";
            prefixText.ForceMeshUpdate();
            EditorUtility.SetDirty(prefixText);

            prefixRibbon.enabled = hasCustomTitle;

            RectTransform ribbonRect = prefixRibbon.rectTransform;
            RectTransform prefixAreaRect = prefixArea.GetComponent<RectTransform>();

            float preferredWidth = 0f;
            if (hasCustomTitle)
            {
                preferredWidth = EstimateTitlePrefixPreferredWidth(normalizedPrefix, prefixText.fontSize);
            }

            float minRibbonWidth = 310f;
            float hardMaxRibbonWidth = 1200f;
            float areaMaxRibbonWidth = prefixAreaRect.rect.width - 24f;
            float maxRibbonWidth = Mathf.Clamp(areaMaxRibbonWidth, minRibbonWidth, hardMaxRibbonWidth);
            float ribbonWidth = Mathf.Clamp(preferredWidth + 120f, minRibbonWidth, maxRibbonWidth);
            ribbonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ribbonWidth);

            EditorUtility.SetDirty(prefixRibbon);

            triggerCollider.radius = Mathf.Max(1f, proximityRadius);
            triggerCollider.enabled = false;
            EditorUtility.SetDirty(triggerCollider);

            EditorUtility.SetDirty(timetable);
            EditorUtility.SetDirty(setup);
        }

        string BuildAutoTitlePrefix()
        {
            List<string> normalizedLabels = BuildNormalizedTitleLabels();

            if (normalizedLabels.Count == 0)
            {
                return "";
            }

            string joined = string.Join("・", normalizedLabels);
            if (joined.Length <= TITLE_PREFIX_MAX_CHARS)
            {
                return joined;
            }

            return normalizedLabels[0] + "など";
        }

        List<string> BuildNormalizedTitleLabels()
        {
            foreach (EditorRootGroup rootGroup in editorRootGroups)
            {
                if (rootGroup.Key != "root:preset") continue;

                foreach (EditorChildGroup childGroup in rootGroup.Children)
                {
                    if (!childGroup.ShowCheckbox) continue;

                    HashSet<int> presetSet = new HashSet<int>(childGroup.TagIds);
                    if (presetSet.SetEquals(selectedSet))
                    {
                        return new List<string> { childGroup.Label };
                    }
                }
            }

            List<string> labels = new List<string>();
            HashSet<int> coveredTagIds = new HashSet<int>();

            foreach (EditorRootGroup rootGroup in editorRootGroups)
            {
                if (rootGroup.Key == "root:preset") continue;

                foreach (EditorChildGroup childGroup in rootGroup.Children)
                {
                    if (!childGroup.ShowCheckbox) continue;
                    if (!AreAllTagIdsSelected(childGroup.TagIds, selectedSet)) continue;

                    labels.Add(childGroup.Label);
                    AddCoveredTagIds(childGroup.TagIds, coveredTagIds);
                }
            }

            foreach (EditorRootGroup rootGroup in editorRootGroups)
            {
                if (rootGroup.Key != "root:preset") continue;

                foreach (EditorChildGroup childGroup in rootGroup.Children)
                {
                    if (!childGroup.ShowCheckbox) continue;
                    if (!AreAllTagIdsSelected(childGroup.TagIds, selectedSet)) continue;

                    bool hasAnyCoveredTag = false;
                    foreach (int tagId in childGroup.TagIds)
                    {
                        if (coveredTagIds.Contains(tagId))
                        {
                            hasAnyCoveredTag = true;
                            break;
                        }
                    }

                    if (hasAnyCoveredTag) continue;

                    labels.Add(childGroup.Label);
                    AddCoveredTagIds(childGroup.TagIds, coveredTagIds);
                }
            }

            foreach (EditorRootGroup rootGroup in editorRootGroups)
            {
                if (rootGroup.Key == "root:preset") continue;

                foreach (EditorChildGroup childGroup in rootGroup.Children)
                {
                    if (!childGroup.ShowTags) continue;

                    foreach (TagMasterTag tag in childGroup.Tags)
                    {
                        if (!selectedSet.Contains(tag.id)) continue;
                        if (coveredTagIds.Contains(tag.id)) continue;

                        labels.Add(tag.label);
                        coveredTagIds.Add(tag.id);
                    }
                }
            }

            return labels;
        }

        void AddCoveredTagIds(IEnumerable<int> tagIds, HashSet<int> coveredTagIds)
        {
            foreach (int tagId in tagIds)
            {
                coveredTagIds.Add(tagId);
            }
        }

        string BuildFinalCalendarTitle()
        {
            if (!useSpecificTags)
            {
                return DEFAULT_TITLE;
            }

            string normalizedPrefix = NormalizeTitlePrefix(calendarTitlePrefix);
            if (selectedSet.Count == 0 || string.IsNullOrEmpty(normalizedPrefix))
            {
                return DEFAULT_TITLE;
            }

            return normalizedPrefix + DEFAULT_TITLE;
        }
    }

    private List<EditorRootGroup> BuildEditorRootGroups()
    {
        List<EditorRootGroup> results = new List<EditorRootGroup>();

        foreach (TagMasterRootGroup rootGroup in _tagMaster.rootGroups.OrderBy(g => g.sortOrder).ThenBy(g => g.id))
        {
            EditorRootGroup editorRoot = new EditorRootGroup
            {
                Key = "root:" + rootGroup.id,
                Label = rootGroup.label,
                AccentColor = DefaultRootAccentColor,
            };

            foreach (TagMasterChildGroup childGroup in rootGroup.children.OrderBy(g => g.sortOrder).ThenBy(g => g.id))
            {
                List<TagMasterTag> sortedTags = childGroup.tags
                    .OrderBy(t => t.sortOrder)
                    .ThenBy(t => t.id)
                    .ToList();

                editorRoot.Children.Add(new EditorChildGroup
                {
                    Key = "child:" + childGroup.id,
                    Label = childGroup.label,
                    TagIds = sortedTags.Select(t => t.id).ToArray(),
                    Tags = sortedTags,
                    ShowCheckbox = ShouldShowChildCheckbox(rootGroup.label, childGroup.label),
                    ShowTags = true,
                });
            }

            results.Add(editorRoot);
        }

        results.Add(BuildPresetRootGroup());

        return results;
    }

    private List<EditorTagSearchItem> BuildTagSearchItems(IEnumerable<EditorRootGroup> rootGroups, string query)
    {
        List<EditorTagSearchItem> results = new List<EditorTagSearchItem>();

        if (string.IsNullOrEmpty(query))
        {
            return results;
        }

        foreach (EditorRootGroup rootGroup in rootGroups)
        {
            if (rootGroup.Key == "root:preset") continue;

            foreach (EditorChildGroup childGroup in rootGroup.Children)
            {
                if (!childGroup.ShowTags) continue;

                foreach (TagMasterTag tag in childGroup.Tags)
                {
                    if (!IsSearchMatch(tag.label, query)) continue;

                    results.Add(new EditorTagSearchItem
                    {
                        Tag = tag,
                    });
                }
            }
        }

        return results;
    }

    private EditorRootGroup BuildPresetRootGroup()
    {
        EditorRootGroup presetRoot = new EditorRootGroup
        {
            Key = "root:preset",
            Label = "プリセット",
            AccentColor = PresetRootAccentColor,
        };

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:music",
            Label = "音楽系",
            TagIds = FindTagIdsByLabels("DJ", "ライブ", "演奏", "オープンマイク", "歌・カラオケ", "音楽"),
            ShowCheckbox = true,
            ShowTags = false,
        });

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:game",
            Label = "ゲーム系",
            TagIds = UnionIds(
                FindChildGroupTagIdsByLabel("遊び系"),
                FindTagIdsByLabels("ゲーム", "eスポーツ")
            ),
            ShowCheckbox = true,
            ShowTags = false,
        });

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:horror",
            Label = "ホラー系",
            TagIds = FindTagIdsByLabels("怪談・心霊", "ホラー"),
            ShowCheckbox = true,
            ShowTags = false,
        });

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:photo",
            Label = "撮影系",
            TagIds = FindTagIdsByLabels("撮影", "カメラ", "写真・映像"),
            ShowCheckbox = true,
            ShowTags = false,
        });

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:sports",
            Label = "スポーツ系",
            TagIds = UnionIds(
                FindTagIdsByLabels("観戦", "スポーツ"),
                FindChildGroupTagIdsByLabel("運動系")
            ),
            ShowCheckbox = true,
            ShowTags = false,
        });

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:academic",
            Label = "学術系",
            TagIds = FindTagIdsByLabels("授業・講義", "発表・LT", "講演会", "科学・学術", "技術・IT", "歴史", "語学"),
            ShowCheckbox = true,
            ShowTags = false,
        });

        presetRoot.Children.Add(new EditorChildGroup
        {
            Key = "preset:comedy",
            Label = "お笑い系",
            TagIds = FindTagIdsByLabels("大喜利", "漫才・コント", "落語・漫談", "お笑い"),
            ShowCheckbox = true,
            ShowTags = false,
        });

        return presetRoot;
    }

    private bool ShouldShowChildCheckbox(string rootLabel, string childLabel)
    {
        if (rootLabel == "雰囲気") return false;
        if (rootLabel == "アバター") return false;
        if (rootLabel == "スタイル") return false;
        if (rootLabel == "テーマ" && childLabel == "その他") return false;

        return true;
    }

    private static bool AreAllTagIdsSelected(IEnumerable<int> tagIds, HashSet<int> selectedSet)
    {
        if (tagIds == null) return false;

        bool hasAny = false;

        foreach (int tagId in tagIds)
        {
            hasAny = true;
            if (!selectedSet.Contains(tagId))
            {
                return false;
            }
        }

        return hasAny;
    }

    private static int CountSelectedTagIds(IEnumerable<int> tagIds, HashSet<int> selectedSet)
    {
        int count = 0;

        foreach (int tagId in tagIds)
        {
            if (selectedSet.Contains(tagId))
            {
                count++;
            }
        }

        return count;
    }

    private static void SetTagIdSelection(IEnumerable<int> tagIds, HashSet<int> selectedSet, bool isSelected)
    {
        foreach (int tagId in tagIds)
        {
            if (isSelected)
            {
                selectedSet.Add(tagId);
            }
            else
            {
                selectedSet.Remove(tagId);
            }
        }
    }

    private int[] FindTagIdsByLabels(params string[] labels)
    {
        HashSet<int> ids = new HashSet<int>();

        foreach (TagMasterRootGroup rootGroup in _tagMaster.rootGroups)
        {
            foreach (TagMasterChildGroup childGroup in rootGroup.children)
            {
                foreach (TagMasterTag tag in childGroup.tags)
                {
                    foreach (string label in labels)
                    {
                        if (tag.label == label)
                        {
                            ids.Add(tag.id);
                            break;
                        }
                    }
                }
            }
        }

        return ids.OrderBy(id => id).ToArray();
    }

    private int[] FindChildGroupTagIdsByLabel(string childGroupLabel)
    {
        foreach (TagMasterRootGroup rootGroup in _tagMaster.rootGroups)
        {
            foreach (TagMasterChildGroup childGroup in rootGroup.children)
            {
                if (childGroup.label == childGroupLabel)
                {
                    return childGroup.tags
                        .Select(t => t.id)
                        .OrderBy(id => id)
                        .ToArray();
                }
            }
        }

        return new int[0];
    }

    private static int[] UnionIds(params int[][] arrays)
    {
        HashSet<int> ids = new HashSet<int>();

        foreach (int[] array in arrays)
        {
            if (array == null) continue;

            foreach (int id in array)
            {
                ids.Add(id);
            }
        }

        return ids.OrderBy(id => id).ToArray();
    }

    private static string NormalizeTitlePrefix(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

        if (normalized.Length > TITLE_PREFIX_MAX_CHARS)
        {
            normalized = normalized.Substring(0, TITLE_PREFIX_MAX_CHARS);
        }

        return normalized;
    }

    private static float GetTitleCharacterSpacing(string value)
    {
        int length = string.IsNullOrEmpty(value) ? 0 : value.Length;

        if (length <= 2) return 70f;
        if (length == 3) return 25f;
        if (length == 4) return 10f;
        if (length <= 20) return 0f;
        if (length <= 25) return -3f;

        return -3f;
    }

    private static float EstimateTitlePrefixPreferredWidth(string value, float fontSize)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        float width = 0f;

        foreach (char c in value)
        {
            width += EstimateTitleCharacterWidth(c, fontSize);
        }

        float characterSpacing = GetTitleCharacterSpacing(value);
        width += Mathf.Max(0, value.Length - 1) * characterSpacing * fontSize * TITLE_CHARACTER_SPACING_WIDTH_SCALE;

        return width * 1.10f;
    }

    private static float EstimateTitleCharacterWidth(char c, float fontSize)
    {
        if (c == ' ')
        {
            return fontSize * 0.30f;
        }

        if (c >= '\uFF61' && c <= '\uFF9F')
        {
            return fontSize * 0.55f;
        }

        if (c >= '\u0021' && c <= '\u007E')
        {
            return fontSize * GetAsciiTitleWidthFactor(c);
        }

        return fontSize;
    }

    private static float GetAsciiTitleWidthFactor(char c)
    {
        if ("ijlI!.,:;|'`".IndexOf(c) >= 0)
        {
            return 0.28f;
        }

        if ("frt()[]{}\"".IndexOf(c) >= 0)
        {
            return 0.38f;
        }

        if ("mwMW@%&".IndexOf(c) >= 0)
        {
            return 0.88f;
        }

        if (char.IsDigit(c))
        {
            return 0.56f;
        }

        if (char.IsUpper(c))
        {
            return 0.64f;
        }

        if (char.IsLower(c))
        {
            return 0.56f;
        }

        return 0.42f;
    }

    private static string NormalizeSearchQuery(string value)
    {
        return NormalizeKanaForSearch(string.IsNullOrWhiteSpace(value) ? "" : value.Trim());
    }

    private static bool IsSearchMatch(string source, string query)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query))
        {
            return false;
        }

        string normalizedSource = NormalizeKanaForSearch(source);
        string normalizedQuery = NormalizeKanaForSearch(query);

        return normalizedSource.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeKanaForSearch(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string normalized = value.Normalize(NormalizationForm.FormKC);
        char[] chars = normalized.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];

            if (c >= '\u3041' && c <= '\u3096')
            {
                chars[i] = (char)(c + 0x60);
            }
        }

        return new string(chars);
    }

    private Label CreateSectionLabel(string text)
    {
        Label label = new Label(text);
        label.style.fontSize = 13;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private Label CreateSubLabel(string text)
    {
        Label label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private Label CreateInfoTooltipIcon(string tooltip)
    {
        Label icon = new Label("i");
        icon.tooltip = tooltip;
        icon.pickingMode = PickingMode.Position;
        icon.style.width = 14;
        icon.style.height = 14;
        icon.style.marginLeft = 3;
        icon.style.unityTextAlign = TextAnchor.MiddleCenter;
        icon.style.fontSize = 9;
        icon.style.unityFontStyleAndWeight = FontStyle.Bold;
        icon.style.color = GetInfoIconTextColor();
        icon.style.backgroundColor = GetInfoIconBackgroundColor();
        icon.style.borderTopWidth = 1;
        icon.style.borderBottomWidth = 1;
        icon.style.borderLeftWidth = 1;
        icon.style.borderRightWidth = 1;
        icon.style.borderTopColor = GetInfoIconBorderColor();
        icon.style.borderBottomColor = GetInfoIconBorderColor();
        icon.style.borderLeftColor = GetInfoIconBorderColor();
        icon.style.borderRightColor = GetInfoIconBorderColor();

        icon.style.borderTopLeftRadius = 7;
        icon.style.borderTopRightRadius = 7;
        icon.style.borderBottomLeftRadius = 7;
        icon.style.borderBottomRightRadius = 7;

        return icon;
    }

    private VisualElement CreateSpacer(float height)
    {
        VisualElement spacer = new VisualElement();
        spacer.style.height = height;
        return spacer;
    }

    private VisualElement CreateDevelopmentReferenceSection()
    {
        Foldout foldout = new Foldout();
        foldout.text = "（開発用）内部参照";
        foldout.value = false;

        VisualElement box = CreateSpecificSettingsBox();
        box.style.marginTop = 6;

        box.Add(new PropertyField(serializedObject.FindProperty("timetable"), "EventTimetable"));
        box.Add(new PropertyField(serializedObject.FindProperty("proximityToggle"), "ProximityToggle"));
        box.Add(new PropertyField(serializedObject.FindProperty("titleSingle"), "TitleSingle"));
        box.Add(new PropertyField(serializedObject.FindProperty("titleDouble"), "TitleDouble"));
        box.Add(new PropertyField(serializedObject.FindProperty("prefixArea"), "PrefixArea"));
        box.Add(new PropertyField(serializedObject.FindProperty("prefixRibbon"), "PrefixRibbon"));
        box.Add(new PropertyField(serializedObject.FindProperty("prefixText"), "PrefixText"));

        foldout.Add(box);
        return foldout;
    }

    private VisualElement CreateSpecificSettingsBox()
    {
        VisualElement box = new VisualElement();
        box.style.marginTop = 10;
        box.style.marginLeft = 8;
        box.style.marginRight = 8;
        box.style.paddingTop = 6;
        box.style.paddingBottom = 6;
        box.style.paddingLeft = 8;
        box.style.paddingRight = 8;
        box.style.borderTopWidth = 1;
        box.style.borderBottomWidth = 1;
        box.style.borderLeftWidth = 1;
        box.style.borderRightWidth = 1;
        box.style.borderTopColor = GetBoxBorderColor();
        box.style.borderBottomColor = GetBoxBorderColor();
        box.style.borderLeftColor = GetBoxBorderColor();
        box.style.borderRightColor = GetBoxBorderColor();
        box.style.backgroundColor = GetBoxBackgroundColor();
        return box;
    }

    private VisualElement CreateGuideMessageBox(string text)
    {
        VisualElement box = new VisualElement();
        box.style.marginTop = 8;
        box.style.marginLeft = 8;
        box.style.marginRight = 8;
        box.style.paddingTop = 6;
        box.style.paddingBottom = 6;
        box.style.paddingLeft = 8;
        box.style.paddingRight = 8;
        box.style.backgroundColor = GetGuideMessageBackgroundColor();

        box.style.borderLeftWidth = 3;
        box.style.borderLeftColor = GetGuideMessageAccentColor();

        Label label = new Label(text);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.fontSize = 12;
        label.style.color = GetSelectedTextColor();

        box.Add(label);
        return box;
    }

    private static Color GetGuideMessageBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.03f)
            : new Color(0f, 0f, 0f, 0.025f);
    }

    private static Color GetGuideMessageAccentColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.14f)
            : new Color(0f, 0f, 0f, 0.14f);
    }

    private VisualElement CreateInnerSection()
    {
        VisualElement section = new VisualElement();
        section.style.flexDirection = FlexDirection.Column;
        return section;
    }

    private VisualElement CreateToggleRow(string text, bool initialValue, out ToggleRowRefs rowRefs)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 4;

        Toggle toggle = CreateBareToggle(initialValue);

        Label label = CreateOptionLabel(text);
        ApplyOptionLabelStyle(label, initialValue);

        row.Add(toggle);
        row.Add(label);

        rowRefs = new ToggleRowRefs
        {
            Toggle = toggle,
            Label = label,
        };

        return row;
    }

    private VisualElement CreateChildGroupRow(string text, bool showCheckbox, bool initialValue, out ToggleRowRefs rowRefs)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 0;

        if (showCheckbox)
        {
            Toggle toggle = CreateBareToggle(initialValue);
            toggle.style.marginRight = 4;
            row.Add(toggle);

            VisualElement labelWrap = CreateChildGroupLabelWrap(text, out Label label);
            row.Add(labelWrap);

            rowRefs = new ToggleRowRefs
            {
                Toggle = toggle,
                Label = label,
            };

            ApplyChildGroupLabelStyle(label, initialValue);
        }
        else
        {
            VisualElement labelWrap = CreateChildGroupLabelWrap(text, out Label label);
            row.Add(labelWrap);

            rowRefs = new ToggleRowRefs
            {
                Toggle = null,
                Label = label,
            };

            ApplyChildGroupLabelStyle(label, initialValue);
        }

        return row;
    }

    private VisualElement CreateChildGroupLabelWrap(string text, out Label label)
    {
        label = new Label(text);
        label.style.fontSize = 13;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.flexGrow = 0;
        label.style.paddingBottom = 0;
        label.style.marginBottom = 0;

        VisualElement labelWrap = new VisualElement();
        labelWrap.style.flexDirection = FlexDirection.Column;
        labelWrap.style.flexGrow = 0;

        VisualElement underline = new VisualElement();
        underline.style.height = 1;
        underline.style.marginTop = 0;
        underline.style.backgroundColor = new Color(1f, 1f, 1f, 0.5f);

        labelWrap.Add(label);
        labelWrap.Add(underline);

        return labelWrap;
    }

    private VisualElement CreateRootGroupHeader(string labelText, Color accentColor, out Label countLabel, out Label arrowLabel)
    {
        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.height = 26;
        header.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

        VisualElement accent = new VisualElement();
        accent.style.width = 4;
        accent.style.height = 19;
        accent.style.marginRight = 6;
        accent.style.backgroundColor = accentColor;

        Label label = new Label(labelText);
        label.style.fontSize = 16;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginLeft = 4;
        label.style.flexGrow = 0;

        countLabel = new Label("");
        countLabel.style.marginLeft = 6;
        countLabel.style.paddingLeft = 4;
        countLabel.style.paddingRight = 4;
        countLabel.style.height = 16;
        countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        countLabel.style.fontSize = 11;
        countLabel.style.color = GetSelectedTextColor();
        countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        countLabel.style.backgroundColor = GetCountBadgeBackgroundColor();
        countLabel.style.borderTopWidth = 0;
        countLabel.style.borderBottomWidth = 0;
        countLabel.style.borderLeftWidth = 0;
        countLabel.style.borderRightWidth = 0;
        countLabel.style.display = DisplayStyle.None;

        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1;

        arrowLabel = new Label("▶");
        arrowLabel.style.marginRight = 8;
        arrowLabel.style.color = GetSelectedTextColor();
        arrowLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        header.Add(accent);
        header.Add(label);
        header.Add(countLabel);
        header.Add(spacer);
        header.Add(arrowLabel);

        return header;
    }

    private Toggle CreateBareToggle(bool value)
    {
        Toggle toggle = new Toggle();
        toggle.label = string.Empty;
        toggle.value = value;
        toggle.style.marginRight = 2;
        toggle.style.marginTop = 0;
        toggle.style.marginBottom = 0;
        toggle.style.flexShrink = 0;
        return toggle;
    }

    private Label CreateOptionLabel(string text)
    {
        Label label = new Label(text);
        label.style.flexGrow = 1;
        label.style.marginTop = 0;
        label.style.marginBottom = 0;
        return label;
    }

    private Label CreateStaticTitleLabel(string text)
    {
        Label label = new Label(text);
        label.style.color = GetSelectedTextColor();
        label.style.fontSize = 15;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginLeft = 2;
        label.style.marginTop = 3;
        label.style.marginBottom = 3;
        label.text = text;
        return label;
    }

    private void ApplyOptionLabelStyle(Label label, bool isSelected)
    {
        if (label == null) return;

        label.style.color = isSelected ? GetSelectedTextColor() : GetUnselectedTextColor();
        label.style.unityFontStyleAndWeight = isSelected ? FontStyle.Bold : FontStyle.Normal;
    }

    private void ApplyChildGroupLabelStyle(Label label, bool isSelected)
    {
        if (label == null) return;

        label.style.color = isSelected ? GetSelectedTextColor() : GetChildGroupTextColor();
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 13;
    }

    private void ApplyTagChipStyle(Button button, bool isSelected, bool isHovered = false)
    {
        if (button == null) return;

        button.style.borderTopWidth = 1;
        button.style.borderBottomWidth = 1;
        button.style.borderLeftWidth = 1;
        button.style.borderRightWidth = 1;
        button.style.color = GetSelectedTextColor();
        button.style.unityFontStyleAndWeight = FontStyle.Normal;

        if (isSelected)
        {
            button.style.backgroundColor = isHovered
                ? new Color(0.90f, 0.45f, 0.10f, 0.38f)
                : new Color(0.90f, 0.45f, 0.10f, 0.28f);

            Color selectedBorder = new Color(0.90f, 0.45f, 0.10f, 0.45f);
            button.style.borderTopColor = selectedBorder;
            button.style.borderBottomColor = selectedBorder;
            button.style.borderLeftColor = selectedBorder;
            button.style.borderRightColor = selectedBorder;
        }
        else
        {
            button.style.backgroundColor = isHovered
                ? GetChipHoverBackgroundColor()
                : GetChipBackgroundColor();

            Color subtleBorder = GetSubtleChipBorderColor();
            button.style.borderTopColor = subtleBorder;
            button.style.borderBottomColor = subtleBorder;
            button.style.borderLeftColor = subtleBorder;
            button.style.borderRightColor = subtleBorder;
        }
    }

    private void EnsureTagMasterLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TagMasterJsonPath);
        if (jsonAsset == null)
        {
            Debug.LogError($"Tag master JSON が見つかりません: {TagMasterJsonPath}");
            return;
        }

        try
        {
            _tagMaster = JsonUtility.FromJson<TagMasterRoot>(jsonAsset.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"Tag master JSON のパースに失敗しました: {e.Message}");
        }
    }

    private static int[] GetPrivateIntArray(object target, string fieldName)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null)
        {
            Debug.LogError($"Field not found: {fieldName}");
            return Array.Empty<int>();
        }

        object value = fi.GetValue(target);
        return value as int[] ?? Array.Empty<int>();
    }

    private static string GetPrivateString(object target, string fieldName, string defaultValue)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null)
        {
            Debug.LogError($"Field not found: {fieldName}");
            return defaultValue;
        }

        object value = fi.GetValue(target);
        return value as string ?? defaultValue;
    }

    private static int GetPrivateInt(object target, string fieldName, int defaultValue)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null)
        {
            Debug.LogError($"Field not found: {fieldName}");
            return defaultValue;
        }

        object value = fi.GetValue(target);
        return value is int intValue ? intValue : defaultValue;
    }

    private static float GetPrivateFloat(object target, string fieldName, float defaultValue)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null)
        {
            Debug.LogError($"Field not found: {fieldName}");
            return defaultValue;
        }

        object value = fi.GetValue(target);
        return value is float floatValue ? floatValue : defaultValue;
    }

    private static bool GetPrivateBool(object target, string fieldName, bool defaultValue)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null)
        {
            Debug.LogError($"Field not found: {fieldName}");
            return defaultValue;
        }

        object value = fi.GetValue(target);
        return value is bool boolValue ? boolValue : defaultValue;
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

    private static string BuildUrl(string baseUrl, IEnumerable<int> tagIds)
    {
        int[] ids = tagIds
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (ids.Length == 0)
        {
            return baseUrl;
        }

        return $"{baseUrl}?tag_ids={string.Join(",", ids)}";
    }

    private static Color GetSelectedTextColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.90f, 0.90f, 0.90f, 1f)
            : new Color(0.10f, 0.10f, 0.10f, 1f);
    }

    private static Color GetUnselectedTextColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.60f, 0.60f, 0.60f, 1f)
            : new Color(0.45f, 0.45f, 0.45f, 1f);
    }

    private static Color GetChildGroupTextColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.76f, 0.76f, 0.76f, 1f)
            : new Color(0.20f, 0.20f, 0.20f, 1f);
    }

    private static Color GetBoxBorderColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.28f, 0.28f, 0.28f, 1f)
            : new Color(0.72f, 0.72f, 0.72f, 1f);
    }

    private static Color GetBoxBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.03f)
            : new Color(0f, 0f, 0f, 0.02f);
    }

    private static Color GetFieldBorderColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.25f, 0.25f, 0.25f, 1f)
            : new Color(0.70f, 0.70f, 0.70f, 1f);
    }

    private static Color GetFieldBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.18f, 1f)
            : new Color(1f, 1f, 1f, 1f);
    }

    private static Color GetChipBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.36f, 0.36f, 0.36f, 1f)
            : new Color(0.90f, 0.90f, 0.90f, 1f);
    }

    private static Color GetChipHoverBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.44f, 0.44f, 0.44f, 1f)
            : new Color(0.84f, 0.84f, 0.84f, 1f);
    }

    private static Color GetSubtleChipBorderColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.10f)
            : new Color(0f, 0f, 0f, 0.10f);
    }

    private static Color GetCountBadgeBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.32f, 0.32f, 0.32f, 1f)
            : new Color(0.90f, 0.90f, 0.90f, 1f);
    }

    private static Color GetInfoIconTextColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.82f, 0.82f, 0.82f, 1f)
            : new Color(0.25f, 0.25f, 0.25f, 1f);
    }

    private static Color GetInfoIconBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.04f)
            : new Color(0f, 0f, 0f, 0.03f);
    }

    private static Color GetInfoIconBorderColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.10f)
            : new Color(0f, 0f, 0f, 0.10f);
    }
}
#endif