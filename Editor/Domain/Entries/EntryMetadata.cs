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
/// 单个 Localization Table Entry 的本地扩展信息。
/// </summary>
namespace EciFlowConnector.Editor.Domain.Entries
{

[Serializable]
public sealed class EntryMetadata
{
    public string TableName;
    public string EntryKey;
    public int CharLimit;
    public string Comment = string.Empty;
    public string ScreenShotId = string.Empty;

    public int SyncedCharLimit;
    public string SyncedComment = string.Empty;
    public bool HasSyncState;

    public bool CharLimitDirty;
    public bool CommentDirty;
    public bool ScreenshotDirty;

    public EntryId Id => new(TableName, EntryKey);
}
}
