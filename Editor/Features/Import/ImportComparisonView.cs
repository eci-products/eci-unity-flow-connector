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
using UnityEngine;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.Features.Import
{

internal sealed class ImportComparisonView : FlowConnectorComponent
{
    internal ImportComparisonView(FlowConnectorContext context) : base(context) { }

    internal void Bind() => BindImportComparisonElements();
    internal void RegisterEvents() => SetupImportComparisonEvents();
    internal void Show(ImportComparison comparison) => ShowImportComparison(comparison);
    internal void Refresh() => RefreshImportComparisonView();

    // 下拉框文本与 Resolution 枚举通过文件底部的转换方法映射。
    private static readonly List<string> ContentResolutionChoices =
        // new List<string> { "Unresolved", "Keep Local", "Use Remote", "Custom" };
        new List<string> { "Unresolved", "Keep Local", "Use Remote" };

    private static readonly List<string> VersionResolutionChoices =
        new List<string> { "Unresolved", "Keep Local Version", "Use Remote Version" };

    private VisualElement importDiffPanel;
    private VisualElement importDiffContent;
    private ScrollView importDiffScroll;
    private Label importDiffSummary;
    private Label importDiffValidation;
    private Button btnImportKeepAllLocal;
    private Button btnImportUseAllRemote;
    private Button btnImportMerge;
    private Button btnImportCancel;
    private Button btnImportClose;


    // 从 UXML 获取 Import 差异面板中的静态控件。
    private void BindImportComparisonElements()
    {
        importDiffPanel = root.Q<VisualElement>("importDiffPanel");
        importDiffContent = root.Q<VisualElement>("importDiffContent");
        importDiffScroll = root.Q<ScrollView>("importDiffScroll");
        importDiffSummary = root.Q<Label>("importDiffSummary");
        importDiffValidation = root.Q<Label>("importDiffValidation");
        btnImportKeepAllLocal = root.Q<Button>("btnImportKeepAllLocal");
        btnImportUseAllRemote = root.Q<Button>("btnImportUseAllRemote");
        btnImportMerge = root.Q<Button>("btnImportMerge");
        btnImportCancel = root.Q<Button>("btnImportCancel");
        btnImportClose = root.Q<Button>("btnImportClose");

    }

    // 注册面板级批量操作、合并和关闭事件。
    private void SetupImportComparisonEvents()
    {
        btnImportKeepAllLocal?.RegisterCallback<ClickEvent>(
            _ =>
            {
                Context.ImportController.KeepAllLocalImportData();
                RefreshImportComparisonView();
            });
        btnImportUseAllRemote?.RegisterCallback<ClickEvent>(
            _ =>
            {
                Context.ImportController.AcceptAllRemoteImportData();
                RefreshImportComparisonView();
            });
        btnImportMerge?.RegisterCallback<ClickEvent>(_ => MergeImportComparisonAndClose());
        btnImportCancel?.RegisterCallback<ClickEvent>(_ => CloseImportComparisonPanel());
        btnImportClose?.RegisterCallback<ClickEvent>(_ => CloseImportComparisonPanel());

    }

    /// <summary>
    /// 显示指定的 Import 差异结果。
    /// </summary>
    private void ShowImportComparison(ImportComparison comparison)
    {
        if (comparison == null || importDiffPanel == null)
            return;

        detailContent?.AddToClassList("hidden");
        // scrollCol.style.height = StyleKeyword.Auto;
        // scrollCol.style.maxHeight = 200;
        importDiffPanel.RemoveFromClassList("hidden");
        RefreshImportComparisonView();
        if (importDiffScroll != null)
        {
            importDiffScroll.schedule.Execute(
                () => importDiffScroll.scrollOffset = Vector2.zero);
        }
        importDiffPanel.schedule.Execute(
            () =>
            {
                if (mainTabContent is ScrollView mainScrollView)
                    mainScrollView.ScrollTo(importDiffPanel);
            });
    }

    // 根据内存中的最新决策重新生成差异列表和统计信息。
    private void RefreshImportComparisonView()
    {
        if (importDiffContent == null)
            return;

        importDiffContent.Clear();
        var comparison = Context.ImportController.CurrentImportComparison;
        if (comparison == null)
        {
            CloseImportComparisonPanel(false);
            return;
        }

        // 不显示内容和版本完全一致的 Entry，减少大型 Table 的界面开销。
        var visibleEntries = comparison.Entries
            .Where(HasVisibleDifference)
            .ToList();

        if (visibleEntries.Count == 0)
        {
            var emptyLabel = new Label("No content or version differences were found.");
            emptyLabel.AddToClassList("empty-state");
            importDiffContent.Add(emptyLabel);
        }
        else
        {
            foreach (var entry in visibleEntries)
                importDiffContent.Add(CreateImportEntryCard(entry));
        }

        importDiffSummary.text =
            $"{visibleEntries.Count} entries; " +
            $"{comparison.ChangedCellCount} changed cells; " +
            $"{comparison.ConflictCellCount} conflicts; " +
            $"{comparison.VersionDifferenceCount} version differences;";

        UpdateImportMergeState();
    }

    // 创建一个 Entry 卡片，包括版本决策、批量操作和各语言差异。
    private VisualElement CreateImportEntryCard(EntryComparison entry)
    {
        var card = new VisualElement();
        card.AddToClassList("import-entry-card");

        var header = new VisualElement();
        header.AddToClassList("import-entry-header");

        var titleRow = new VisualElement();
        titleRow.AddToClassList("import-entry-title-row");
        var title = new Label($"{entry.TableName} / {entry.EntryKey}");
        title.AddToClassList("import-entry-title");
        titleRow.Add(title);
        header.Add(titleRow);

        var actions = new VisualElement();
        actions.AddToClassList("import-entry-actions");

        var keepLocal = new Button(
            () =>
            {
                Context.ImportController.KeepLocalImportEntry(entry.TableName, entry.EntryKey);
                RefreshImportComparisonView();
            })
        {
            text = "Keep Local Entry"
        };
        keepLocal.AddToClassList("import-entry-action-button");
        keepLocal.AddToClassList("import-keep-button");
        actions.Add(keepLocal);

        var useRemote = new Button(
            () =>
            {
                Context.ImportController.UseRemoteImportEntry(entry.TableName, entry.EntryKey);
                RefreshImportComparisonView();
            })
        {
            text = "Use Remote Entry"
        };
        useRemote.AddToClassList("import-entry-action-button");
        useRemote.AddToClassList("import-remote-button");
        actions.Add(useRemote);
        header.Add(actions);

        if (HasVersionDifference(entry))
            header.Add(CreateVersionResolutionRow(entry));

        card.Add(header);

        foreach (var cell in entry.Cells.Where(
                     value => value.State != ContentComparisonState.Unchanged))
        {
            card.Add(CreateImportCellCard(entry, cell));
        }

        return card;
    }

    // 版本号是 Entry 级数据，因此在各语言单元格之前统一处理。
    private VisualElement CreateVersionResolutionRow(EntryComparison entry)
    {
        var row = new VisualElement();
        row.AddToClassList("import-version-row");

        var label = new Label(
            $"Version: \n" + 
            $"{DisplayValue(entry.LocalVersionId, !string.IsNullOrEmpty(entry.LocalVersionId))}\n" +
            $"Remote Version: \n" +
            $"{DisplayValue(entry.RemoteVersionId, !string.IsNullOrEmpty(entry.RemoteVersionId))}\n" +
            $"  ({entry.VersionState})");
        label.AddToClassList("import-version-label");
        row.Add(label);

        var selectedChoice = GetVersionResolutionChoice(entry.VersionResolution);
        var popup = new PopupField<string>(
            VersionResolutionChoices,
            VersionResolutionChoices.IndexOf(selectedChoice));
        popup.AddToClassList("import-version-popup");
        popup.RegisterValueChangedCallback(
            evt =>
            {
                Context.ImportController.SetImportVersionResolution(
                    entry.TableName,
                    entry.EntryKey,
                    GetVersionResolution(evt.newValue));
                UpdateImportMergeState();
            });
        row.Add(popup);
        return row;
    }

    // 创建单个 Locale 的本地/远端内容对照和最终结果选择控件。
    private VisualElement CreateImportCellCard(
        EntryComparison entry,
        CellComparison cell)
    {
        var card = new VisualElement();
        card.AddToClassList("import-cell-card");

        var heading = new VisualElement();
        heading.AddToClassList("import-cell-heading");
        var locale = new Label(cell.LocaleCode);
        locale.AddToClassList("import-locale-label");
        heading.Add(locale);

        var state = new Label(cell.State.ToString());
        state.AddToClassList("import-state-label");
        state.AddToClassList(GetStateClass(cell.State));
        heading.Add(state);
        card.Add(heading);

        var columns = new VisualElement();
        columns.AddToClassList("import-value-columns");
        columns.Add(CreateValueColumn("LOCAL", cell.LocalExists, cell.LocalValue));
        columns.Add(CreateValueColumn("REMOTE", cell.RemoteExists, cell.RemoteValue));
        card.Add(columns);

        var resolutionRow = new VisualElement();
        resolutionRow.AddToClassList("import-resolution-row");
        var resolutionLabel = new Label("Result:");
        resolutionLabel.AddToClassList("import-resolution-label");
        resolutionRow.Add(resolutionLabel);

        var selectedChoice = GetContentResolutionChoice(cell.Resolution);
        var resolutionPopup = new PopupField<string>(
            ContentResolutionChoices,
            ContentResolutionChoices.IndexOf(selectedChoice));
        resolutionPopup.AddToClassList("import-resolution-popup");
        resolutionRow.Add(resolutionPopup);
        card.Add(resolutionRow);

        // Custom预留 只有选择 Custom 时才显示并使用自定义输入框。
        // var customField = new TextField
        // {
        //     multiline = true,
        //     value = cell.CustomValue ?? cell.LocalValue ?? cell.RemoteValue ?? string.Empty
        // };
        // customField.AddToClassList("import-custom-value");
        // customField.EnableInClassList(
        //     "hidden",
        //     cell.Resolution != ValueResolution.Custom);
        // customField.RegisterValueChangedCallback(
        //     evt =>
        //     {
        //         Context.ImportController.SetImportCellResolution(
        //             entry.TableName,
        //             entry.EntryKey,
        //             cell.LocaleCode,
        //             ValueResolution.Custom,
        //             evt.newValue);
        //     });
        // card.Add(customField);

        resolutionPopup.RegisterValueChangedCallback(
            evt =>
            {
                var resolution = GetContentResolution(evt.newValue);
                Context.ImportController.SetImportCellResolution(
                    entry.TableName,
                    entry.EntryKey,
                    cell.LocaleCode,
                    resolution);
                // Custom预留
                //     resolution == ValueResolution.Custom
                //         ? customField.value
                //         : null);
                // customField.EnableInClassList(
                //     "hidden",
                //     resolution != ValueResolution.Custom);
                UpdateImportMergeState();
            });

        return card;
    }

    // 本地和远端文本只读展示，避免误以为编辑它们会修改最终结果。
    private static VisualElement CreateValueColumn(
        string titleText,
        bool exists,
        string value)
    {
        var column = new VisualElement();
        column.AddToClassList("import-value-column");

        var title = new Label(titleText);
        title.AddToClassList("import-value-column-title");
        column.Add(title);

        var field = new TextField
        {
            multiline = true,
            isReadOnly = true,
            value = DisplayValue(value, exists)
        };
        field.AddToClassList("import-value-field");
        if (!exists)
            field.AddToClassList("import-missing-value");
        column.Add(field);
        return column;
    }

    // 未解决项归零前禁用合并按钮，保证写入过程使用完整决策。
    private void UpdateImportMergeState()
    {
        var comparison = Context.ImportController.CurrentImportComparison;
        if (comparison == null)
        {
            btnImportMerge?.SetEnabled(false);
            return;
        }

        var unresolvedCells = comparison.Entries.Sum(
            entry => entry.Cells.Count(
                cell => cell.Resolution == ValueResolution.Unresolved));
        var unresolvedVersions = comparison.Entries.Count(
            entry => entry.VersionResolution == VersionResolution.Unresolved);
        var unresolvedCount = unresolvedCells + unresolvedVersions;

        btnImportMerge?.SetEnabled(unresolvedCount == 0);
        importDiffValidation?.RemoveFromClassList("import-diff-validation-ready");
        importDiffValidation?.RemoveFromClassList("import-diff-validation-error");

        if (unresolvedCount == 0)
        {
            if (importDiffValidation != null)
            {
                importDiffValidation.text = "All differences are resolved. Ready to merge.";
                importDiffValidation.AddToClassList("import-diff-validation-ready");
            }
        }
        else if (importDiffValidation != null)
        {
            importDiffValidation.text =
                $"{unresolvedCells} content and {unresolvedVersions} version differences " +
                "still need a decision.";
        }
    }

    // 应用内存中的最终决策；成功后关闭面板，失败则保留并显示错误。
    private void MergeImportComparisonAndClose()
    {
        var result = Context.ImportController.ApplyImportComparison();
        if (!result.Success)
        {
            if (importDiffValidation != null)
            {
                importDiffValidation.text = string.Join("\n", result.Errors.Take(5));
                importDiffValidation.AddToClassList("import-diff-validation-error");
            }
            return;
        }

        ShowNotification(
            new GUIContent(
                $"Localization merged: {result.AddedCount} added, " +
                $"{result.UpdatedCount} updated, {result.RemovedCount} removed."));
        CloseImportComparisonPanel();
    }

    // Cancel 和右上角关闭按钮都会丢弃当前对比，不修改本地资源。
    private void CloseImportComparisonPanel(bool discardComparison = true)
    {
        importDiffPanel?.AddToClassList("hidden");
        if (importDiffPanel != null)
            importDiffPanel.style.height = StyleKeyword.Null;
        // scrollCol.style.height = StyleKeyword.Auto;
        // scrollCol.style.maxHeight = StyleKeyword.Auto;
        importDiffContent?.Clear();
        if (discardComparison)
            Context.ImportController.DiscardImportComparison();
    }

    private static bool HasVisibleDifference(EntryComparison entry)
    {
        return HasVersionDifference(entry) ||
               entry.Cells.Any(
                   cell => cell.State != ContentComparisonState.Unchanged);
    }

    private static bool HasVersionDifference(EntryComparison entry)
    {
        return entry.VersionState != VersionComparisonState.Same &&
               entry.VersionState != VersionComparisonState.Untracked;
    }

    private static string DisplayValue(string value, bool exists)
    {
        return exists ? value ?? string.Empty : "(missing)";
    }

    private static string GetStateClass(ContentComparisonState state)
    {
        switch (state)
        {
            case ContentComparisonState.Conflict:
                return "import-state-conflict";
            case ContentComparisonState.Changed:
                return "import-state-changed";
            case ContentComparisonState.LocalOnly:
                return "import-state-local";
            case ContentComparisonState.RemoteOnly:
                return "import-state-remote";
            default:
                return string.Empty;
        }
    }

    // 以下方法负责界面文本与内部枚举之间的转换。
    private static string GetContentResolutionChoice(ValueResolution resolution)
    {
        switch (resolution)
        {
            case ValueResolution.KeepLocal:
                return "Keep Local";
            case ValueResolution.UseRemote:
                return "Use Remote";
            // Custom预留
            // case ValueResolution.Custom:
            //     return "Custom";
            default:
                return "Unresolved";
        }
    }

    private static ValueResolution GetContentResolution(string choice)
    {
        switch (choice)
        {
            case "Keep Local":
                return ValueResolution.KeepLocal;
            case "Use Remote":
                return ValueResolution.UseRemote;
            // Custom预留
            // case "Custom":
            //     return ValueResolution.Custom;
            default:
                return ValueResolution.Unresolved;
        }
    }

    private static string GetVersionResolutionChoice(
        VersionResolution resolution)
    {
        switch (resolution)
        {
            case VersionResolution.KeepLocal:
                return "Keep Local Version";
            case VersionResolution.UseRemote:
                return "Use Remote Version";
            default:
                return "Unresolved";
        }
    }

    private static VersionResolution GetVersionResolution(string choice)
    {
        switch (choice)
        {
            case "Keep Local Version":
                return VersionResolution.KeepLocal;
            case "Use Remote Version":
                return VersionResolution.UseRemote;
            default:
                return VersionResolution.Unresolved;
        }
    }
}
}
