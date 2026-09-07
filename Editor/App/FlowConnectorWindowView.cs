using EciFlowConnector.Runtime;
using EciFlowConnector.Editor.App;
using EciFlowConnector.Editor.Domain.Entries;
using EciFlowConnector.Editor.Domain.Versioning;
using EciFlowConnector.Editor.Features.EntryDetails;
using EciFlowConnector.Editor.Features.Export;
using EciFlowConnector.Editor.Features.Highlighter;
using EciFlowConnector.Editor.Features.Import;
using EciFlowConnector.Editor.Features.Screenshots;
using EciFlowConnector.Editor.Features.Settings;
using EciFlowConnector.Editor.Features.TableBrowser;
using EciFlowConnector.Editor.Infrastructure.Api;
using EciFlowConnector.Editor.Infrastructure.Api.Contracts;
using EciFlowConnector.Editor.Infrastructure.Images;
using EciFlowConnector.Editor.Infrastructure.Localization;
using EciFlowConnector.Editor.Infrastructure.Persistence;
using EciFlowConnector.Editor.UI.Components.MultiDropdown;
using EciFlowConnector.Editor.UI.Components.ResizablePanel;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

/// <summary>
/// 主窗口 View：加载资源、创建控件并把用户事件转发给 Controller。
/// </summary>
namespace EciFlowConnector.Editor.App
{

internal sealed class FlowConnectorWindowView : FlowConnectorComponent
{
    private const string PackageRoot = "Packages/net.eciflow.unity";
    private const string UxmlPath = PackageRoot + "/Editor/UI/MainWindow.uxml";

    private static readonly string[] UssPaths =
    {
        PackageRoot + "/Editor/UI/Styles/Base.uss",
        PackageRoot + "/Editor/UI/Styles/TableBrowser.uss",
        PackageRoot + "/Editor/UI/Styles/Actions.uss",
        PackageRoot + "/Editor/UI/Styles/Settings.uss",
        PackageRoot + "/Editor/UI/Styles/Import.uss",
        PackageRoot + "/Editor/UI/Styles/EntryDetails.uss"
    };

    internal FlowConnectorWindowView(FlowConnectorContext context) : base(context) { }

    internal void Build(VisualElement rootElement)
    {
        root = rootElement;
        root.Clear();

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (visualTree == null)
        {
            Debug.LogError("Flow Connector Warning: " + $"Failed to load UXML from: {UxmlPath}");
            return;
        }

        visualTree.CloneTree(root);
        foreach (var path in UssPaths)
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            else
                Debug.LogWarning("Flow Connector Warning: " + $"Failed to load USS from: {path}");
        }

        Context.SettingsController.LoadSavedConfigs();
        BindElements();
        RegisterEvents();
        LoadInitialData();
    }

    private void BindElements()
    {
        tabMain = root.Q<Button>("tabMain");
        tabSettings = root.Q<Button>("tabSettings");
        mainTabContent = root.Q<VisualElement>("mainTabContent");
        settingsTabContent = root.Q<VisualElement>("settingsTabContent");
        detailContent = root.Q<VisualElement>("detailContent");

        tableContent = root.Q<VisualElement>("tableContent");
        tableHeader = root.Q<VisualElement>("tableHeader");
        statusContent = root.Q<VisualElement>("statusContent");
        btnPush = root.Q<Button>("btnPush");
        btnPull = root.Q<Button>("btnPull");

        configListContent = root.Q<VisualElement>("configListContent");
        configEditorSection = root.Q<VisualElement>("configEditorSection");
        configNameField = root.Q<TextField>("configNameField");
        configApiKeyField = root.Q<TextField>("configApiKeyField");
        configHookUrlField = root.Q<TextField>("configHookUrlField");
        btnAddConfig = root.Q<Button>("btnAddConfig");
        btnPasteConfig = root.Q<Button>("btnPasteConifg");
        btnTestConnect = root.Q<Button>("btnTestConnect");
        btnSaveConfig = root.Q<Button>("btnSaveConfig");
        btnDeleteConfig = root.Q<Button>("btnDeleteConfig");
        btnCancelConfig = root.Q<Button>("btnCancelConfig");
        highlighterBorderSettings = root.Q<VisualElement>("highlighterBorderSettings");
        highlighterArrowSettings = root.Q<VisualElement>("highlighterArrowSettings");
        tableArea = root.Q<VisualElement>("tableArea");

        tableDropdown = new MultiDropdownField { Label = "Table Name:" };
        root.Q<VisualElement>("TableSelcet")?.Add(tableDropdown);

        branchDropdown = new PopupField<string>("", new List<string> { "" }, 0)
        {
            name = "batchDropdown"
        };
        branchDropdown.AddToClassList("popup-field");
        root.Q<VisualElement>("batchDropdownContainer")?.Add(branchDropdown);

        CreateHighlighterSettingFields();
        BindTableScroll();
        BindResizablePanels();

        btnDetailClose = root.Q<Button>("btnDetailClose");
        Context.EntryDetailsView.Bind(root);
        Context.ScreenshotController.Bind(root, Context.HostWindow);
        Context.ImportComparisonView.Bind();
        Context.Highlighter.RefreshSettingsVisibility();
    }

