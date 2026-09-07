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

/// <summary>
/// 保存 Flow Connector 编辑器窗口运行期间的可变状态。
/// </summary>
namespace EciFlowConnector.Editor.App
{

public sealed class FlowConnectorAppState
{
    public string SearchQuery { get; set; } = string.Empty;
    public string SelectedSourceLocaleCode { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string FlowUrlPrefix { get; set; } = string.Empty;
    public string LastSelectedConfig { get; set; } = string.Empty;
    public string StatusData { get; set; } = string.Empty;
    public string SelectedBranch { get; set; } = string.Empty;
    public int EditingConfigIndex { get; set; } = -1;

    public Dictionary<string, List<string>> SelectedTableData { get; } = new();
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> TableData { get; } = new();
    public Dictionary<string, int> TableHeaderData { get; } = new();
    public Dictionary<string, string> BranchMap { get; } = new();
    public List<ServerConfig> ServerConfigs { get; set; } = new();
}
}
