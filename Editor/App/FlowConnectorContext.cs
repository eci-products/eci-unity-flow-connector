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
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// MVC 各对象共享的依赖容器。这里只保存引用和 Model，不实现业务逻辑。
/// </summary>
namespace EciFlowConnector.Editor.App
{

internal sealed class FlowConnectorContext
{
    internal FlowConnectorAppState AppState { get; } = new();
    internal FlowConnectorSession Session { get; } = new();
    internal FlowApiClient ApiClient { get; } = new();
    internal EntryMetadataRepository EntryMetadataRepository { get; } = new();
    internal ScreenshotCache ScreenshotCache { get; } = new();
    internal UnityLocalizationRepository LocalizationRepository { get; } = new();
    internal EditorWindow HostWindow { get; set; }

    internal TableBrowserView TableBrowser { get; set; }
    internal EntryDetailsView EntryDetailsView { get; set; }
    internal ImportComparisonView ImportComparisonView { get; set; }
    internal ExportController ExportController { get; set; }
    internal PullController PullController { get; set; }
    internal ServerConfigController SettingsController { get; set; }
    internal ImportController ImportController { get; set; }
    internal ScreenshotController ScreenshotController { get; set; }
    internal HighlighterController Highlighter { get; set; }

    internal VisualElement Root;
    internal Button TabMain;
    internal Button TabSettings;
    internal VisualElement MainTabContent;
    internal VisualElement SettingsTabContent;
    internal MultiDropdownField TableDropdown;
    internal PopupField<string> BranchDropdown;
    internal VisualElement TableContent;
    internal VisualElement TableHeader;
    internal VisualElement StatusContent;
    internal Button BtnPush;
    internal Button BtnPull;
    internal bool IsLoading;
    internal string BtnPushText = "Push";
    internal string BtnPullText = "Pull";

    internal VisualElement ConfigListContent;
    internal VisualElement ConfigEditorSection;
    internal TextField ConfigNameField;
    internal TextField ConfigApiKeyField;
    internal TextField ConfigHookUrlField;
    internal Button BtnAddConfig;
    internal Button BtnPasteConfig;
    internal Button btnTestConnect;
    internal Button BtnSaveConfig;
    internal Button BtnDeleteConfig;
    internal Button BtnCancelConfig;
    internal PopupField<string> HighlighterModeDropdown;
    internal ColorField HighlighterBorderColorField;
    internal Slider HighlighterBorderThicknessSlider;
    internal ColorField HighlighterArrowColorField;
    internal Slider HighlighterArrowSizeSlider;
    internal VisualElement HighlighterBorderSettings;
    internal VisualElement HighlighterArrowSettings;
    internal VisualElement TableArea;
    internal ScrollView ScrollCol;
    internal VisualElement DetailContent;
    internal List<Toggle> TableToggleGroup { get; } = new();
    internal Toggle TableToggleHeader;
    internal Button BtnDetailClose;
}
}
