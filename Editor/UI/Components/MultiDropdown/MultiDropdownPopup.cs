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
using UnityEditor;
using UnityEngine;

namespace EciFlowConnector.Editor.UI.Components.MultiDropdown
{
    public class MultiDropdownPopup : PopupWindowContent
    {
        private readonly MultiDropdownField parent;
        private readonly Vector2 size;
        private string search = "";

        public MultiDropdownPopup(MultiDropdownField parent, Vector2 size)
        {
            this.parent = parent;
            this.size = size;
        }

        public override Vector2 GetWindowSize() => size;

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.BeginVertical();

            // 搜索当前候选项。
            search = EditorGUILayout.TextField("Search", search);
            EditorGUILayout.Space(2);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                foreach (var option in parent.options)
                    parent.selected.Add(option);
            }
            if (GUILayout.Button("Clean"))
            {
                parent.selected.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            using (var scope = new EditorGUILayout.ScrollViewScope(Vector2.zero))
            {
                foreach (var option in parent.options)
                {
                    if (!string.IsNullOrEmpty(search) &&
                        !option.ToLower().Contains(search.ToLower()))
                        continue;

                    bool on = parent.selected.Contains(option);
                    bool newOn = EditorGUILayout.ToggleLeft(option, on);
                    if (newOn) parent.addSelect(option);
                    else parent.removeSelect(option);
                }
            }

            EditorGUILayout.EndVertical();
        }

        public override void OnClose()
        {
            parent.OnPopupClosed();
        }
    }

    // 保存多选下拉弹窗使用的候选项和选中项。
    public class MultiDropdownData
    {
        public readonly List<string> Options;
        public readonly HashSet<string> Selected;

        public MultiDropdownData(List<string> options, HashSet<string> selected)
        {
            Options = options;
            Selected = selected;
        }
    }
}
