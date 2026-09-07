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
/// 在插件内部唯一标识一个 Localization Table Entry。
/// </summary>
namespace EciFlowConnector.Editor.Domain.Entries
{

public readonly struct EntryId : IEquatable<EntryId>
{
    public EntryId(string tableName, string entryKey)
    {
        TableName = tableName ?? string.Empty;
        EntryKey = entryKey ?? string.Empty;
    }

    public string TableName { get; }
    public string EntryKey { get; }
    public bool IsValid =>
        !string.IsNullOrEmpty(TableName) && !string.IsNullOrEmpty(EntryKey);

    public bool Equals(EntryId other)
    {
        return string.Equals(TableName, other.TableName, StringComparison.Ordinal) &&
               string.Equals(EntryKey, other.EntryKey, StringComparison.Ordinal);
    }

    public override bool Equals(object obj) => obj is EntryId other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((TableName != null ? TableName.GetHashCode() : 0) * 397) ^
                   (EntryKey != null ? EntryKey.GetHashCode() : 0);
        }
    }

    public override string ToString() => $"{TableName}/{EntryKey}";

    public static bool operator ==(EntryId left, EntryId right) => left.Equals(right);
    public static bool operator !=(EntryId left, EntryId right) => !left.Equals(right);
}
}
