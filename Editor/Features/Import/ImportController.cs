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
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace EciFlowConnector.Editor.Features.Import
{

internal sealed class ImportController : FlowConnectorComponent
{
    internal ImportController(FlowConnectorContext context) : base(context) { }

    internal ImportComparison CompareImport(GamePullDTO remoteData, string branchId) =>
        CreateImportComparison(remoteData, branchId);

    internal LocalDifferenceContext GetLocalContext() => CreateLocalDifferenceContext();

    internal LocalDifferenceResult GetLocalResult(
        LocalDifferenceContext context,
        string tableName,
        string entryKey,
        string localeCode,
        string currentValue) =>
        GetLocalDifferenceResult(context, tableName, entryKey, localeCode, currentValue);

    internal int ApplyPushVersion(GamePushDTO response, GamePushParam request) =>
        ApplyPushResponseVersion(response, request);

    internal string GetVersionScope(string branchId) => CreateVersionScope(branchId);
    internal List<StoredLocalizationVersion> GetStoredVersions() => LoadStoredVersions();
    internal string GetStoredEntryVersion(
        IEnumerable<StoredLocalizationVersion> versions,
        string scope,
        string tableName,
        string entryKey) =>
        GetStoredVersion(versions, scope, tableName, entryKey);

    private const string LOCALIZATION_VERSION_PREF_KEY = "ECIFlowConnector_LocalizationVersions_V1";

    // 处理 versionComparisonService 对应的数据或操作。
    private readonly VersionComparisonService versionComparisonService =
        new VersionComparisonService();

    /// <summary>
    /// 处理 versionComparisonService 对应的数据或操作。
    /// </summary>
    public VersionComparisonService VersionComparisonService =>
        versionComparisonService;

    public ImportComparison CurrentImportComparison =>
        versionComparisonService.CurrentComparison;

    /// <summary>
    /// 设置指定语言单元格的 Import 内容解决策略。
    /// </summary>
    public bool SetImportCellResolution(
        string tableName,
        string entryKey,
        string localeCode,
        ValueResolution resolution,
        string customValue = null)
    {
        return versionComparisonService.SetCellResolution(
            tableName,
            entryKey,
            localeCode,
            resolution,
            customValue);
    }

    /// <summary>
    /// 设置指定 Entry 的版本解决策略。
    /// </summary>
    public bool SetImportVersionResolution(
        string tableName,
        string entryKey,
        VersionResolution resolution,
        string customVersionId = null)
    {
        return versionComparisonService.SetVersionResolution(
            tableName,
            entryKey,
            resolution,
            customVersionId);
    }

    /// <summary>
    /// 将指定 Entry 的全部内容和版本设置为使用远端结果。
    /// </summary>
    public bool UseRemoteImportEntry(string tableName, string entryKey)
    {
        var contentResolved = versionComparisonService.SetEntryCellResolutions(
            tableName,
            entryKey,
            ValueResolution.UseRemote);
        var versionResolved = versionComparisonService.SetVersionResolution(
            tableName,
            entryKey,
            VersionResolution.UseRemote);
        return contentResolved && versionResolved;
    }

    /// <summary>
    /// 将指定 Entry 的全部内容设置为保留本地结果。
    /// </summary>
    public bool KeepLocalImportEntry(string tableName, string entryKey)
    {
        var contentResolved = versionComparisonService.SetEntryCellResolutions(
            tableName,
            entryKey,
            ValueResolution.KeepLocal,
            true);
        var versionResolved = versionComparisonService.SetVersionResolution(
            tableName,
            entryKey,
            VersionResolution.UseRemote);
        return contentResolved && versionResolved;
    }

    /// <summary>
    /// 将当前全部差异设置为使用远端结果。
    /// </summary>
    public void AcceptAllRemoteImportData()
    {
        versionComparisonService.SetAllCellResolutions(
            ValueResolution.UseRemote);
        // 保留本地内容时仍采用远端版本号，作为后续比较的新基线。
        versionComparisonService.SetAllVersionResolutions(
            VersionResolution.UseRemote);
    }

    /// <summary>
    /// 将当前全部内容差异设置为保留本地结果。
    /// </summary>
    public void KeepAllLocalImportData()
    {
        versionComparisonService.SetAllCellResolutions(
            ValueResolution.KeepLocal,
            true);
        versionComparisonService.SetAllVersionResolutions(
            VersionResolution.UseRemote,
            true);
    }

    /// <summary>
    /// 丢弃当前尚未应用的 Import 差异结果。
    /// </summary>
    public void DiscardImportComparison()
    {
        versionComparisonService.Clear();
    }

    /// <summary>
    /// 把已经解决的 Import 差异写入 Unity Localization Table。
    /// </summary>
    public ApplyResult ApplyImportComparison()
    {
        var comparison = CurrentImportComparison;
        var result = new ApplyResult();
        if (comparison == null)
        {
            result.Errors.Add("No import comparison is available.");
            return result;
        }

        if (comparison.IsApplied)
        {
            result.Errors.Add("The current import comparison has already been applied.");
            return result;
        }

        foreach (var entry in comparison.Entries)
        {
            if (entry.VersionResolution == VersionResolution.Unresolved)
            {
                result.Errors.Add(
                    $"{entry.TableName}/{entry.EntryKey}: version difference is unresolved.");
            }

            foreach (var cell in entry.Cells)
            {
                if (cell.Resolution == ValueResolution.Unresolved)
                {
                    result.Errors.Add(
                        $"{entry.TableName}/{entry.EntryKey}/{cell.LocaleCode}: " +
                        "content difference is unresolved.");
                }
            }
        }

        if (result.Errors.Count > 0)
            return result;

        var recordedObjects = new HashSet<UnityEngine.Object>();
        var dirtyTables = new HashSet<StringTable>();
        var failedEntries = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entryComparison in comparison.Entries)
        {
            foreach (var cell in entryComparison.Cells)
            {
                if (cell.Resolution == ValueResolution.KeepLocal)
                {
                    result.SkippedCount++;
                    continue;
                }

                var locale = LocalizationSettings.AvailableLocales.GetLocale(cell.LocaleCode);
                if (locale == null)
                {
                    AddApplyError(
                        result,
                        failedEntries,
                        entryComparison,
                        $"Locale '{cell.LocaleCode}' was not found.");
                    continue;
                }

                var collection =
                    LocalizationEditorSettings.GetStringTableCollection(entryComparison.TableName);
                var table = collection?.GetTable(locale.Identifier) as StringTable;
                if (table == null)
                {
                    AddApplyError(
                        result,
                        failedEntries,
                        entryComparison,
                        $"StringTable '{entryComparison.TableName}/{cell.LocaleCode}' was not found.");
                    continue;
                }

                RecordForUndo(table, recordedObjects);
                RecordForUndo(table.SharedData, recordedObjects);

                var currentEntry = table.GetEntry(entryComparison.EntryKey);
                if (!cell.FinalExists)
                {
                    if (currentEntry != null && table.RemoveEntry(entryComparison.EntryKey))
                    {
                        result.RemovedCount++;
                        dirtyTables.Add(table);
                        UpdateCachedTableValue(
                            entryComparison.TableName,
                            entryComparison.EntryKey,
                            cell.LocaleCode,
                            false,
                            null);
                    }
                    else
                    {
                        result.SkippedCount++;
                    }

                    continue;
                }

                var finalValue = cell.FinalValue ?? string.Empty;
                if (currentEntry == null)
                {
                    table.AddEntry(entryComparison.EntryKey, finalValue);
                    result.AddedCount++;
                    dirtyTables.Add(table);
                    UpdateCachedTableValue(
                        entryComparison.TableName,
                        entryComparison.EntryKey,
                        cell.LocaleCode,
                        true,
                        finalValue);
                }
                else if (!string.Equals(currentEntry.Value, finalValue, StringComparison.Ordinal))
                {
                    currentEntry.Value = finalValue;
                    result.UpdatedCount++;
                    dirtyTables.Add(table);
                    UpdateCachedTableValue(
                        entryComparison.TableName,
                        entryComparison.EntryKey,
                        cell.LocaleCode,
                        true,
                        finalValue);
                }
                else
                {
                    result.SkippedCount++;
                }
            }
        }

        foreach (var table in dirtyTables)
        {
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
        }

        if (dirtyTables.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        var storedVersions = LoadStoredVersions();
        foreach (var entry in comparison.Entries)
        {
            if (failedEntries.Contains(GetEntryIdentity(entry.TableName, entry.EntryKey)))
            {
                continue;
            }

            if (entry.VersionResolution != VersionResolution.KeepLocal)
            {
                SetStoredVersion(
                    storedVersions,
                    comparison.Scope,
                    entry.TableName,
                    entry.EntryKey,
                    entry.FinalVersionId);
                result.VersionUpdatedCount++;
            }

            foreach (var cell in entry.Cells)
            {
                SetStoredContentValue(
                    storedVersions,
                    comparison.Scope,
                    entry.TableName,
                    entry.EntryKey,
                    cell.LocaleCode,
                    cell.FinalExists,
                    cell.FinalValue);
            }
        }
        RemoveEmptyStoredEntries(storedVersions);
        SaveStoredVersions(storedVersions);
        Context.TableBrowser.RefreshData();

        if (result.Success)
            versionComparisonService.MarkCurrentComparisonApplied();

        return result;
    }

    /// <summary>
    /// 根据本地数据和远端数据创建版本差异结果。
    /// </summary>
    private ImportComparison CreateImportComparison(GamePullDTO remoteData, string branchId)
    {
        var scope = CreateVersionScope(branchId);
        var storedVersions = LoadStoredVersions();
        var remoteSnapshots = BuildRemoteSnapshots(remoteData);
        var localSnapshots = BuildLocalSnapshots(remoteSnapshots, scope, storedVersions);

        // 服务端响应中完全没有的 Entry 不代表删除，直接排除，不生成 LocalOnly 差异。
        var returnedRemoteIdentities = new HashSet<string>(
            remoteSnapshots.Select(
                snapshot => GetEntryIdentity(snapshot.TableName, snapshot.EntryKey)),
            StringComparer.Ordinal);
        localSnapshots.RemoveAll(snapshot => !returnedRemoteIdentities.Contains(
            GetEntryIdentity(snapshot.TableName, snapshot.EntryKey)));

        return versionComparisonService.Compare(
            scope,
            branchId,
            localSnapshots,
            remoteSnapshots);
    }

    // 把服务器返回的数据转换为可比较的远端快照。
    private List<EntrySnapshot> BuildRemoteSnapshots(GamePullDTO remoteData)
    {
        var snapshots = new Dictionary<string, EntrySnapshot>(StringComparer.Ordinal);
        if (remoteData?.tableData == null)
            return snapshots.Values.ToList();

        foreach (var tablePair in remoteData.tableData)
        {
            if (tablePair.Value == null)
                continue;

            foreach (var remoteEntry in tablePair.Value)
            {
                if (remoteEntry == null || string.IsNullOrEmpty(remoteEntry.entryKey))
                    continue;

                var identity = GetEntryIdentity(tablePair.Key, remoteEntry.entryKey);
                if (!snapshots.TryGetValue(identity, out var snapshot))
                {
                    snapshot = new EntrySnapshot
                    {
                        TableName = tablePair.Key,
                        EntryKey = remoteEntry.entryKey
                    };
                    snapshots.Add(identity, snapshot);
                }

                if (!string.IsNullOrEmpty(remoteEntry.versionId))
                    snapshot.VersionId = remoteEntry.versionId;

                if (remoteEntry.cellValue == null)
                    continue;

                foreach (var cell in remoteEntry.cellValue)
                    snapshot.Values[cell.Key] = cell.Value;
            }
        }

        return snapshots.Values.ToList();
    }

    // 读取 Unity Localization Table 并生成本地快照。
    private List<EntrySnapshot> BuildLocalSnapshots(IEnumerable<EntrySnapshot> remoteSnapshots, string scope, List<StoredLocalizationVersion> storedVersions)
    {
        var requestedEntries =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var selectedTable in selectedTableData)
        {
            if (!requestedEntries.TryGetValue(selectedTable.Key, out var keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                requestedEntries.Add(selectedTable.Key, keys);
            }

            if (selectedTable.Value != null)
                keys.UnionWith(selectedTable.Value);
        }

        foreach (var remote in remoteSnapshots)
        {
            if (!requestedEntries.TryGetValue(remote.TableName, out var keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                requestedEntries.Add(remote.TableName, keys);
            }
            keys.Add(remote.EntryKey);
        }

        var snapshots = new List<EntrySnapshot>();
        foreach (var tablePair in requestedEntries)
        {
            var collection =
                LocalizationEditorSettings.GetStringTableCollection(tablePair.Key);

            foreach (var entryKey in tablePair.Value)
            {
                var snapshot = new EntrySnapshot
                {
                    TableName = tablePair.Key,
                    EntryKey = entryKey,
                    VersionId = GetStoredVersion(
                        storedVersions,
                        scope,
                        tablePair.Key,
                        entryKey)
                };

                if (collection != null)
                {
                    foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                    {
                        var table = collection.GetTable(locale.Identifier) as StringTable;
                        var entry = table?.GetEntry(entryKey);
                        if (entry != null)
                            snapshot.Values[locale.Identifier.Code] = entry.Value;
                    }
                }

                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    // 创建当前服务器和分支对应的本地版本作用域。
    private string CreateVersionScope(string branchId)
    {
        return
            $"{flowUrlPrefix?.TrimEnd('/') ?? string.Empty}|" +
            $"{apiKey}|" +
            $"{branchId ?? string.Empty}";
    }

    private static string GetEntryIdentity(string tableName, string entryKey)
    {
        return $"{tableName}\u001f{entryKey}";
    }

    private static void AddApplyError(ApplyResult result, ISet<string> failedEntries, EntryComparison entry, string message)
    {
        failedEntries.Add(GetEntryIdentity(entry.TableName, entry.EntryKey));
        result.Errors.Add($"{entry.TableName}/{entry.EntryKey}: {message}");
    }

    private static void RecordForUndo(UnityEngine.Object target, ISet<UnityEngine.Object> recordedObjects)
    {
        if (target != null && recordedObjects.Add(target))
            Undo.RecordObject(target, "Apply Localization Import");
    }

    // 同步更新窗口中缓存的 Table 单元格数据。
    private void UpdateCachedTableValue(string tableName, string entryKey, string localeCode, bool exists, string value)
    {
        if (!tableData.TryGetValue(tableName, out var entries))
            return;

        if (!entries.TryGetValue(entryKey, out var cells))
        {
            if (!exists)
                return;

            cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            entries.Add(entryKey, cells);
        }

        if (exists)
        {
            cells[localeCode] = value ?? string.Empty;
        }
        else
        {
            cells.Remove(localeCode);
            if (cells.Count == 0)
                entries.Remove(entryKey);
        }
    }

    /// <summary>
    /// 创建刷新 Table 状态标签所需的本地比较上下文。
    /// </summary>
    private LocalDifferenceContext CreateLocalDifferenceContext()
    {
        if (string.IsNullOrEmpty(selectedBranch) ||
            !branchMap.TryGetValue(selectedBranch, out var branchId))
        {
            return new LocalDifferenceContext(null, null);
        }

        return new LocalDifferenceContext(
            CreateVersionScope(branchId),
            LoadStoredVersions());
    }

    /// <summary>
    /// 计算当前单元格相对本地版本记录的差异状态。
    /// </summary>
    private static LocalDifferenceResult GetLocalDifferenceResult(
        LocalDifferenceContext context,
        string tableName,
        string entryKey,
        string localeCode,
        string currentValue)
    {
        if (context == null ||
            string.IsNullOrEmpty(context.Scope) ||
            context.StoredEntries == null)
        {
            return LocalDifferenceResult.NoResult;
        }

        var stored = FindStoredEntry(context.StoredEntries,context.Scope,tableName,entryKey);
        
        if (stored?.Values == null ||
            !TryGetStoredContentValue(stored.Values, localeCode, out var baselineValue))
        {
            return LocalDifferenceResult.NoResult;
        }

        return string.Equals(
            baselineValue ?? string.Empty,
            currentValue ?? string.Empty,
            StringComparison.Ordinal)
            ? LocalDifferenceResult.Unchanged
            : LocalDifferenceResult.Changed;
    }

    // 从 EditorPrefs 读取本地保存的 Entry 版本记录。
    private static List<StoredLocalizationVersion> LoadStoredVersions()
    {
        var json = EditorPrefs.GetString(LOCALIZATION_VERSION_PREF_KEY, "[]");
        try
        {
            return JsonConvert.DeserializeObject<List<StoredLocalizationVersion>>(json) ??
                   new List<StoredLocalizationVersion>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Flow Connector Warning: " + $"Failed to load local Localization versions: {exception.Message}");
            return new List<StoredLocalizationVersion>();
        }
    }

    private static void SaveStoredVersions(List<StoredLocalizationVersion> versions)
    {
        EditorPrefs.SetString(
            LOCALIZATION_VERSION_PREF_KEY,
            JsonConvert.SerializeObject(versions));
    }

    private static string GetStoredVersion(
        IEnumerable<StoredLocalizationVersion> versions,
        string scope,
        string tableName,
        string entryKey)
    {
        return FindStoredEntry(versions, scope, tableName, entryKey)?.VersionId;
    }

    /// <summary>
    /// 将 Push 返回的新版本号写入已提交 Entry 的本地记录。
    /// </summary>
    private int ApplyPushResponseVersion(
        GamePushDTO pushResponse,
        GamePushParam pushRequest)
    {
        if (string.IsNullOrEmpty(pushResponse?.newVersion) ||
            pushRequest?.tableData == null)
        {
            return 0;
        }

        var scope = CreateVersionScope(pushRequest.branchId);
        var storedVersions = LoadStoredVersions();
        var updatedEntries = new HashSet<string>(StringComparer.Ordinal);

        foreach (var table in pushRequest.tableData)
        {
            if (table?.entryData == null || string.IsNullOrEmpty(table.tableName))
                continue;

            foreach (var submittedEntry in table.entryData)
            {
                if (submittedEntry == null || string.IsNullOrEmpty(submittedEntry.entryKey))
                    continue;

                var identity = GetEntryIdentity(table.tableName, submittedEntry.entryKey);
                if (!updatedEntries.Add(identity))
                    continue;

                SetStoredVersion(
                    storedVersions,
                    scope,
                    table.tableName,
                    submittedEntry.entryKey,
                    pushResponse.newVersion);

                if (submittedEntry.cellValue == null)
                    continue;

                foreach (var cell in submittedEntry.cellValue)
                {
                    SetStoredContentValue(
                        storedVersions,
                        scope,
                        table.tableName,
                        submittedEntry.entryKey,
                        cell.Key,
                        true,
                        cell.Value);
                }
            }
        }

        if (updatedEntries.Count == 0)
            return 0;

        RemoveEmptyStoredEntries(storedVersions);
        SaveStoredVersions(storedVersions);
        Context.TableBrowser.RefreshData();
        return updatedEntries.Count;
    }

    private static void SetStoredVersion(
        List<StoredLocalizationVersion> versions,
        string scope,
        string tableName,
        string entryKey,
        string versionId)
    {
        var stored = FindStoredEntry(versions, scope, tableName, entryKey);

        if (string.IsNullOrEmpty(versionId))
        {
            if (stored != null)
                stored.VersionId = null;
            return;
        }

        stored = GetOrCreateStoredEntry(versions, scope, tableName, entryKey);
        stored.VersionId = versionId;
    }

    private static void SetStoredContentValue(
        List<StoredLocalizationVersion> versions,
        string scope,
        string tableName,
        string entryKey,
        string localeCode,
        bool exists,
        string value)
    {
        var stored = GetOrCreateStoredEntry(versions, scope, tableName, entryKey);
        stored.Values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var storedLocaleCode = stored.Values.Keys.FirstOrDefault(
            key => string.Equals(key, localeCode, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            if (storedLocaleCode != null)
                stored.Values.Remove(storedLocaleCode);
            return;
        }

        stored.Values[storedLocaleCode ?? localeCode] = value ?? string.Empty;
    }

    private static bool TryGetStoredContentValue(
        IReadOnlyDictionary<string, string> values,
        string localeCode,
        out string value)
    {
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static StoredLocalizationVersion FindStoredEntry(
        IEnumerable<StoredLocalizationVersion> versions,
        string scope,
        string tableName,
        string entryKey)
    {
        return versions.FirstOrDefault(
            version => string.Equals(version.Scope, scope, StringComparison.Ordinal) &&
                       string.Equals(version.TableName, tableName, StringComparison.Ordinal) &&
                       string.Equals(version.EntryKey, entryKey, StringComparison.Ordinal));
    }

    private static StoredLocalizationVersion GetOrCreateStoredEntry(
        List<StoredLocalizationVersion> versions,
        string scope,
        string tableName,
        string entryKey)
    {
        var stored = FindStoredEntry(versions, scope, tableName, entryKey);
        if (stored != null)
            return stored;

        stored = new StoredLocalizationVersion
        {
            Scope = scope,
            TableName = tableName,
            EntryKey = entryKey,
            Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
        versions.Add(stored);
        return stored;
    }

    private static void RemoveEmptyStoredEntries(List<StoredLocalizationVersion> versions)
    {
        versions.RemoveAll(
            version => string.IsNullOrEmpty(version.VersionId) &&
                       (version.Values == null || version.Values.Count == 0));
    }

    internal sealed class LocalDifferenceContext
    {
        public LocalDifferenceContext(string scope, List<StoredLocalizationVersion> storedEntries)
        {
            Scope = scope;
            StoredEntries = storedEntries;
        }

        public string Scope { get; }
        public List<StoredLocalizationVersion> StoredEntries { get; }
    }

    internal readonly struct LocalDifferenceResult
    {
        private LocalDifferenceResult(string text, string className, string tooltip)
        {
            Text = text;
            ClassName = className;
            Tooltip = tooltip;
        }

        public string Text { get; }
        public string ClassName { get; }
        public string Tooltip { get; }

        public static LocalDifferenceResult NoResultValue =>
            new LocalDifferenceResult(
                "No Result",
                "table-cell-status-none",
                "No local version results available for comparison.");

        public static LocalDifferenceResult UnchangedValue =>
            new LocalDifferenceResult(
                "Synced",
                "table-cell-status-clean",
                "The current local content matches the recorded local version.");

        public static LocalDifferenceResult ChangedValue =>
            new LocalDifferenceResult(
                "Diff",
                "table-cell-status-changed",
                "The current local content differs from the recorded local version.");

        // 处理 NoResultValue 对应的数据或操作。

        public static LocalDifferenceResult NoResult => NoResultValue;
        public static LocalDifferenceResult Unchanged => UnchangedValue;
        public static LocalDifferenceResult Changed => ChangedValue;
    }

    [Serializable]
    internal sealed class StoredLocalizationVersion
    {
        // 用于隔离不同服务器与分支的本地版本作用域。
        public string Scope;
        public string TableName;
        public string EntryKey;
        public string VersionId;
        public Dictionary<string, string> Values;
    }
}
}
