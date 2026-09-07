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
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// 应用组合根：创建各功能模块，并只在这里建立跨模块协作关系。
/// </summary>
namespace EciFlowConnector.Editor.App
{

internal sealed class FlowConnectorApplication
{
    private readonly FlowConnectorContext context = new();
    private readonly FlowConnectorWindowView windowView;

    internal FlowConnectorApplication()
    {
        context.ImportController = new ImportController(context);
        context.EntryDetailsView = new EntryDetailsView(
            context.Session,
            context.EntryMetadataRepository);
        context.ScreenshotController = new ScreenshotController(
            context.Session,
            context.AppState,
            context.EntryMetadataRepository,
            context.ScreenshotCache,
            context.ApiClient,
            new ScreenshotView());
        context.Highlighter = new HighlighterController(context);
        context.TableBrowser = new TableBrowserView(context);
        context.ImportComparisonView = new ImportComparisonView(context);
        context.SettingsController = new ServerConfigController(context);
        context.ExportController = new ExportController(context);
        context.PullController = new PullController(context);
        context.Session.SelectedEntryChanged += OnSelectedEntryChanged;
        windowView = new FlowConnectorWindowView(context);
    }

    internal void Initialize(EditorWindow hostWindow, VisualElement root)
    {
        context.HostWindow = hostWindow;
        windowView.Build(root);
    }

    internal void Dispose()
    {
        context.Session.SelectedEntryChanged -= OnSelectedEntryChanged;
        context.Highlighter.Dispose();
    }

    private void OnSelectedEntryChanged(EntryId? entryId)
    {
        context.EntryDetailsView.Refresh();
        context.ScreenshotController.RefreshPreview();

        if (entryId.HasValue)
        {
            context.Highlighter.SelectBoundUi(
                entryId.Value.TableName,
                entryId.Value.EntryKey);
        }
        else
        {
            context.Highlighter.Clear();
        }
    }
}
}
