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
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.Features.Export
{

internal sealed class ExportController : FlowConnectorComponent
{
    internal ExportController(FlowConnectorContext context) : base(context) { }

    internal void Export() => ExportData();

    private async void ExportData()
    {
        if (isLoading) return;

        if (string.IsNullOrEmpty(flowUrlPrefix))
        {
            EditorUtility.DisplayDialog("Error", "Please configure a server in Settings tab.", "OK");
            return;
        }

        if (tableData.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No table data loaded.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(selectedBranch))
        {
            EditorUtility.DisplayDialog("Error", "No branch selected.", "OK");
            return;
        }

        SetLoading(true, btnPush, "Pushing...");

        try
        {
            GamePushParam gamePushParam = new();
            gamePushParam.tableData = new();
            gamePushParam.branchId = branchMap[selectedBranch];

            var versionScope = Context.ImportController.GetVersionScope(gamePushParam.branchId);
            var storedVersions = Context.ImportController.GetStoredVersions();
            var exportedVersionCount = 0;
            var exportedImageCount = 0;
            var exportedCharLimitCount = 0;
            var exportedCommentCount = 0;

            foreach (var table in selectedTableData)
            {
                if (table.Value.Count > 0)
                {
                    GameTableData gameTableData = new();
                    gameTableData.tableName = table.Key;
                    gameTableData.entryData = new();
                    foreach (var entry in table.Value)
                    {
                        var versionId = Context.ImportController.GetStoredEntryVersion(
                            storedVersions,
                            versionScope,
                            table.Key,
                            entry);
                        var metadata =
                            Context.EntryMetadataRepository.Get(
                                new EntryId(table.Key, entry));
                        var pngImage = metadata?.ScreenshotDirty == true
                            ? Context.ScreenshotController.GetPngForExport(table.Key, entry)
                            : null;
                        int? charLimit = metadata?.CharLimitDirty == true
                            ? metadata.CharLimit
                            : -1;
                        var comment = metadata?.CommentDirty == true
                            ? metadata.Comment
                            : null;

                        GameEntryData gameEntryData = new()
                        {
                            entryKey = entry,
                            versionId = versionId,
                            cellValue = tableData[table.Key][entry],
                            image = pngImage,
                            limitSize = charLimit,
                            comment = comment
                        };
                        gameTableData.entryData.Add(gameEntryData);

                        if (!string.IsNullOrEmpty(versionId))
                            exportedVersionCount++;
                        if (pngImage != null)
                            exportedImageCount++;
                        if (charLimit.HasValue)
                            exportedCharLimitCount++;
                        if (comment != null)
                            exportedCommentCount++;
                    }
                    gamePushParam.tableData.Add(gameTableData);
                }
            }

            // Debug.Log(
            //     $"Prepared Export payload with {exportedVersionCount} versions and " +
            //     $"{exportedImageCount} new PNG screenshots, " +
            //     $"{exportedCharLimitCount} changed char limits and " +
            //     $"{exportedCommentCount} changed comments.");

            var pushUrl = flowUrlPrefix + CMS_URL_PATH + PUSH_METHOD;
            var response = await PostAsync(pushUrl, gamePushParam);

            var result = JsonConvert.DeserializeAnonymousType<Result<GamePushDTO>>(response, new Result<GamePushDTO>());
            if (result != null && result.code == 0)
            {
                var submittedEntryCount = gamePushParam.tableData.Sum(
                    table => table.entryData?.Count ?? 0);
                var updatedMetadataCount =
                    Context.EntryMetadataRepository.ApplyPushResult(
                        result.data,
                        gamePushParam);
                var updatedVersionCount =
                    Context.ImportController.ApplyPushVersion(result.data, gamePushParam);

                string warning = "";
                if (string.IsNullOrEmpty(result.data?.newVersion))
                {
                    warning = "Push succeeded, but GamePushDTO.newVersion was empty. " +
                        "Local entry versions were not changed.";
                }

                EditorUtility.DisplayDialog(
                    "Push Successful",
                    $"Warning: {warning ?? "(null)"}\n" +
                    $"Latest version: {result.data?.newVersion ?? "(empty)"}\n" +
                    $"Screenshot ID: {result.data?.screenShotId ?? "(empty)"}\n" +
                    $"Submitted entries: {submittedEntryCount}\n" +
                    $"Updated local metadata: {updatedMetadataCount}\n" +
                    $"Updated local versions: {updatedVersionCount}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Push Failed", $"Code: {result?.code ?? -1}, Message: {result?.msg ?? "Unknown error"}", "OK");
            }

        }
        finally
        {
            SetLoading(false, btnPush, btnPushText);
        }
    }

    private void SetLoading(bool loading, Button button, string text)
    {
        isLoading = loading;
        if (button != null)
        {
            button.text = text;
            button.SetEnabled(!loading);
        }

        btnPush?.SetEnabled(!loading);
        btnPull?.SetEnabled(!loading);
    }
}
}
