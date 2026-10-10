using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace EciFlowConnector.Samples.Editor
{
    /// <summary>在导入后的 Demo 目录中补齐本项目的 Localization 注册，可重复执行。</summary>
    public static class DemoSampleSetup
    {
        private const string MenuPath = "Tools/ECI Flow Connector/Initialize Demo";

        [MenuItem(MenuPath)]
        public static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Initialize Demo", "Please exit Play Mode and wait for compilation to finish.", "OK");
                return;
            }

            try
            {
                var root = FindDemoRoot();
                var locales = FindAssets<Locale>(root, "t:Locale");
                var tables = FindAssets<StringTable>(root, "t:StringTable");
                var collections = FindAssets<StringTableCollection>(root, "t:StringTableCollection");
                var settings = FindAssets<LocalizationSettings>(root, "t:LocalizationSettings");
                Validate(root, locales, tables, collections, settings);

                // 只有目标项目尚未配置 Localization 时才启用示例 Settings。
                var active = LocalizationEditorSettings.ActiveLocalizationSettings;
                var useSampleSettings = active == null;
                if (useSampleSettings)
                    LocalizationEditorSettings.ActiveLocalizationSettings = settings[0];

                AddressableAssetSettingsDefaultObject.GetSettings(true);
                var registered = LocalizationEditorSettings.GetLocales().ToList();
                var addedLocales = 0;
                foreach (var locale in locales)
                {
                    if (registered.Any(item => item.Identifier == locale.Identifier))
                        continue;
                    LocalizationEditorSettings.AddLocale(locale, true);
                    registered.Add(locale);
                    addedLocales++;
                }

                var addedTables = 0;
                foreach (var collection in collections)
                {
                    foreach (var table in tables.Where(item => item.SharedData == collection.SharedData))
                    {
                        if (collection.ContainsTable(table))
                            continue;
                        collection.AddTable(table, true);
                        addedTables++;
                    }
                    // 官方接口负责 Shared Data、表地址和标签，无需复制宿主项目的分组配置。
                    collection.RefreshAddressables(true);
                    EditorUtility.SetDirty(collection);
                }

                if (useSampleSettings)
                {
                    var fallback = registered.First(item => item.Identifier.Code == "en-US");
                    var selectors = settings[0].GetStartupLocaleSelectors();
                    var specific = selectors.OfType<SpecificLocaleSelector>().FirstOrDefault();
                    if (specific == null)
                        selectors.Add(new SpecificLocaleSelector { LocaleId = fallback.Identifier });
                    else
                        specific.LocaleId = fallback.Identifier;
                    LocalizationSettings.ProjectLocale = fallback;
                    EditorUtility.SetDirty(settings[0]);
                }

                AssetDatabase.SaveAssets();
                Verify(locales, tables, collections);
                EditorUtility.DisplayDialog("Initialize Demo",
                    $"Demo initialized.\nLocales added: {addedLocales}\nTables linked: {addedTables}\n" +
                    $"Tables registered: {tables.Count}\n" +
                    (useSampleSettings ? "Demo Localization Settings enabled (fallback: en-US)." :
                        "Existing Localization Settings and language selection preserved.") +
                    "\nAddressables build profiles and Play Mode Script were not changed. " +
                    "Build Addressables separately when using an existing build or preparing a player build.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Demo initialization failed",
                    exception.Message + "\nAny completed registrations are retained. Resolve the issue and run again.", "OK");
            }
        }

        private static string FindDemoRoot()
        {
            // 以导入后的脚本定位资源，允许用户移动目录。多份 Sample 时使用当前选中目录消歧。
            var roots = AssetDatabase.FindAssets("DemoSampleSetup t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    path.EndsWith("/Editor/DemoSampleSetup.cs", StringComparison.Ordinal))
                .Select(path => path.Substring(0, path.Length - "/Editor/DemoSampleSetup.cs".Length))
                .Distinct().ToList();
            var selected = AssetDatabase.GetAssetPath(Selection.activeObject);
            var matches = roots.Where(root => selected == root || selected.StartsWith(root + "/", StringComparison.Ordinal)).ToList();
            if (matches.Count == 1)
                return matches[0];
            if (roots.Count != 1)
                throw new InvalidOperationException("Import the Demo Sample first. If multiple copies exist, select an asset inside the intended Demo folder.");
            return roots[0];
        }

        private static List<T> FindAssets<T>(string root, string filter) where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets(filter, new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null).ToList();
        }

        private static void Validate(string root, List<Locale> locales, List<StringTable> tables,
            List<StringTableCollection> collections, List<LocalizationSettings> settings)
        {
            // 在写入配置前发现缺失资源及冲突，避免覆盖已有翻译表。
            if (settings.Count != 1 || locales.Count == 0 || tables.Count == 0 || collections.Count == 0)
                throw new InvalidOperationException("Demo Localization assets are missing or ambiguous. Reimport the complete Sample including .meta files.");
            if (!locales.Any(locale => locale.Identifier.Code == "en-US"))
                throw new InvalidOperationException("The Demo en-US Locale is missing.");
            if (locales.GroupBy(locale => locale.Identifier.Code).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Duplicate Locale codes exist inside the Demo.");
            foreach (var collection in collections)
            {
                if (collection.SharedData == null)
                    throw new InvalidOperationException($"{collection.name}: Shared Data is missing.");
                var conflicts = LocalizationEditorSettings.GetStringTableCollections()
                    .Any(other => other != collection && other.TableCollectionName == collection.TableCollectionName);
                if (conflicts)
                    throw new InvalidOperationException($"A different collection named '{collection.TableCollectionName}' already exists. Resolve the duplicate before initializing.");
                var candidates = tables.Where(table => table.SharedData == collection.SharedData).ToList();
                if (candidates.GroupBy(table => table.LocaleIdentifier.Code).Any(group => group.Count() > 1))
                    throw new InvalidOperationException($"{collection.name}: duplicate language tables exist.");
                foreach (var table in candidates)
                {
                    var existing = collection.GetTable(table.LocaleIdentifier);
                    if (existing != null && existing != table)
                        throw new InvalidOperationException($"{collection.name}: a different table already exists for {table.LocaleIdentifier}.");
                }
            }
            foreach (var table in tables)
            {
                if (table.SharedData == null || collections.Count(c => c.SharedData == table.SharedData) != 1 ||
                    !locales.Any(locale => locale.Identifier == table.LocaleIdentifier))
                    throw new InvalidOperationException($"{table.name}: matching collection, Shared Data or Locale is missing.");
            }
            var scenePath = root + "/FLowDemoScene.unity";
            if (!File.Exists(scenePath))
                throw new InvalidOperationException("FLowDemoScene.unity is missing from the Demo folder.");
        }

        private static void Verify(List<Locale> locales, List<StringTable> tables, List<StringTableCollection> collections)
        {
            var registered = LocalizationEditorSettings.GetLocales();
            var addressables = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (addressables == null || locales.Any(locale => !registered.Any(item => item.Identifier == locale.Identifier)))
                throw new InvalidOperationException("Locale registration verification failed.");
            foreach (var asset in tables.Cast<UnityEngine.Object>().Concat(collections.Select(c => (UnityEngine.Object)c.SharedData)))
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                if (addressables.FindAssetEntry(guid) == null)
                    throw new InvalidOperationException($"Addressables registration is missing for {asset.name}.");
            }
        }
    }
}
