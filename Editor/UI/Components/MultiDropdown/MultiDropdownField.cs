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
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.UI.Components.MultiDropdown
{
    public partial class MultiDropdownField : VisualElement
    {
#if false
        public string Label { get; set; } = "多选下拉框";
#endif
        public string Label { get; set; } = "\u591a\u9009\u4e0b\u62c9\u6846";
        public string Options { get; set; } = "";

        public readonly List<string> options = new();
        public readonly HashSet<string> selected = new();

        private Button button;
        private Label labelText;

        public event System.Action<HashSet<string>> OnSelectionChanged;

        public MultiDropdownField()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/net.eciflow.unity/Editor/UI/Components/MultiDropdown/MultiDropdown.uxml");
            tree.CloneTree(this);

            labelText = this.Q<Label>("label");
            button = this.Q<Button>("dropdownBtn");

            button.clicked += OpenPopup;
            RegisterCallback<AttachToPanelEvent>(OnAttach);
        }

        private void OnAttach(AttachToPanelEvent _)
        {
            if (!string.IsNullOrEmpty(Options))
            {
                options.Clear();
                options.AddRange(Options.Split(',').Select(s => s.Trim()));
            }

            labelText.text = Label;
            RefreshButton();
        }

        /// <summary>
        /// 替换下拉框选项，并清理已经不存在的选择。
        /// </summary>
        public void SetOptions(IEnumerable<string> opts)
        {
            options.Clear();
            if (opts != null)
            {
                options.AddRange(opts
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .Distinct(System.StringComparer.OrdinalIgnoreCase));
            }

            if (selected.RemoveWhere(option => !options.Contains(
                    option,
                    System.StringComparer.OrdinalIgnoreCase)) > 0)
            {
                RefreshButton();
            }
        }

        public IEnumerable<string> GetSelected() => selected;
        public void SetSelected(IEnumerable<string> sel)
        {
            selected.Clear();
            foreach (var s in sel) selected.Add(s);
            // RefreshButton();
        }

        public void addSelect(string sel)
        {
            selected.Add(sel);
            // RefreshButton();
        }

        public void removeSelect(string sel)
        {
            selected.Remove(sel);
            // RefreshButton();
        }


        // 处理 OpenPopup 对应的数据或操作。
        private void OpenPopup()
        {
            var rect = button.worldBound;
            var data = new MultiDropdownData(options, selected);
            var popup = new MultiDropdownPopup(this, new Vector2(rect.width, 260));

            UnityEditor.PopupWindow.Show(rect, popup);
        }

        private void RefreshButton()
        {
            button.text = selected.Count == 0
                ? "Unselected"
                : string.Join(", ", selected);
            OnSelectionChanged?.Invoke(selected);
        }

        // 处理 OnPopupClosed 对应的数据或操作。
        internal void OnPopupClosed()
        {
            RefreshButton();
        }
    }
}