    private void CreateHighlighterSettingFields()
    {
        highlighterModeDropdown = new PopupField<string>(
            "", new List<string> { "Border", "Arrow", "None" }, (int)Context.Highlighter.Mode)
        {
            name = "highlighterModeDropdown"
        };
        highlighterModeDropdown.AddToClassList("highlighter-field");
        root.Q<VisualElement>("highlighterModeContainer")?.Add(highlighterModeDropdown);

        highlighterBorderColorField = new ColorField
        {
            name = "highlighterBorderColorField",
            value = Context.Highlighter.BorderColor,
            showAlpha = true
        };
        highlighterBorderColorField.AddToClassList("highlighter-field");
        root.Q<VisualElement>("highlighterBorderColorContainer")?.Add(highlighterBorderColorField);

        highlighterBorderThicknessSlider = new Slider(1f, 20f)
        {
            name = "highlighterBorderThicknessSlider",
            value = Context.Highlighter.BorderThickness,
            showInputField = true
        };
        highlighterBorderThicknessSlider.AddToClassList("highlighter-field");
        root.Q<VisualElement>("highlighterBorderThicknessContainer")?.Add(highlighterBorderThicknessSlider);

        highlighterArrowColorField = new ColorField
        {
            name = "highlighterArrowColorField",
            value = Context.Highlighter.ArrowColor,
            showAlpha = true
        };
        highlighterArrowColorField.AddToClassList("highlighter-field");
        root.Q<VisualElement>("highlighterArrowColorContainer")?.Add(highlighterArrowColorField);

        highlighterArrowSizeSlider = new Slider(12f, 128f)
        {
            name = "highlighterArrowSizeSlider",
            value = Context.Highlighter.ArrowSize,
            showInputField = true
        };
        highlighterArrowSizeSlider.AddToClassList("highlighter-field");
        root.Q<VisualElement>("highlighterArrowSizeContainer")?.Add(highlighterArrowSizeSlider);
        root.Q<Label>("openDoc").RegisterCallback<ClickEvent>(evt =>
        {
            Application.OpenURL("https://eu.eciflow.net/docs/lcms/workspace.html");
        });
    }

    private void BindTableScroll()
    {
        var fixedColumn = root.Q<ScrollView>("fixedScroll");
        scrollCol = root.Q<ScrollView>("tableScrollView");
        if (fixedColumn == null || scrollCol == null)
            return;

        scrollCol.horizontalScroller.valueChanged += value =>
        {
            fixedColumn.horizontalScroller.lowValue = scrollCol.horizontalScroller.lowValue;
            fixedColumn.horizontalScroller.highValue = scrollCol.horizontalScroller.highValue;
            fixedColumn.horizontalScroller.value = value;
        };
    }

