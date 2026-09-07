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
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 处理 ContentComparisonState 对应的数据或操作。
/// </summary>
namespace EciFlowConnector.Editor.Domain.Versioning
{

public enum ContentComparisonState
{
    Unchanged,
    LocalOnly,
    RemoteOnly,
    Changed,
    Conflict
}

/// <summary>
/// 处理 VersionComparisonState 对应的数据或操作。
/// </summary>
public enum VersionComparisonState
{
    Untracked,
    Same,
    LocalOnly,
    RemoteOnly,
    Different
}

/// <summary>
/// 处理 ValueResolution 对应的数据或操作。
/// </summary>
public enum ValueResolution
{
    Unresolved,
    KeepLocal,
    UseRemote
    // Custom
}

/// <summary>
/// 处理 VersionResolution 对应的数据或操作。
/// </summary>
public enum VersionResolution
{
    Unresolved,
    KeepLocal,
    UseRemote
    // Custom
}

/// <summary>
/// 处理 EntrySnapshot 对应的数据或操作。
/// </summary>
public sealed class EntrySnapshot
{
    public string TableName { get; set; }
    public string EntryKey { get; set; }
    public string VersionId { get; set; }
    public Dictionary<string, string> Values { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 处理 CellComparison 对应的数据或操作。
/// </summary>
public sealed class CellComparison
{
    public string LocaleCode { get; internal set; }
    public bool LocalExists { get; internal set; }
    public string LocalValue { get; internal set; }
    public bool RemoteExists { get; internal set; }
    public string RemoteValue { get; internal set; }
    public ContentComparisonState State { get; internal set; }
    public ValueResolution Resolution { get; internal set; } =
        ValueResolution.Unresolved;
    public string CustomValue { get; internal set; }
    public bool FinalExists
    {
        get
        {
            switch (Resolution)
            {
                case ValueResolution.UseRemote:
                    return RemoteExists;
                //Custom预留
                // case ValueResolution.Custom:
                //     return true;
                default:
                    return LocalExists;
            }
        }
    }

    public string FinalValue
    {
        get
        {
            switch (Resolution)
            {
                case ValueResolution.UseRemote:
                    return RemoteValue;
                //Custom预留
                // case ValueResolution.Custom:
                //     return CustomValue;
                default:
                    return LocalValue;
            }
        }
    }
}

/// <summary>
/// 处理 EntryComparison 对应的数据或操作。
/// </summary>
public sealed class EntryComparison
{
    private readonly List<CellComparison> cells =
        new List<CellComparison>();

    public string TableName { get; internal set; }
    public string EntryKey { get; internal set; }
    public string LocalVersionId { get; internal set; }
    public string RemoteVersionId { get; internal set; }
    public VersionComparisonState VersionState { get; internal set; }
    public VersionResolution VersionResolution { get; internal set; } =
        VersionResolution.Unresolved;
    public string CustomVersionId { get; internal set; }
    public IReadOnlyList<CellComparison> Cells => cells;
    public string FinalVersionId
    {
        get
        {
            switch (VersionResolution)
            {
                case VersionResolution.UseRemote:
                    return RemoteVersionId;
                //Custom预留
                // case VersionResolution.Custom:
                //     return CustomVersionId;
                default:
                    return LocalVersionId;
            }
        }
    }

    internal List<CellComparison> MutableCells => cells;
}

/// <summary>
/// 处理 ImportComparison 对应的数据或操作。
/// </summary>
public sealed class ImportComparison
{
    private readonly List<EntryComparison> entries =
        new List<EntryComparison>();

    public string Scope { get; internal set; }
    public string BranchId { get; internal set; }
    public DateTime CreatedAtUtc { get; internal set; }
    public bool IsApplied { get; internal set; }
    public IReadOnlyList<EntryComparison> Entries => entries;
    public int EntryCount => entries.Count;
    public int CellCount => entries.Sum(entry => entry.Cells.Count);
    public int ChangedCellCount => entries.Sum(
        entry => entry.Cells.Count(cell => cell.State != ContentComparisonState.Unchanged));
    public int ConflictCellCount => entries.Sum(
        entry => entry.Cells.Count(cell => cell.State == ContentComparisonState.Conflict));
    public int VersionDifferenceCount => entries.Count(
        entry => entry.VersionState != VersionComparisonState.Same &&
                 entry.VersionState != VersionComparisonState.Untracked);

    internal List<EntryComparison> MutableEntries => entries;
}

/// <summary>
/// 处理 ApplyResult 对应的数据或操作。
/// </summary>
public sealed class ApplyResult
{
    public int AddedCount { get; internal set; }
    public int UpdatedCount { get; internal set; }
    public int RemovedCount { get; internal set; }
    public int VersionUpdatedCount { get; internal set; }
    public int SkippedCount { get; internal set; }
    public List<string> Errors { get; } = new List<string>();
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// 处理 VersionController 对应的数据或操作。
/// </summary>
public sealed class VersionComparisonService
{
    public ImportComparison CurrentComparison { get; private set; }

    /// <summary>
    /// 比较本地与远端 Entry 快照并生成差异结果。
    /// </summary>
    public ImportComparison Compare(
        string scope,
        string branchId,
        IEnumerable<EntrySnapshot> localEntries,
        IEnumerable<EntrySnapshot> remoteEntries)
    {
        var localMap = CreateSnapshotMap(localEntries);
        var remoteMap = CreateSnapshotMap(remoteEntries);
        var identities = new HashSet<EntryIdentity>(localMap.Keys);
        identities.UnionWith(remoteMap.Keys);

        var comparison = new ImportComparison
        {
            Scope = scope ?? string.Empty,
            BranchId = branchId ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 使用稳定顺序生成结果，保证后续可视化的展示顺序一致。
        foreach (var identity in identities
                     .OrderBy(value => value.TableName, StringComparer.Ordinal)
                     .ThenBy(value => value.EntryKey, StringComparer.Ordinal))
        {
            localMap.TryGetValue(identity, out var local);
            remoteMap.TryGetValue(identity, out var remote);

            var entryComparison = new EntryComparison
            {
                TableName = identity.TableName,
                EntryKey = identity.EntryKey,
                LocalVersionId = local?.VersionId,
                RemoteVersionId = remote?.VersionId,
                VersionState = CompareVersion(local?.VersionId, remote?.VersionId)
            };
            entryComparison.VersionResolution =
                entryComparison.VersionState == VersionComparisonState.Same ||
                entryComparison.VersionState == VersionComparisonState.Untracked
                    ? VersionResolution.KeepLocal
                    : VersionResolution.Unresolved;

            var localeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (local?.Values != null)
                localeCodes.UnionWith(local.Values.Keys);
            if (remote?.Values != null)
                localeCodes.UnionWith(remote.Values.Keys);

            // 使用两侧 Locale 并集；远端缺失 Locale 会在下方跳过，明确空字符串仍参与比较。
            foreach (var localeCode in localeCodes.OrderBy(value => value, StringComparer.Ordinal))
            {
                string localValue = null;
                string remoteValue = null;
                var localExists = local?.Values != null &&
                                  local.Values.TryGetValue(localeCode, out localValue);
                var remoteExists = remote?.Values != null &&
                                   remote.Values.TryGetValue(localeCode, out remoteValue);

                // 远端 Entry 存在但未返回该 Locale 时，不视为删除，也不生成差异。
                // 远端明确返回空字符串时 remoteExists 仍为 true，会正常参与比较。
                if (remote != null && !remoteExists)
                    continue;

                var state = CompareContent(
                    localExists,
                    localValue,
                    remoteExists,
                    remoteValue,
                    entryComparison.VersionState);
                entryComparison.MutableCells.Add(new CellComparison
                {
                    LocaleCode = localeCode,
                    LocalExists = localExists,
                    LocalValue = localValue,
                    RemoteExists = remoteExists,
                    RemoteValue = remoteValue,
                    State = state,
                    Resolution = state == ContentComparisonState.Unchanged
                        ? ValueResolution.KeepLocal
                        : ValueResolution.Unresolved
                });
            }

            comparison.MutableEntries.Add(entryComparison);
        }

        CurrentComparison = comparison;
        return comparison;
    }

    public bool SetCellResolution(
        string tableName,
        string entryKey,
        string localeCode,
        ValueResolution resolution,
        string customValue = null)
    {
        var cell = FindCell(tableName, entryKey, localeCode);
        if (cell == null)
            return false;

        cell.Resolution = resolution;
        //Custom预留
        // cell.CustomValue = resolution == ValueResolution.Custom
        //     ? customValue ?? string.Empty
        //     : null;
        return true;
    }

    /// <summary>
    /// 批量设置当前比较结果中的内容解决策略。
    /// </summary>
    public int SetAllCellResolutions(
        ValueResolution resolution,
        bool includeUnchanged = false)
    {
        if (CurrentComparison == null)
            return 0;

        var changed = 0;
        foreach (var cell in CurrentComparison.Entries.SelectMany(entry => entry.Cells))
        {
            if (!includeUnchanged && cell.State == ContentComparisonState.Unchanged)
                continue;

            cell.Resolution = resolution;
            //Custom预留
            // cell.CustomValue = resolution == ValueResolution.Custom
            //     ? cell.LocalValue ?? cell.RemoteValue ?? string.Empty
            //     : null;
            changed++;
        }

        return changed;
    }

    public bool SetEntryCellResolutions(
        string tableName,
        string entryKey,
        ValueResolution resolution,
        bool includeUnchanged = false)
    {
        var entry = FindEntry(tableName, entryKey);
        if (entry == null)
            return false;

        foreach (var cell in entry.Cells)
        {
            if (!includeUnchanged && cell.State == ContentComparisonState.Unchanged)
                continue;

            cell.Resolution = resolution;
            //Custom预留
            // cell.CustomValue = resolution == ValueResolution.Custom
            //     ? cell.LocalValue ?? cell.RemoteValue ?? string.Empty
            //     : null;
        }

        return true;
    }

    /// <summary>
    /// 设置指定 Entry 的版本解决策略。
    /// </summary>
    public bool SetVersionResolution(
        string tableName,
        string entryKey,
        VersionResolution resolution,
        string customVersionId = null)
    {
        var entry = FindEntry(tableName, entryKey);
        if (entry == null)
            return false;

        entry.VersionResolution = resolution;
        //Custom预留
        // entry.CustomVersionId = resolution == VersionResolution.Custom
        //     ? customVersionId
        //     : null;
        return true;
    }

    public int SetAllVersionResolutions(
        VersionResolution resolution,
        bool includeSame = false)
    {
        if (CurrentComparison == null)
            return 0;

        var changed = 0;
        foreach (var entry in CurrentComparison.Entries)
        {
            if (!includeSame &&
                (entry.VersionState == VersionComparisonState.Same ||
                 entry.VersionState == VersionComparisonState.Untracked))
            {
                continue;
            }

            entry.VersionResolution = resolution;
            //Custom预留
            // entry.CustomVersionId = resolution == VersionResolution.Custom
            //     ? entry.LocalVersionId ?? entry.RemoteVersionId
            //     : null;
            changed++;
        }

        return changed;
    }

    public EntryComparison FindEntry(string tableName, string entryKey)
    {
        return CurrentComparison?.Entries.FirstOrDefault(
            entry => string.Equals(entry.TableName, tableName, StringComparison.Ordinal) &&
                     string.Equals(entry.EntryKey, entryKey, StringComparison.Ordinal));
    }

    public CellComparison FindCell(
        string tableName,
        string entryKey,
        string localeCode)
    {
        return FindEntry(tableName, entryKey)?.Cells.FirstOrDefault(
            cell => string.Equals(cell.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase));
    }

    public void Clear()
    {
        CurrentComparison = null;
    }

    internal void MarkCurrentComparisonApplied()
    {
        if (CurrentComparison != null)
            CurrentComparison.IsApplied = true;
    }

    private static Dictionary<EntryIdentity, EntrySnapshot> CreateSnapshotMap(
        IEnumerable<EntrySnapshot> entries)
    {
        var map = new Dictionary<EntryIdentity, EntrySnapshot>();
        if (entries == null)
            return map;

        foreach (var entry in entries)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.TableName) ||
                string.IsNullOrEmpty(entry.EntryKey))
            {
                continue;
            }

            var identity = new EntryIdentity(entry.TableName, entry.EntryKey);
            if (!map.TryGetValue(identity, out var existing))
            {
                existing = new EntrySnapshot
                {
                    TableName = entry.TableName,
                    EntryKey = entry.EntryKey,
                    VersionId = entry.VersionId
                };
                map.Add(identity, existing);
            }

            if (!string.IsNullOrEmpty(entry.VersionId))
                existing.VersionId = entry.VersionId;

            if (entry.Values == null)
                continue;

            foreach (var value in entry.Values)
                existing.Values[value.Key] = value.Value;
        }

        return map;
    }

    // 版本号是不透明字符串，因此这里只做存在性与相等性比较。
    private static VersionComparisonState CompareVersion(
        string localVersion,
        string remoteVersion)
    {
        var hasLocal = !string.IsNullOrEmpty(localVersion);
        var hasRemote = !string.IsNullOrEmpty(remoteVersion);

        if (!hasLocal && !hasRemote)
            return VersionComparisonState.Untracked;
        if (hasLocal && !hasRemote)
            return VersionComparisonState.LocalOnly;
        if (!hasLocal)
            return VersionComparisonState.RemoteOnly;
        return string.Equals(localVersion, remoteVersion, StringComparison.Ordinal)
            ? VersionComparisonState.Same
            : VersionComparisonState.Different;
    }

    // 内容不同且两侧版本也不同时标记为 Conflict，交由用户显式处理。
    private static ContentComparisonState CompareContent(
        bool localExists,
        string localValue,
        bool remoteExists,
        string remoteValue,
        VersionComparisonState versionState)
    {
        if (!localExists)
            return remoteExists
                ? ContentComparisonState.RemoteOnly
                : ContentComparisonState.Unchanged;
        if (!remoteExists)
            return ContentComparisonState.LocalOnly;
        if (string.Equals(localValue, remoteValue, StringComparison.Ordinal))
            return ContentComparisonState.Unchanged;
        return versionState == VersionComparisonState.Different
            ? ContentComparisonState.Conflict
            : ContentComparisonState.Changed;
    }

    private readonly struct EntryIdentity : IEquatable<EntryIdentity>
    {
        public EntryIdentity(string tableName, string entryKey)
        {
            TableName = tableName;
            EntryKey = entryKey;
        }

        public string TableName { get; }
        public string EntryKey { get; }

        public bool Equals(EntryIdentity other)
        {
            return string.Equals(TableName, other.TableName, StringComparison.Ordinal) &&
                   string.Equals(EntryKey, other.EntryKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EntryIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((TableName != null ? TableName.GetHashCode() : 0) * 397) ^
                       (EntryKey != null ? EntryKey.GetHashCode() : 0);
            }
        }
    }
}
}
