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
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 为独立 MVC 对象提供共享 Context 的简写访问，不保存第二份状态。
/// </summary>
namespace EciFlowConnector.Editor.App
{

internal abstract class FlowConnectorComponent
{
    protected const string CONFIGS_PREF_KEY = "ECIFlowConnector_ServerConfigs";
    protected const string SELECTED_CONFIG_PREF_KEY = "ECIFlowConnector_SelectedConfig";
    protected FlowConnectorComponent(FlowConnectorContext context)
    {
        Context = context;
    }

    protected FlowConnectorContext Context { get; }

    protected EntryId? selectedEntry => Context.Session.SelectedEntry;
    protected string selectedEntryTableName => selectedEntry?.TableName ?? string.Empty;
    protected string selectedEntryKey => selectedEntry?.EntryKey ?? string.Empty;
    protected string searchQuery { get => Context.AppState.SearchQuery; set => Context.AppState.SearchQuery = value; }
    protected string selectedSourceLocaleCode { get => Context.AppState.SelectedSourceLocaleCode; set => Context.AppState.SelectedSourceLocaleCode = value; }
    protected string apiKey { get => Context.AppState.ApiKey; set => Context.AppState.ApiKey = value; }
    protected string flowUrlPrefix { get => Context.AppState.FlowUrlPrefix; set => Context.AppState.FlowUrlPrefix = value; }
    protected string lastSelectConfig { get => Context.AppState.LastSelectedConfig; set => Context.AppState.LastSelectedConfig = value; }
    protected string statusDataStr { get => Context.AppState.StatusData; set => Context.AppState.StatusData = value; }
    protected string selectedBranch { get => Context.AppState.SelectedBranch; set => Context.AppState.SelectedBranch = value; }
    protected Dictionary<string, List<string>> selectedTableData => Context.AppState.SelectedTableData;
    protected Dictionary<string, Dictionary<string, Dictionary<string, string>>> tableData => Context.AppState.TableData;
    protected Dictionary<string, int> tableHeaderData => Context.AppState.TableHeaderData;
    protected Dictionary<string, string> branchMap => Context.AppState.BranchMap;
    protected List<ServerConfig> serverConfigs { get => Context.AppState.ServerConfigs; set => Context.AppState.ServerConfigs = value; }
    protected int editingConfigIndex { get => Context.AppState.EditingConfigIndex; set => Context.AppState.EditingConfigIndex = value; }

    protected string CMS_URL_PATH => "/api-service/content-management-service/";
    protected string PUSH_METHOD => "api-key-auth/unity/push";
    protected string PULL_METHOD => "api-key-auth/unity/pull";
    protected string INIT_CONNECT_METHOD => "api-key-auth/unity/initConnect";

    protected VisualElement root { get => Context.Root; set => Context.Root = value; }
    protected Button tabMain { get => Context.TabMain; set => Context.TabMain = value; }
    protected Button tabSettings { get => Context.TabSettings; set => Context.TabSettings = value; }
    protected VisualElement mainTabContent { get => Context.MainTabContent; set => Context.MainTabContent = value; }
    protected VisualElement settingsTabContent { get => Context.SettingsTabContent; set => Context.SettingsTabContent = value; }
    protected MultiDropdownField tableDropdown { get => Context.TableDropdown; set => Context.TableDropdown = value; }
    protected PopupField<string> branchDropdown { get => Context.BranchDropdown; set => Context.BranchDropdown = value; }
    protected VisualElement tableContent { get => Context.TableContent; set => Context.TableContent = value; }
    protected VisualElement tableHeader { get => Context.TableHeader; set => Context.TableHeader = value; }
    protected VisualElement statusContent { get => Context.StatusContent; set => Context.StatusContent = value; }
    protected Button btnPush { get => Context.BtnPush; set => Context.BtnPush = value; }
    protected Button btnPull { get => Context.BtnPull; set => Context.BtnPull = value; }
    protected bool isLoading { get => Context.IsLoading; set => Context.IsLoading = value; }
    protected string btnPushText => Context.BtnPushText;
    protected string btnPullText => Context.BtnPullText;

