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
using System;

/// <summary>
/// 保存一次插件窗口会话中的用户选择，不保存 UI 控件引用。
/// </summary>
namespace EciFlowConnector.Editor.App
{

internal sealed class FlowConnectorSession
{
    internal EntryId? SelectedEntry { get; private set; }

    internal event Action<EntryId?> SelectedEntryChanged;

    internal void SelectEntry(EntryId entryId)
    {
        if (!entryId.IsValid ||
            (SelectedEntry.HasValue && SelectedEntry.Value == entryId))
        {
            return;
        }

        SelectedEntry = entryId;
        SelectedEntryChanged?.Invoke(SelectedEntry);
    }

    internal void ClearSelectedEntry()
    {
        if (!SelectedEntry.HasValue)
            return;

        SelectedEntry = null;
        SelectedEntryChanged?.Invoke(null);
    }
}
}
