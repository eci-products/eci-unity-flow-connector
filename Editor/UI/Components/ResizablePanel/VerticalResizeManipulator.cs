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
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 通过面板顶部拖动条同步调整上下两个区域的高度。
/// </summary>
namespace EciFlowConnector.Editor.UI.Components.ResizablePanel
{

internal sealed class VerticalResizeManipulator : PointerManipulator
{
    private readonly VisualElement lowerPanel;
    private readonly VisualElement upperPanel;
    private readonly float minimumLowerHeight;
    private readonly float minimumUpperHeight;

    private int activePointerId = -1;
    private float pointerStartY;
    private float lowerStartHeight;
    private float upperStartHeight;

    internal VerticalResizeManipulator(
        VisualElement lowerPanel,
        VisualElement upperPanel,
        float minimumLowerHeight = 160f,
        float minimumUpperHeight = 100f)
    {
        this.lowerPanel = lowerPanel;
        this.upperPanel = upperPanel;
        this.minimumLowerHeight = minimumLowerHeight;
        this.minimumUpperHeight = minimumUpperHeight;
        activators.Add(new ManipulatorActivationFilter
        {
            button = MouseButton.LeftMouse
        });
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (activePointerId >= 0 || !CanStartManipulation(evt) ||
            lowerPanel == null || upperPanel == null)
        {
            return;
        }

        activePointerId = evt.pointerId;
        pointerStartY = evt.position.y;
        lowerStartHeight = GetCurrentHeight(lowerPanel);
        upperStartHeight = GetCurrentHeight(upperPanel);

        target.CapturePointer(activePointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != activePointerId ||
            !target.HasPointerCapture(activePointerId))
        {
            return;
        }

        // 顶部分隔条向上移动时，下方面板增高、上方面板等量缩小。
        var requestedDelta = pointerStartY - evt.position.y;
        var minimumDelta = minimumLowerHeight - lowerStartHeight;
        var maximumDelta = upperStartHeight - minimumUpperHeight;
        var delta = Mathf.Clamp(requestedDelta, minimumDelta, maximumDelta);

        var lowerHeight = lowerStartHeight + delta;
        var upperHeight = upperStartHeight - delta;
        lowerPanel.style.height = lowerHeight;
        upperPanel.style.height = upperHeight;
        upperPanel.style.maxHeight = upperHeight;

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != activePointerId || !CanStopManipulation(evt))
            return;

        if (target.HasPointerCapture(activePointerId))
            target.ReleasePointer(activePointerId);

        activePointerId = -1;
        evt.StopPropagation();
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (evt.pointerId == activePointerId)
            activePointerId = -1;
    }

    private static float GetCurrentHeight(VisualElement element)
    {
        var height = element.resolvedStyle.height;
        if (float.IsNaN(height) || height <= 0f)
            height = element.layout.height;
        return Mathf.Max(0f, height);
    }
}
}