    protected VisualElement configListContent { get => Context.ConfigListContent; set => Context.ConfigListContent = value; }
    protected VisualElement configEditorSection { get => Context.ConfigEditorSection; set => Context.ConfigEditorSection = value; }
    protected TextField configNameField { get => Context.ConfigNameField; set => Context.ConfigNameField = value; }
    protected TextField configApiKeyField { get => Context.ConfigApiKeyField; set => Context.ConfigApiKeyField = value; }
    protected TextField configHookUrlField { get => Context.ConfigHookUrlField; set => Context.ConfigHookUrlField = value; }
    protected Button btnAddConfig { get => Context.BtnAddConfig; set => Context.BtnAddConfig = value; }
    protected Button btnPasteConfig { get => Context.BtnPasteConfig; set => Context.BtnPasteConfig = value; }
    protected Button btnSaveConfig { get => Context.BtnSaveConfig; set => Context.BtnSaveConfig = value; }
    protected Button btnTestConnect { get => Context.btnTestConnect; set => Context.btnTestConnect = value; }
    protected Button btnDeleteConfig { get => Context.BtnDeleteConfig; set => Context.BtnDeleteConfig = value; }
    protected Button btnCancelConfig { get => Context.BtnCancelConfig; set => Context.BtnCancelConfig = value; }
    protected PopupField<string> highlighterModeDropdown { get => Context.HighlighterModeDropdown; set => Context.HighlighterModeDropdown = value; }
    protected ColorField highlighterBorderColorField { get => Context.HighlighterBorderColorField; set => Context.HighlighterBorderColorField = value; }
    protected Slider highlighterBorderThicknessSlider { get => Context.HighlighterBorderThicknessSlider; set => Context.HighlighterBorderThicknessSlider = value; }
    protected ColorField highlighterArrowColorField { get => Context.HighlighterArrowColorField; set => Context.HighlighterArrowColorField = value; }
    protected Slider highlighterArrowSizeSlider { get => Context.HighlighterArrowSizeSlider; set => Context.HighlighterArrowSizeSlider = value; }
    protected VisualElement highlighterBorderSettings { get => Context.HighlighterBorderSettings; set => Context.HighlighterBorderSettings = value; }
    protected VisualElement highlighterArrowSettings { get => Context.HighlighterArrowSettings; set => Context.HighlighterArrowSettings = value; }
    protected VisualElement tableArea { get => Context.TableArea; set => Context.TableArea = value; }
    protected ScrollView scrollCol { get => Context.ScrollCol; set => Context.ScrollCol = value; }
    protected VisualElement detailContent { get => Context.DetailContent; set => Context.DetailContent = value; }
    protected List<Toggle> tableToggleGroup => Context.TableToggleGroup;
    protected Toggle tableToggleHeader { get => Context.TableToggleHeader; set => Context.TableToggleHeader = value; }
    protected Button btnDetailClose { get => Context.BtnDetailClose; set => Context.BtnDetailClose = value; }

    protected Task<string> PostAsync<T>(string url, T data) => Context.ApiClient.PostAsync(url, data, apiKey);
    protected Task<string> PostAsyncWithToken<T>(string url, T data, string apiKey) => Context.ApiClient.PostAsync(url, data, apiKey);
    protected Task<byte[]> GetBytesAsync(string url) => Context.ApiClient.GetBytesAsync(url);
    protected EditorWindow GetWindow(System.Type type) => EditorWindow.GetWindow(type);
    protected void Focus() => Context.HostWindow?.Focus();
    protected void ShowNotification(GUIContent content) => Context.HostWindow?.ShowNotification(content);
    protected void DestroyImmediate(Object target) => Object.DestroyImmediate(target);

}
}
