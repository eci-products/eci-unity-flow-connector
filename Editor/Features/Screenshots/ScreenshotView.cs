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
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 截图区域的 UI 适配器，不负责网络、缓存或持久化。
/// </summary>
namespace EciFlowConnector.Editor.Features.Screenshots
{

internal sealed class ScreenshotView
{
    private VisualElement preview;
    private Button captureButton;
    private Button deleteButton;
    private Label statusLabel;
    private IVisualElementScheduledItem loadingAnimation;
    private int loadingDotCount;

    internal event Action CaptureRequested;
    internal event Action DeleteRequested;

    internal void Bind(VisualElement root)
    {
        preview = root.Q<VisualElement>("ScreenShotPic");
        captureButton = root.Q<Button>("btnScreenShot");
        deleteButton = root.Q<Button>("btnScreenShotDel");
        statusLabel = root.Q<Label>("ScreenShotLoading");
        captureButton?.RegisterCallback<ClickEvent>(_ => CaptureRequested?.Invoke());
        deleteButton?.RegisterCallback<ClickEvent>(_ => DeleteRequested?.Invoke());
    }

    internal void SetCapturing(bool capturing)
    {
        if (captureButton == null)
            return;

        captureButton.text = capturing ? "Capturing..." : "ScreenShot";
        captureButton.SetEnabled(!capturing);
    }

    internal void ShowTexture(EntryId entryId, Texture2D texture)
    {
        if (preview == null || texture == null)
            return;

        preview.style.backgroundImage =
            new StyleBackground(Background.FromTexture2D(texture));
        preview.tooltip = $"{entryId} ({texture.width}x{texture.height})";
        HideStatus();
    }

    internal void ShowEmpty(EntryId? entryId)
    {
        if (preview == null)
            return;

        preview.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        preview.tooltip = entryId.HasValue
            ? $"No screenshot captured for {entryId.Value}."
            : "Select a table entry, then capture the Play Mode frame.";
        HideStatus();
    }

    internal void ShowLoading(EntryId entryId)
    {
        if (preview == null || statusLabel == null)
            return;

        preview.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        preview.tooltip = $"Loading screenshot for {entryId}.";
        statusLabel.RemoveFromClassList("hidden");
        loadingDotCount = 0;
        statusLabel.text = "Loading";

        if (loadingAnimation == null)
        {
            loadingAnimation = statusLabel.schedule.Execute(() =>
            {
                loadingDotCount = (loadingDotCount + 1) % 4;
                statusLabel.text = "Loading" + new string('.', loadingDotCount);
            }).Every(320);
        }
        else
        {
            loadingAnimation.Resume();
        }
    }

    internal void ShowError(string error)
    {
        if (preview != null)
        {
            preview.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            preview.tooltip = error ?? string.Empty;
        }

        if (statusLabel == null)
            return;

        loadingAnimation?.Pause();
        statusLabel.text = "Load failed";
        statusLabel.RemoveFromClassList("hidden");
    }

    internal void Clear()
    {
        SetCapturing(false);
        if (preview != null)
            preview.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        HideStatus();
    }

    private void HideStatus()
    {
        loadingAnimation?.Pause();
        statusLabel?.AddToClassList("hidden");
    }
}
}
