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
using UnityEditor;
using UnityEngine;

/// <summary>
/// ECI Flow Connector 的 Unity EditorWindow 入口。
/// </summary>
namespace EciFlowConnector.Editor.App
{

public sealed class FlowConnectorEditorWindow : EditorWindow
{
    private FlowConnectorApplication application;

    [MenuItem("Tools/ECI Flow Connector")]
    public static void ShowWindow()
    {
        var window = GetWindow<FlowConnectorEditorWindow>("ECI Flow Connector");
        window.minSize = new Vector2(500, 800);
        window.Show();
    }

    public void CreateGUI()
    {
        application?.Dispose();
        application = new FlowConnectorApplication();
        application.Initialize(this, rootVisualElement);
    }

    private void OnDisable()
    {
        application?.Dispose();
        application = null;
    }
}
}
