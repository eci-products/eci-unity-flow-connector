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
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.Features.TableBrowser
{

internal sealed class TableBrowserView : FlowConnectorComponent
{
    private readonly Dictionary<EntryId, TableRowBinding> rowBindings = new();

    internal TableBrowserView(FlowConnectorContext context) : base(context) { }

    /// <summary>
    /// 从 Localization 工程设置中读取全部 String Table，并更新多选下拉框。
    /// </summary>
    internal void RefreshDropdownOptions()
    {
        if (tableDropdown == null)
            return;

        tableDropdown.SetOptions(Context.LocalizationRepository.GetStringTableNames());
    }

    internal void SelectionChanged(HashSet<string> selected) => OntableSelectChanged(selected);

    // 仅同步已渲染控件的数据；结构变化时才回退为完整重绘。
    internal void Refresh() => RefreshData();
    internal void RefreshData() => RefreshRenderedTableData();
    internal void ClearSelection() => ClearSelectedEntry();

    private void OntableSelectChanged(HashSet<string> select)
    {
        Context.Highlighter.Clear();
        if (tableArea.ClassListContains("hidden"))
        {
            tableArea.RemoveFromClassList("hidden");
        }
        LoadTableData(select);
        ClearSelectedEntry();
    }

    private void LoadTableData(HashSet<string> tableNames)
    {
        if (tableNames.Count <= 0)
            return;

        if (tableHeader == null) return;
        tableHeaderData.Clear();
        tableData.Clear();
        tableHeader.Clear();
        selectedTableData.Clear();
        tableToggleGroup.Clear();
        rowBindings.Clear();

        var row = new VisualElement();
        row.AddToClassList("header-row");
        var tableLabel = new Toggle("Table");
        tableLabel.RegisterValueChangedCallback(OnToggleSelectAll);
        tableLabel.style.flexDirection = FlexDirection.RowReverse;
        tableLabel.style.maxWidth = 200;
        tableToggleHeader = tableLabel;
        row.Add(tableLabel);
        row.AddToClassList("header-row");
        var keyLabel = new Label("Key");
        keyLabel.AddToClassList("header-key-cell");
        row.Add(keyLabel);

        int pos = 2;
        foreach (var tableName in tableNames)
        {
            var tableCollection = Context.LocalizationRepository.GetStringTableCollection(tableName);
            if (tableCollection == null)
            {
                Debug.LogError("Flow Connector Warning: " +  $"Table collection '{tableName}' not found!");
                return;
            }
            tableData[tableName] = new();
            var locales = Context.LocalizationRepository.GetAvailableLocales();
            foreach (var locale in locales)
            {
                var stringTable = tableCollection.GetTable(locale.Identifier) as StringTable;
                if (stringTable == null) continue;

                var localeKey = locale.Identifier.Code;
                if (!tableHeaderData.ContainsKey(localeKey))
                {
                    tableHeaderData[localeKey] = pos++;
                    var valueLabel = new Label(locale.LocaleName);
                    valueLabel.AddToClassList("header-cell");
                    row.Add(valueLabel);
                }

                foreach (var entry in stringTable.SharedData.Entries)
                {
                    var tableEntry = stringTable.GetEntry(entry.Key);
                    if (!tableData[tableName].ContainsKey(entry.Key))
                        tableData[tableName].Add(entry.Key, new());
                    tableData[tableName][entry.Key][localeKey] = tableEntry?.Value ?? string.Empty;
                }
            }
        }

        tableHeader.Add(row);
        RefreshTableDisplay();
    }

    private void RefreshTableDisplay()
    {
        if (tableContent == null) return;

        tableContent.Clear();
        tableToggleGroup.Clear();
        rowBindings.Clear();
        NormalizeSelectedEntries();
        // 读取本地基线。
        var localDifferenceContext = Context.ImportController.GetLocalContext();

        if (tableData.Count == 0)
        {
            var emptyLabel = new Label("No table data loaded.");
            emptyLabel.AddToClassList("instruction-text");
            tableContent.Add(emptyLabel);
            return;
        }

        foreach (var table in tableData)
        {
            foreach (var entry in table.Value)
            {
                var row = new VisualElement();
                row.AddToClassList("table-row");
                row.tooltip = $"Click to locate UI elements bound to '{entry.Key}'.";
                // if (tableName == selectedEntryTableName && entry.Key == selectedEntryKey)
                //     row.AddToClassList("table-row-selected");
                row.RegisterCallback<ClickEvent>(_ => OnTableEntryClicked(table.Key, entry.Key, row));

                var tableLabel = new Toggle(table.Key);
                tableLabel.style.flexDirection = FlexDirection.RowReverse;
                tableLabel.style.maxWidth = 200;
                tableLabel.SetValueWithoutNotify(IsEntrySelected(table.Key, entry.Key));
                tableLabel.RegisterCallback<ChangeEvent<bool>>(evt =>
                    {
                        bool allOn = tableToggleGroup.All(toggle => toggle.value);
                        tableToggleHeader.SetValueWithoutNotify(allOn);
                        if (!selectedTableData.ContainsKey(table.Key))
                            selectedTableData.Add(table.Key, new());
                        if (evt.newValue)
                        {
                            if (!selectedTableData[table.Key].Contains(entry.Key))
                                selectedTableData[table.Key].Add(entry.Key);
                        }
                        else
                        {
                            if (selectedTableData[table.Key].Contains(entry.Key))
                                selectedTableData[table.Key].Remove(entry.Key);
                        }

                    });

                tableToggleGroup.Add(tableLabel);
                row.Insert(0, tableLabel);
                var rowBinding = new TableRowBinding(tableLabel);

                var keyLabel = new Label(entry.Key);
                keyLabel.AddToClassList("key-cell");
                row.Insert(1, keyLabel);


                foreach (var tablelocale in tableHeaderData)
                {
                    var keyCellContainer = new VisualElement();
                    keyCellContainer.style.flexDirection = FlexDirection.Row;
                    var valueLabel = new Label();
                    valueLabel.AddToClassList("table-cell");
                    if (entry.Value.ContainsKey(tablelocale.Key))
                        valueLabel.text = entry.Value[tablelocale.Key];
                    keyCellContainer.Add(valueLabel);
                    var localDifference = Context.ImportController.GetLocalResult(
                        localDifferenceContext,
                        table.Key,
                        entry.Key,
                        tablelocale.Key,
                        entry.Value.TryGetValue(tablelocale.Key, out var currentValue)
                            ? currentValue
                            : string.Empty);
                    var statusLabel = new Label(localDifference.Text);
                    statusLabel.AddToClassList("table-cell-label");
                    statusLabel.AddToClassList(localDifference.ClassName);
                    statusLabel.tooltip = localDifference.Tooltip;
                    keyCellContainer.Add(statusLabel);
                    row.Insert(tablelocale.Value, keyCellContainer);
                    rowBinding.Cells.Add(
                        tablelocale.Key,
                        new TableCellBinding(
                            valueLabel,
                            statusLabel,
                            localDifference.ClassName));
                }
                rowBindings[new EntryId(table.Key, entry.Key)] = rowBinding;
                tableContent.Add(row);
            }
        }

        UpdateHeaderToggleState();
    }

    private void RefreshRenderedTableData()
    {
        if (tableContent == null)
            return;

        NormalizeSelectedEntries();
        if (!RenderedStructureMatchesData())
        {
            RefreshTableDisplay();
            return;
        }

        var localDifferenceContext = Context.ImportController.GetLocalContext();
        foreach (var table in tableData)
        {
            foreach (var entry in table.Value)
            {
                var entryId = new EntryId(table.Key, entry.Key);
                var rowBinding = rowBindings[entryId];
                rowBinding.Toggle.SetValueWithoutNotify(
                    IsEntrySelected(table.Key, entry.Key));

                foreach (var locale in tableHeaderData.Keys)
                {
                    var cellBinding = rowBinding.Cells[locale];
                    var value = entry.Value.TryGetValue(locale, out var currentValue)
                        ? currentValue ?? string.Empty
                        : string.Empty;
                    cellBinding.ValueLabel.text = value;

                    var localDifference = Context.ImportController.GetLocalResult(
                        localDifferenceContext,
                        table.Key,
                        entry.Key,
                        locale,
                        value);
                    cellBinding.ApplyDifference(localDifference);
                }
            }
        }

        UpdateHeaderToggleState();
    }

    private bool RenderedStructureMatchesData()
    {
        var expectedRowCount = tableData.Sum(table => table.Value.Count);
        if (rowBindings.Count != expectedRowCount)
            return false;

        foreach (var table in tableData)
        {
            foreach (var entry in table.Value)
            {
                if (!rowBindings.TryGetValue(
                        new EntryId(table.Key, entry.Key),
                        out var rowBinding) ||
                    rowBinding.Cells.Count != tableHeaderData.Count ||
                    tableHeaderData.Keys.Any(locale => !rowBinding.Cells.ContainsKey(locale)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsEntrySelected(string tableName, string entryKey)
    {
        return selectedTableData.TryGetValue(tableName, out var selectedEntries) &&
               selectedEntries.Contains(entryKey);
    }

    private void NormalizeSelectedEntries()
    {
        foreach (var tableName in selectedTableData.Keys.ToList())
        {
            if (!tableData.TryGetValue(tableName, out var entries))
            {
                selectedTableData.Remove(tableName);
                continue;
            }

            selectedTableData[tableName].RemoveAll(
                entryKey => !entries.ContainsKey(entryKey));
        }
    }

    private void UpdateHeaderToggleState()
    {
        tableToggleHeader?.SetValueWithoutNotify(
            tableToggleGroup.Count > 0 && tableToggleGroup.All(toggle => toggle.value));
    }

    private void OnTableEntryClicked(string tableName, string entryKey, VisualElement selectedRow)
    {
        Context.Session.SelectEntry(new EntryId(tableName, entryKey));

        tableContent?
            .Query<VisualElement>(className: "table-row-selected")
            .ForEach(row => row.RemoveFromClassList("table-row-selected"));
        selectedRow?.AddToClassList("table-row-selected");

        detailContent?.RemoveFromClassList("hidden");
        // scrollCol.style.maxHeight = 200;

    }

    private void ClearSelectedEntry()
    {
        Context.Session.ClearSelectedEntry();
    }

    private void OnToggleSelectAll(ChangeEvent<bool> evt)
    {
        foreach (var toggle in tableToggleGroup)
        {
            bool oldValue = toggle.value;
            toggle.SetValueWithoutNotify(evt.newValue);
            using var changeEvt = ChangeEvent<bool>.GetPooled(oldValue, evt.newValue);
            changeEvt.target = toggle;
            toggle.SendEvent(changeEvt);
        }
    }

    private sealed class TableRowBinding
    {
        internal TableRowBinding(Toggle toggle)
        {
            Toggle = toggle;
        }

        internal Toggle Toggle { get; }
        internal Dictionary<string, TableCellBinding> Cells { get; } =
            new Dictionary<string, TableCellBinding>(System.StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TableCellBinding
    {
        private string statusClassName;

        internal TableCellBinding(
            Label valueLabel,
            Label statusLabel,
            string initialStatusClassName)
        {
            ValueLabel = valueLabel;
            StatusLabel = statusLabel;
            statusClassName = initialStatusClassName;
        }

        internal Label ValueLabel { get; }
        internal Label StatusLabel { get; }

        internal void ApplyDifference(ImportController.LocalDifferenceResult difference)
        {
            if (!string.IsNullOrEmpty(statusClassName))
                StatusLabel.RemoveFromClassList(statusClassName);

            StatusLabel.text = difference.Text;
            StatusLabel.tooltip = difference.Tooltip;
            statusClassName = difference.ClassName;
            if (!string.IsNullOrEmpty(statusClassName))
                StatusLabel.AddToClassList(statusClassName);
        }
    }
}
}
