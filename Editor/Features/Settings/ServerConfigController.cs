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
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.Features.Settings
{

internal sealed class ServerConfigController : FlowConnectorComponent
{
    internal ServerConfigController(FlowConnectorContext context) : base(context) { }

    internal void LoadSavedConfigs() => LoadServerConfigs();
    internal Task<bool> LoadConfigAsync(string configName) => LoadConfigByName(configName);
    internal void BranchChanged(ChangeEvent<string> evt) => OnBranchChanged(evt);
    internal void RefreshList() => RefreshConfigList();
    internal void AddConfig() => StartAddingConfig();
    internal void PasteConfigFromClipboard() => PasteClipboardConfig();
    internal void TestConnectConfig() => TestConnect();
    internal void SaveCurrentConfig() => SaveConfig();
    internal void DeleteCurrentConfig() => DeleteConfig();
    internal void CancelEditing() => CancelConfigEdit();

    private enum ServerConnectionState
    {
        NotConnected,
        Connecting,
        Connected,
        Failed
    }

    private readonly Dictionary<string, ServerConnectionState> serverConnectionStates = new();

    private void ResetServerConnectionStates()
    {
        foreach (var key in serverConnectionStates.Keys.ToList())
        {
            serverConnectionStates[key] = ServerConnectionState.NotConnected;
        }
    }

    private async Task<bool> LoadConfigByName(string configName)
    {
        if (string.IsNullOrEmpty(configName)) return false;

        var config = serverConfigs.FirstOrDefault(c => c.Name == configName);
        if (config == null) return false;

        ResetServerConnectionStates();
        serverConnectionStates[configName] = ServerConnectionState.Connecting;
        RefreshConfigList();

        if (!lastSelectConfig.Equals(configName))
        {
            lastSelectConfig = configName;
        }

        apiKey = config.ApiKey;
        selectedSourceLocaleCode = config.SourceLanguageCode;
        flowUrlPrefix = config.HookUrl;

        EditorPrefs.SetString(SELECTED_CONFIG_PREF_KEY, configName);
        var connected = await InitConnect();
        serverConnectionStates[configName] = connected
            ? ServerConnectionState.Connected
            : ServerConnectionState.Failed;
        RefreshConfigList();
        return connected;
    }

    private void OnBranchChanged(ChangeEvent<string> evt)
    {
        selectedBranch = evt.newValue ?? string.Empty;
        Context.TableBrowser.Refresh();
    }

    //仅用于尝试连接
    private async Task TestConnectFlow(string flowUrl, string apiKey)
    {
        try
        {
            var identifiersUrl = flowUrl + CMS_URL_PATH + INIT_CONNECT_METHOD;
            var response = await PostAsyncWithToken(identifiersUrl, new { }, apiKey);

            var result = JsonConvert.DeserializeAnonymousType<Result<GameInitResponseDTO>>(response, new());
            if (result != null && result.code == 0 && result.data?.branch != null)
            {
                EditorUtility.DisplayDialog(
                "FlowConfig",
                $"Connect Success!",
                "OK");
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog(
                "FlowConfig",
                $"Failed to load identifiers: {ex}",
                "OK");
        }
        btnTestConnect.text = "Test Connect";
        btnTestConnect.SetEnabled(true);
    }

    private async Task<bool> InitConnect()
    {
        var hasBranch = false;
        branchMap.Clear();
        branchMap.Add("", "");

        if (!string.IsNullOrEmpty(flowUrlPrefix))
        {
            try
            {
                var identifiersUrl = flowUrlPrefix + CMS_URL_PATH + INIT_CONNECT_METHOD;
                var response = await PostAsync(identifiersUrl, new { });

                var result = JsonConvert.DeserializeAnonymousType<Result<GameInitResponseDTO>>(response, new());
                if (result != null && result.code == 0 && result.data?.branch != null)
                {
                    foreach (var branch in result.data.branch)
                    {
                        if (string.IsNullOrWhiteSpace(branch.Value)) continue;

                        if (!branchMap.ContainsKey(branch.Value))
                            branchMap.Add(branch.Value, branch.Key);
                        else
                            branchMap[branch.Value] = branch.Key;

                        hasBranch = true;
                    }
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                "FlowConfig",
                $"Failed to load identifiers: {ex}",
                "OK");
            }
        }

        if (branchDropdown != null && branchMap.Count > 0)
        {
            var branchChoices = branchMap.Keys.ToList();
            branchDropdown.choices = branchChoices;
            var mainIndex = branchChoices.FindIndex(
                branchName => string.Equals(
                    branchName,
                    "main",
                    StringComparison.OrdinalIgnoreCase));
            branchDropdown.index = mainIndex >= 0 ? mainIndex : 0;
        }

        return hasBranch;
    }

    private async void ConnectConfig(int index)
    {
        if (index < 0 || index >= serverConfigs.Count) return;

        var configName = serverConfigs[index].Name;

        await LoadConfigByName(configName);
    }

    private void RefreshConfigList()
    {
        if (configListContent == null) return;
        configListContent.Clear();
        if (serverConfigs.Count == 0)
        {
            var emptyLabel = new Label("No server configurations.\nClick '+ Add New Config' to create one.");
            emptyLabel.AddToClassList("empty-state");
            configListContent.Add(emptyLabel);
            return;
        }
        for (int i = 0; i < serverConfigs.Count; i++)
        {
            var config = serverConfigs[i];
            var index = i;
            var item = new VisualElement();
            item.AddToClassList("config-list-item");
            var info = new VisualElement();
            info.AddToClassList("config-item-info");
            var nameLabel = new Label(config.Name);
            nameLabel.AddToClassList("config-item-name");
            info.Add(nameLabel);
            var urlLabel = new Label(TruncateUrl(config.HookUrl));
            urlLabel.AddToClassList("config-item-url");
            info.Add(urlLabel);
            item.Add(info);

            var actions = new VisualElement();
            actions.AddToClassList("config-item-actions");

            var state = serverConnectionStates.TryGetValue(config.Name, out var connectionState)
                ? connectionState
                : ServerConnectionState.NotConnected;
            var connectBtn = new Button(() => ConnectConfig(index));
            connectBtn.AddToClassList("config-item-connect-button");
            switch (state)
            {
                case ServerConnectionState.Connecting:
                    connectBtn.text = "Connecting...";
                    connectBtn.AddToClassList("config-connect-connecting");
                    connectBtn.SetEnabled(false);
                    break;
                case ServerConnectionState.Connected:
                    connectBtn.text = "Connected";
                    connectBtn.AddToClassList("config-connect-connected");
                    break;
                case ServerConnectionState.Failed:
                    connectBtn.text = "Failed";
                    connectBtn.AddToClassList("config-connect-failed");
                    break;
                default:
                    connectBtn.text = "Connect";
                    break;
            }
            actions.Add(connectBtn);

            var editBtn = new Button(() => StartEditingConfig(index));
            editBtn.text = "Edit";
            editBtn.AddToClassList("config-item-edit-button");
            actions.Add(editBtn);
            item.Add(actions);
            configListContent.Add(item);
        }
    }

    private string TruncateUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "(no URL)";
        if (url.Length <= 40) return url;
        return url.Substring(0, 37) + "...";
    }

    private void StartAddingConfig()
    {
        editingConfigIndex = -1;
        ClearConfigFields();
        ShowConfigEditor(true);
        btnDeleteConfig?.SetEnabled(false);
    }

    private void StartEditingConfig(int index)
    {
        if (index < 0 || index >= serverConfigs.Count) return;
        editingConfigIndex = index;
        var config = serverConfigs[index];
        if (configNameField != null) configNameField.value = config.Name;
        if (configApiKeyField != null) configApiKeyField.value = config.ApiKey;
        if (configHookUrlField != null) configHookUrlField.value = config.HookUrl;
        ShowConfigEditor(true);
        btnDeleteConfig?.SetEnabled(true);
    }

    private void PasteClipboardConfig()
    {
        var clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard))
        {
            EditorUtility.DisplayDialog(
                "Paste Configuration",
                "The clipboard is empty.",
                "OK");
            return;
        }

        string endpointUrl = null;
        string accessToken = null;
        string configName = null;
        var lines = clipboard.Split(
            new[] { "\r\n", "\n", "\r" },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim().Trim('“', '”', '"');
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var label = line.Substring(0, separatorIndex).Trim();
            var value = line.Substring(separatorIndex + 1)
                .Trim()
                .Trim('“', '”', '"');

            if (string.Equals(label, "API Endpoint URL", StringComparison.OrdinalIgnoreCase))
                endpointUrl = value;
            else if (string.Equals(label, "API Access Token", StringComparison.OrdinalIgnoreCase))
                accessToken = value;
            else if (string.Equals(label, "Name", StringComparison.OrdinalIgnoreCase))
                configName = value;
        }

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(endpointUrl))
            missingFields.Add("API Endpoint URL");
        if (string.IsNullOrWhiteSpace(accessToken))
            missingFields.Add("API Access Token");
        if (string.IsNullOrWhiteSpace(configName))
            missingFields.Add("Name");

        if (missingFields.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Paste Configuration",
                $"Clipboard format is invalid. Missing: {string.Join(", ", missingFields)}.",
                "OK");
            return;
        }

        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp &&
             endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            EditorUtility.DisplayDialog(
                "Paste Configuration",
                "API Endpoint URL must be a valid HTTP or HTTPS URL.",
                "OK");
            return;
        }

        if (configNameField != null)
            configNameField.value = configName;
        if (configHookUrlField != null)
            configHookUrlField.value = endpointUrl;
        if (configApiKeyField != null)
            configApiKeyField.value = accessToken;

        ShowNotification(new GUIContent("Configuration pasted from clipboard."));
    }
    private async void TestConnect()
    {
        btnTestConnect.text = "Connecting...";
        btnTestConnect.SetEnabled(false);
        var apiKeyValue = configApiKeyField?.value?.Trim() ?? "";
        var hookUrl = configHookUrlField?.value?.Trim() ?? "";
        await TestConnectFlow(hookUrl, apiKeyValue);
    }

    private void SaveConfig()
    {
        var name = configNameField?.value?.Trim() ?? "";
        var apiKeyValue = configApiKeyField?.value?.Trim() ?? "";
        var hookUrl = configHookUrlField?.value?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            EditorUtility.DisplayDialog("Validation Error", "Name is required.", "OK");
            return;
        }
        var existingIndex = serverConfigs.FindIndex(c => c.Name == name);
        if (existingIndex >= 0 && existingIndex != editingConfigIndex)
        {
            EditorUtility.DisplayDialog("Validation Error", "A config with this name already exists.", "OK");
            return;
        }
        var config = new ServerConfig { Name = name, ApiKey = apiKeyValue, HookUrl = hookUrl };
        if (editingConfigIndex >= 0)
        {
            var oldName = serverConfigs[editingConfigIndex].Name;
            serverConfigs[editingConfigIndex] = config;
            serverConnectionStates.Remove(oldName);
        }
        else serverConfigs.Add(config);
        SaveServerConfigs();
        ShowConfigEditor(false);
        RefreshConfigList();
    }

    private void DeleteConfig()
    {
        if (editingConfigIndex < 0 || editingConfigIndex >= serverConfigs.Count) return;
        var config = serverConfigs[editingConfigIndex];
        if (!EditorUtility.DisplayDialog("Confirm Delete", $"Delete config '{config.Name}'?", "Delete", "Cancel")) return;
        serverConfigs.RemoveAt(editingConfigIndex);
        serverConnectionStates.Remove(config.Name);
        SaveServerConfigs();
        ShowConfigEditor(false);
        RefreshConfigList();
    }

    private void CancelConfigEdit() { ShowConfigEditor(false); }

    private void ClearConfigFields()
    {
        if (configNameField != null) configNameField.value = "";
        if (configApiKeyField != null) configApiKeyField.value = "";
        if (configHookUrlField != null) configHookUrlField.value = "";
    }

    private void ShowConfigEditor(bool show)
    {
        if (configEditorSection == null) return;
        if (show) configEditorSection.RemoveFromClassList("hidden");
        else { configEditorSection.AddToClassList("hidden"); editingConfigIndex = -1; }
    }

    private void LoadServerConfigs()
    {
        var json = EditorPrefs.GetString(CONFIGS_PREF_KEY, "[]");
        try { serverConfigs = JsonConvert.DeserializeObject<List<ServerConfig>>(json) ?? new List<ServerConfig>(); }
        catch { serverConfigs = new List<ServerConfig>(); }
    }

    private void SaveServerConfigs()
    {
        var json = JsonConvert.SerializeObject(serverConfigs);
        EditorPrefs.SetString(CONFIGS_PREF_KEY, json);
    }
}
}
