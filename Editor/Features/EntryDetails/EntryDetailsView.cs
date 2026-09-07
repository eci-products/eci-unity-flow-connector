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
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Entry 详情区域，只负责字段绑定和显示。
/// </summary>
namespace EciFlowConnector.Editor.Features.EntryDetails
{

internal sealed class EntryDetailsView
{
    private readonly FlowConnectorSession session;
    private readonly EntryMetadataRepository metadataRepository;

    private IntegerField charLimitField;
    private TextField commentField;
    private bool updatingFields;

    internal EntryDetailsView(
        FlowConnectorSession session,
        EntryMetadataRepository metadataRepository)
    {
        this.session = session;
        this.metadataRepository = metadataRepository;
    }

    internal void Bind(VisualElement root)
    {
        charLimitField = root.Q<IntegerField>("charLimitField");
        commentField = root.Q<TextField>("commentField");
        Refresh();
    }

    internal void RegisterEvents()
    {
        charLimitField?.RegisterValueChangedCallback(OnCharLimitChanged);
        commentField?.RegisterValueChangedCallback(OnCommentChanged);
    }

    internal void Refresh()
    {
        var entryId = session.SelectedEntry;
        var metadata = entryId.HasValue
            ? metadataRepository.Get(entryId.Value)
            : null;

        updatingFields = true;
        charLimitField?.SetValueWithoutNotify(metadata?.CharLimit ?? 0);
        commentField?.SetValueWithoutNotify(metadata?.Comment ?? string.Empty);
        updatingFields = false;

        charLimitField?.SetEnabled(entryId.HasValue);
        commentField?.SetEnabled(entryId.HasValue);
    }

    internal void Clear()
    {
        updatingFields = true;
        charLimitField?.SetValueWithoutNotify(0);
        commentField?.SetValueWithoutNotify(string.Empty);
        updatingFields = false;
    }

    private void OnCharLimitChanged(ChangeEvent<int> evt)
    {
        if (updatingFields || !session.SelectedEntry.HasValue)
            return;

        var normalizedValue = Math.Max(0, evt.newValue);
        if (normalizedValue != evt.newValue)
            charLimitField.SetValueWithoutNotify(normalizedValue);

        metadataRepository.SetCharLimit(
            session.SelectedEntry.Value,
            normalizedValue);
    }

    private void OnCommentChanged(ChangeEvent<string> evt)
    {
        if (updatingFields || !session.SelectedEntry.HasValue)
            return;

        metadataRepository.SetComment(
            session.SelectedEntry.Value,
            evt.newValue);
    }
}
}