    private void BindResizablePanels()
    {
        if (scrollCol == null)
            return;

        var detailResizeHandle = root.Q<VisualElement>("detailResizeHandle");
        if (detailResizeHandle != null && detailContent != null)
        {
            detailResizeHandle.AddManipulator(
                new VerticalResizeManipulator(detailContent, scrollCol));
        }

        var importDiffPanel = root.Q<VisualElement>("importDiffPanel");
        var importResizeHandle = root.Q<VisualElement>("importDiffResizeHandle");
        if (importResizeHandle != null && importDiffPanel != null)
        {
            importResizeHandle.AddManipulator(
                new VerticalResizeManipulator(importDiffPanel, scrollCol, 220f));
        }
    }

    private void RegisterEvents()
    {
        tabMain?.RegisterCallback<ClickEvent>(_ => SwitchTab(true));
        tabSettings?.RegisterCallback<ClickEvent>(_ => SwitchTab(false));
        tableDropdown.OnSelectionChanged += Context.TableBrowser.SelectionChanged;
        branchDropdown?.RegisterValueChangedCallback(Context.SettingsController.BranchChanged);

        btnPush?.RegisterCallback<ClickEvent>(_ => Context.ExportController.Export());
        btnPull?.RegisterCallback<ClickEvent>(_ => Context.PullController.Pull());
        Context.ImportComparisonView.RegisterEvents();
        Context.EntryDetailsView.RegisterEvents();

        btnAddConfig?.RegisterCallback<ClickEvent>(_ => Context.SettingsController.AddConfig());
        btnPasteConfig?.RegisterCallback<ClickEvent>(
            _ => Context.SettingsController.PasteConfigFromClipboard());
        btnTestConnect?.RegisterCallback<ClickEvent>(_ => Context.SettingsController.TestConnectConfig());
        btnSaveConfig?.RegisterCallback<ClickEvent>(_ => Context.SettingsController.SaveCurrentConfig());
        btnDeleteConfig?.RegisterCallback<ClickEvent>(_ => Context.SettingsController.DeleteCurrentConfig());
        btnCancelConfig?.RegisterCallback<ClickEvent>(_ => Context.SettingsController.CancelEditing());

        highlighterModeDropdown?.RegisterValueChangedCallback(Context.Highlighter.ModeChanged);
        highlighterBorderColorField?.RegisterValueChangedCallback(evt =>
        {
            Context.Highlighter.SetBorderColor(evt.newValue);
        });
        highlighterBorderThicknessSlider?.RegisterValueChangedCallback(evt =>
        {
            Context.Highlighter.SetBorderThickness(evt.newValue);
        });
        highlighterArrowColorField?.RegisterValueChangedCallback(evt =>
        {
            Context.Highlighter.SetArrowColor(evt.newValue);
        });
        highlighterArrowSizeSlider?.RegisterValueChangedCallback(evt =>
        {
            Context.Highlighter.SetArrowSize(evt.newValue);
        });
        btnDetailClose?.RegisterCallback<ClickEvent>(_ =>
        {
            detailContent?.AddToClassList("hidden");
            if (detailContent != null)
                detailContent.style.height = StyleKeyword.Null;
            // if (scrollCol != null)
            // {
            //     scrollCol.style.height = StyleKeyword.Auto;
            //     scrollCol.style.maxHeight = StyleKeyword.Auto;
            // }
        });
    }

    private async void LoadInitialData()
    {
        if (!LocalizationSettings.InitializationOperation.IsDone)
            await LocalizationSettings.InitializationOperation.Task;

        Context.TableBrowser.RefreshDropdownOptions();
        await Context.SettingsController.LoadConfigAsync(serverConfigs.FirstOrDefault()?.Name);
    }

    private void SwitchTab(bool showMain)
    {
        mainTabContent?.EnableInClassList("hidden", !showMain);
        settingsTabContent?.EnableInClassList("hidden", showMain);
        tabMain?.EnableInClassList("tab-active", showMain);
        tabSettings?.EnableInClassList("tab-active", !showMain);

        if (!showMain)
            Context.SettingsController.RefreshList();
    }
}
}
