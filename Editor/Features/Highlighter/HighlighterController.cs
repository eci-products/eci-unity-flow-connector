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
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.Features.Highlighter
{

internal sealed class HighlighterController : FlowConnectorComponent
{
    private readonly UIHighlighter highlighter;

    internal HighlighterController(FlowConnectorContext context) : base(context)
    {
        highlighter = new UIHighlighter();
    }

    internal void Dispose() => OnDisable();
    internal void ModeChanged(ChangeEvent<string> evt) => OnHighlighterModeChanged(evt);
    internal void RefreshSettingsVisibility() => UpdateHighlighterSettingsVisibility();
    internal void SelectBoundUi(string tableName, string entryKey) =>
        SelectBoundLocalizationUI(tableName, entryKey);
    internal UIHighlightMode Mode => highlighter.Mode;
    internal Color BorderColor => highlighter.BorderColor;
    internal float BorderThickness => highlighter.BorderThickness;
    internal Color ArrowColor => highlighter.ArrowColor;
    internal float ArrowSize => highlighter.ArrowSize;

    internal void Clear() => highlighter.Clear();

    internal void SetBorderColor(Color value)
    {
        highlighter.BorderColor = value;
        highlighter.Redraw();
    }

    internal void SetBorderThickness(float value)
    {
        highlighter.BorderThickness = value;
        highlighter.Redraw();
    }

    internal void SetArrowColor(Color value)
    {
        highlighter.ArrowColor = value;
        highlighter.Redraw();
    }

    internal void SetArrowSize(float value)
    {
        highlighter.ArrowSize = value;
        highlighter.Redraw();
    }

    private void OnDisable()
    {
        highlighter.Dispose();
        Context.ScreenshotController.Clear();
        Context.EntryDetailsView.Clear();
    }

    private void OnHighlighterModeChanged(ChangeEvent<string> evt)
    {
        highlighter.Mode = evt.newValue switch
        {
            "Arrow" => UIHighlightMode.Arrow,
            "None" => UIHighlightMode.None,
            _ => UIHighlightMode.Border
        };
        highlighter.Redraw();
        UpdateHighlighterSettingsVisibility();
    }

    private void UpdateHighlighterSettingsVisibility()
    {
        var borderMode = highlighter.Mode == UIHighlightMode.Border;
        var arrowMode = highlighter.Mode == UIHighlightMode.Arrow;
        highlighterBorderSettings?.EnableInClassList("hidden", !borderMode);
        highlighterArrowSettings?.EnableInClassList("hidden", !arrowMode);
    }

    private void SelectBoundLocalizationUI(string tableName, string entryKey)
    {
        highlighter.Clear();

        var tableCollection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        var sharedEntry = tableCollection?.SharedData?.GetEntry(entryKey);
        if (tableCollection == null || sharedEntry == null)
        {
            Debug.LogWarning("Flow Connector Warning: " + $"Could not resolve Localization entry '{entryKey}' in table '{tableName}'.");
            return;
        }

        var tableGuid = tableCollection.SharedData.TableCollectionNameGuid;
        var tableCollectionName = tableCollection.SharedData.TableCollectionName;
        var boundTransforms = new List<Transform>();

        foreach (var localizeEvent in UnityEngine.Object.FindObjectsOfType<LocalizeStringEvent>(true))
        {
            if (localizeEvent == null || localizeEvent.StringReference == null)
                continue;

            var stringReference = localizeEvent.StringReference;
            if (!MatchesTable(stringReference.TableReference, tableGuid, tableCollectionName) ||
                !MatchesEntry(stringReference.TableEntryReference, sharedEntry.Id, entryKey))
            {
                continue;
            }

            var targetCount = localizeEvent.OnUpdateString.GetPersistentEventCount();
            var foundPersistentTarget = false;
            for (var i = 0; i < targetCount; i++)
            {
                var persistentTarget = localizeEvent.OnUpdateString.GetPersistentTarget(i);
                Transform targetTransform = null;
                if (persistentTarget is Component component)
                    targetTransform = component.transform;
                else if (persistentTarget is GameObject gameObject)
                    targetTransform = gameObject.transform;

                if (targetTransform == null)
                    continue;

                foundPersistentTarget = true;
                if (!boundTransforms.Contains(targetTransform))
                    boundTransforms.Add(targetTransform);
            }

            if (!foundPersistentTarget && !boundTransforms.Contains(localizeEvent.transform))
                boundTransforms.Add(localizeEvent.transform);
        }

        if (boundTransforms.Count == 0)
        {
            Debug.LogWarning(
                "Flow Connector Warning: " +
                $"No loaded scene UI is bound to Localization entry '{tableName}/{entryKey}'. " +
                "Enter Play Mode or open the scene containing the LocalizeStringEvent.");
            return;
        }

        Selection.objects = boundTransforms
            .Select(transform => (UnityEngine.Object)transform.gameObject)
            .ToArray();

        var firstTarget = boundTransforms[0];
        EditorGUIUtility.PingObject(firstTarget.gameObject);
        SceneView.lastActiveSceneView?.FrameSelected();
        highlighter.Show(boundTransforms);
    }

    private static bool MatchesTable(TableReference reference, Guid tableGuid, string tableCollectionName)
    {
        switch (reference.ReferenceType)
        {
            case TableReference.Type.Guid:
                return reference.TableCollectionNameGuid == tableGuid;
            case TableReference.Type.Name:
                return string.Equals(
                    reference.TableCollectionName,
                    tableCollectionName,
                    StringComparison.Ordinal);
            default:
                return false;
        }
    }

    private static bool MatchesEntry(TableEntryReference reference, long entryId, string entryKey)
    {
        switch (reference.ReferenceType)
        {
            case TableEntryReference.Type.Id:
                return reference.KeyId == entryId;
            case TableEntryReference.Type.Name:
                return string.Equals(reference.Key, entryKey, StringComparison.Ordinal);
            default:
                return false;
        }
    }
}
}
