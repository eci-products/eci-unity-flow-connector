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
using UnityEngine;

/// <summary>
/// 负责按 Table/Entry 在本机持久化字符限制、备注和服务器截图 ID。
/// </summary>
namespace EciFlowConnector.Editor.Infrastructure.Persistence
{

internal sealed class EntryMetadataRepository
{
    private const string EntryMetadataPrefKey =
        "ECIFlowConnector_EntryMetadata_V1";

    internal EntryMetadata Get(EntryId entryId)
    {
        var stored = Find(Load(), entryId);
        return stored == null ? null : Clone(stored);
    }

    internal void SetCharLimit(EntryId entryId, int charLimit)
    {
        Update(entryId, metadata =>
        {
            metadata.CharLimit = Math.Max(0, charLimit);
            metadata.CharLimitDirty =
                metadata.CharLimit != metadata.SyncedCharLimit;
        });
    }

    internal void SetComment(EntryId entryId, string comment)
    {
        Update(entryId, metadata =>
        {
            metadata.Comment = comment ?? string.Empty;
            metadata.CommentDirty = !string.Equals(
                metadata.Comment,
                metadata.SyncedComment,
                StringComparison.Ordinal);
        });
    }

    internal void MarkScreenshotDirty(EntryId entryId)
    {
        Update(entryId, metadata => metadata.ScreenshotDirty = true);
    }

    internal void MarkScreenshotUndirty(EntryId entryId)
    {
        Update(entryId, metadata => metadata.ScreenshotDirty = false);
    }

    /// <summary>
    /// Push 成功后，将请求中的详情和返回的截图 ID 保存到对应 Entry。
    /// </summary>
    internal int ApplyPushResult(GamePushDTO response, GamePushParam request)
    {
        if (request?.tableData == null)
            return 0;

        var records = Load();
        var updatedCount = 0;
        foreach (var table in request.tableData)
        {
            if (table?.entryData == null || string.IsNullOrEmpty(table.tableName))
                continue;

            foreach (var entry in table.entryData)
            {
                if (entry == null || string.IsNullOrEmpty(entry.entryKey))
                    continue;

                var hasImage = entry.image != null && entry.image.Length > 0;
                if (!entry.limitSize.HasValue && entry.comment == null && !hasImage)
                    continue;

                var metadata = GetOrCreate(
                    records,
                    new EntryId(table.tableName, entry.entryKey));
                if (entry.limitSize.HasValue)
                {
                    metadata.CharLimit = Math.Max(0, entry.limitSize.Value);
                    metadata.SyncedCharLimit = metadata.CharLimit;
                    metadata.CharLimitDirty = false;
                }

                if (entry.comment != null)
                {
                    metadata.Comment = entry.comment;
                    metadata.SyncedComment = metadata.Comment;
                    metadata.CommentDirty = false;
                }

                if (hasImage && !string.IsNullOrEmpty(response?.screenShotId))
                {
                    metadata.ScreenShotId = response.screenShotId;
                    metadata.ScreenshotDirty = false;
                }

                metadata.HasSyncState = true;
                updatedCount++;
            }
        }

        if (updatedCount > 0)
            Save(records);

        return updatedCount;
    }

    internal void Dispose()
    {
        var json = EditorPrefs.GetString(EntryMetadataPrefKey, "[]");
        try
        {
            var records = JsonConvert.DeserializeObject<List<EntryMetadata>>(json) ??
                          new List<EntryMetadata>();

            // 兼容旧数据：升级前已经存在本地的数据视为已同步基线，不重复推送。
            foreach (var metadata in records)
            {
                metadata.ScreenshotDirty = false;

            }
            Save(records);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Flow Connector Warning: " +  $"Failed to load local Entry metadata: {exception.Message}");
        }

    }
    private static void Update(EntryId entryId, Action<EntryMetadata> update)
    {
        if (!entryId.IsValid)
            return;

        var records = Load();
        update(GetOrCreate(records, entryId));
        Save(records);
    }

    private static List<EntryMetadata> Load()
    {
        var json = EditorPrefs.GetString(EntryMetadataPrefKey, "[]");
        try
        {
            var records = JsonConvert.DeserializeObject<List<EntryMetadata>>(json) ??
                          new List<EntryMetadata>();

            // 兼容旧数据：升级前已经存在本地的数据视为已同步基线，不重复推送。
            foreach (var metadata in records.Where(item => item != null && !item.HasSyncState))
            {
                metadata.SyncedCharLimit = metadata.CharLimit;
                metadata.SyncedComment = metadata.Comment ?? string.Empty;
                metadata.CharLimitDirty = false;
                metadata.CommentDirty = false;
                metadata.ScreenshotDirty = false;
                metadata.HasSyncState = true;
            }

            return records;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Flow Connector Warning: " + 
                $"Failed to load local Entry metadata: {exception.Message}");
            return new List<EntryMetadata>();
        }
    }

    private static void Save(List<EntryMetadata> records)
    {
        EditorPrefs.SetString(
            EntryMetadataPrefKey,
            JsonConvert.SerializeObject(records));
    }

    private static EntryMetadata GetOrCreate(
        ICollection<EntryMetadata> records,
        EntryId entryId)
    {
        var metadata = Find(records, entryId);
        if (metadata != null)
            return metadata;

        metadata = new EntryMetadata
        {
            TableName = entryId.TableName,
            EntryKey = entryId.EntryKey,
            HasSyncState = true
        };
        records.Add(metadata);
        return metadata;
    }

    private static EntryMetadata Find(
        IEnumerable<EntryMetadata> records,
        EntryId entryId)
    {
        if (!entryId.IsValid)
            return null;

        return records.FirstOrDefault(record =>
            record != null &&
            record.Id == entryId);
    }

    private static EntryMetadata Clone(EntryMetadata source)
    {
        return new EntryMetadata
        {
            TableName = source.TableName,
            EntryKey = source.EntryKey,
            CharLimit = source.CharLimit,
            Comment = source.Comment ?? string.Empty,
            ScreenShotId = source.ScreenShotId ?? string.Empty,
            SyncedCharLimit = source.SyncedCharLimit,
            SyncedComment = source.SyncedComment ?? string.Empty,
            HasSyncState = source.HasSyncState,
            CharLimitDirty = source.CharLimitDirty,
            CommentDirty = source.CommentDirty,
            ScreenshotDirty = source.ScreenshotDirty
        };
    }
}
}
