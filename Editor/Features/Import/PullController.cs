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
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

/// <summary>
/// 负责从 Flow 拉取远端数据并创建 Import 对比结果。
/// </summary>
namespace EciFlowConnector.Editor.Features.Import
{

internal sealed class PullController : FlowConnectorComponent
{
    internal PullController(FlowConnectorContext context) : base(context) { }

    internal void Pull() => PullData();

    private async void PullData()
    {
        if (isLoading)
            return;

        if (string.IsNullOrEmpty(selectedBranch))
        {
            EditorUtility.DisplayDialog("Error", "Please select a branch first.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(flowUrlPrefix))
        {
            EditorUtility.DisplayDialog(
                "Error",
                "Please configure a server in Settings tab.",
                "OK");
            return;
        }

        SetLoading(true, btnPull, "Pulling...");
        try
        {
            if (!LocalizationSettings.InitializationOperation.IsDone)
                await LocalizationSettings.InitializationOperation.Task;

            var request = new GamePullParam
            {
                branchId = branchMap[selectedBranch],
                tableData = new()
            };

            foreach (var table in selectedTableData)
            {
                if (table.Value.Count == 0)
                    continue;

                var tableRequest = new GameTableData
                {
                    tableName = table.Key,
                    entryData = new()
                };
                foreach (var entryKey in table.Value)
                    tableRequest.entryData.Add(new GameEntryData { entryKey = entryKey });
                request.tableData.Add(tableRequest);
            }

            var response = await PostAsync(
                flowUrlPrefix + CMS_URL_PATH + PULL_METHOD,
                request);
            var result = JsonConvert.DeserializeObject<Result<GamePullDTO>>(response);
            if (result == null || result.code != 0)
            {
                EditorUtility.DisplayDialog(
                    "Pull Failed",
                    $"Code: {result?.code ?? -1}, Message: {result?.msg ?? "Unknown error"}",
                    "OK");
                return;
            }

            var comparison = Context.ImportController.CompareImport(
                result.data,
                request.branchId);
            Debug.Log(
                $"Localization import comparison created: " +
                $"{comparison.EntryCount} entries, " +
                $"{comparison.ChangedCellCount} changed cells, " +
                $"{comparison.ConflictCellCount} conflicts, " +
                $"{comparison.VersionDifferenceCount} version differences.");
            Context.ImportComparisonView.Show(comparison);
        }
        finally
        {
            SetLoading(false, btnPull, btnPullText);
        }
    }

    private void SetLoading(bool loading, Button activeButton, string text)
    {
        isLoading = loading;
        if (activeButton != null)
        {
            activeButton.text = text;
            activeButton.SetEnabled(!loading);
        }

        btnPush?.SetEnabled(!loading);
        btnPull?.SetEnabled(!loading);
    }
}
}
