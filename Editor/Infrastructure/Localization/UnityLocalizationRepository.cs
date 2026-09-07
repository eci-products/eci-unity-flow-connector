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
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// 集中封装对 Unity Localization 编辑器数据的读取，避免界面层直接访问插件静态 API。
/// </summary>
namespace EciFlowConnector.Editor.Infrastructure.Localization
{

internal sealed class UnityLocalizationRepository
{
    internal IEnumerable<string> GetStringTableNames()
    {
        return LocalizationEditorSettings.GetStringTableCollections()
            .Where(collection => collection != null)
            .Select(collection => collection.name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, System.StringComparer.OrdinalIgnoreCase);
    }

    internal StringTableCollection GetStringTableCollection(string tableName)
    {
        return LocalizationEditorSettings.GetStringTableCollection(tableName);
    }

    internal List<Locale> GetAvailableLocales()
    {
        var locales = new List<Locale>(LocalizationSettings.AvailableLocales.Locales);
        locales.Sort((left, right) =>
            string.Compare(left.Identifier.Code, right.Identifier.Code, System.StringComparison.Ordinal));
        return locales;
    }
}
}
