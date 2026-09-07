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
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace EciFlowConnector.Editor.Features.Screenshots
{

internal sealed class ScreenshotController
{
    private const string FMS_URL_PATH = "/api-service/file-management-service/";
    private const string GET_IMG_METHOD = "/download/file";

    private readonly FlowConnectorSession session;
    private readonly FlowConnectorAppState applicationState;
    private readonly EntryMetadataRepository metadataRepository;
    private readonly ScreenshotCache screenshotCache;
    private readonly FlowApiClient httpService;
    private readonly HashSet<string> remoteLoadsInProgress =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> remoteLoadErrors =
        new(StringComparer.Ordinal);
    private readonly ScreenshotView view;
    private EditorWindow hostWindow;
    private PlayModeScreenshotCaptureRunner captureRunner;
    private bool capturePending; //正在截图
    private bool disposed;

    internal ScreenshotController(
        FlowConnectorSession session,
        FlowConnectorAppState applicationState,
        EntryMetadataRepository metadataRepository,
        ScreenshotCache screenshotCache,
        FlowApiClient httpService,
        ScreenshotView view)
    {
        this.session = session;
        this.applicationState = applicationState;
        this.metadataRepository = metadataRepository;
        this.screenshotCache = screenshotCache;
        this.httpService = httpService;
        this.view = view;
    }

    private EntryId? SelectedEntry => session.SelectedEntry;
    private string SelectedTableName => SelectedEntry?.TableName ?? string.Empty;
    private string SelectedEntryKey => SelectedEntry?.EntryKey ?? string.Empty;

    internal void Bind(VisualElement root, EditorWindow hostWindow)
    {
        this.hostWindow = hostWindow;
        view.Bind(root);
        view.CaptureRequested += Capture;
        view.DeleteRequested += Delete;
    }
    internal void Capture() => ScreenShot();
    internal void Delete() => DeleteScreenShot();
    internal void RefreshPreview()
    {
        UpdateScreenShotPreview();
        _ = LoadRemoteScreenshotForSelectionAsync();
    }
    internal byte[] GetPngForExport(string tableName, string entryKey) =>
        GetScreenshotPngForExport(tableName, entryKey);
    internal void Clear() => ClearScreenshots();

    /// <summary>
    /// 当选中 Entry 只有远程截图 ID、尚无内存纹理时，异步下载并缓存截图。
    /// </summary>
    private async Task LoadRemoteScreenshotForSelectionAsync()
    {
        var tableName = SelectedTableName;
        var entryKey = SelectedEntryKey;
        if (disposed || string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(entryKey) || HasScreenshot(tableName, entryKey))
        {
            return;
        }

        var metadata = metadataRepository.Get(
            new EntryId(tableName, entryKey));
        if (string.IsNullOrEmpty(metadata?.ScreenShotId) ||
            string.IsNullOrEmpty(applicationState.FlowUrlPrefix))
        {
            return;
        }

        var identity = GetEntryIdentity(tableName, entryKey);
        if (!remoteLoadsInProgress.Add(identity))
            return;

        remoteLoadErrors.Remove(identity);
        UpdateScreenShotPreview();

        try
        {
            var img = await httpService.GetBytesAsync(
                applicationState.FlowUrlPrefix +
                FMS_URL_PATH +
                GET_IMG_METHOD +
                "?id=" + metadata.ScreenShotId);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"RemoteScreenShot_{tableName}_{entryKey}",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!texture.LoadImage(img, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException(
                    "The downloaded data could not be decoded as an image.");
            }

            if (disposed)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return;
            }

            // 下载期间用户可能已经重新截图，本地新截图优先，不能被旧请求覆盖。
            if (HasScreenshot(tableName, entryKey))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return;
            }

            StoreScreenshot(tableName, entryKey, texture);
            // Debug.Log(
            //     $"Loaded remote screenshot for '{tableName}/{entryKey}' " +
            //     $"({texture.width}x{texture.height}).");
        }
        catch (Exception exception)
        {
            if (!disposed)
            {
                remoteLoadErrors[identity] = exception.Message;
                EditorUtility.DisplayDialog(
                "ScreenShot",
                $"Failed to load screenshot for '{tableName}/{entryKey}': " +
                    exception.Message,
                "OK");
            }
        }
        finally
        {
            remoteLoadsInProgress.Remove(identity);
            if (!disposed && IsSelectedEntry(tableName, entryKey))
                UpdateScreenShotPreview();
        }
    }

    private void DeleteScreenShot()
    {
        if (!SelectedEntry.HasValue)
        {
            EditorUtility.DisplayDialog(
                "ScreenShot",
                "Please select a table entry first.",
                "OK");
            return;
        }

        if (capturePending)
            return;

        var capturedTableName = SelectedTableName;
        var capturedEntryKey = SelectedEntryKey;
        var entryId = new EntryId(capturedTableName, capturedEntryKey);
        if (screenshotCache.TryGet(entryId, out var previousTexture))
        {
            screenshotCache.RemoveCache(entryId,previousTexture);
            UpdateScreenShotPreview();
        }
        //如果存在dirty数据删除dirty数据
        metadataRepository.MarkScreenshotUndirty(new EntryId(capturedTableName, capturedEntryKey));
        
    }
    private void ScreenShot()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "ScreenShot",
                "Please enter Play Mode before taking a screenshot.",
                "OK");
            return;
        }

        if (!SelectedEntry.HasValue)
        {
            EditorUtility.DisplayDialog(
                "ScreenShot",
                "Please select a table entry first.",
                "OK");
            return;
        }

        if (capturePending)
            return;

        var capturedTableName = SelectedTableName;
        var capturedEntryKey = SelectedEntryKey;
        capturePending = true;
        view.SetCapturing(true);

        if (!FocusGameView())
        {
            OnScreenShotFailed("Could not open the Unity Game View.");
            return;
        }

        captureRunner = PlayModeScreenshotCaptureRunner.Begin(
            texture => OnScreenShotCaptured(capturedTableName, capturedEntryKey, texture),
            error => OnScreenShotFailed(error));

        if (captureRunner == null)
            FinishScreenShotCapture();
    }

    private static bool FocusGameView()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gameView = gameViewType != null ? EditorWindow.GetWindow(gameViewType) : null;
        if (gameView == null)
            return false;

        gameView.Focus();
        return true;
    }

    private void OnScreenShotCaptured(string capturedTableName, string capturedEntryKey, Texture2D texture)
    {
        captureRunner = null;
        FinishScreenShotCapture();

        if (disposed)
        {
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            return;
        }

        hostWindow?.Focus();

        texture.name = $"ScreenShot_{capturedTableName}_{capturedEntryKey}";
        texture.hideFlags = HideFlags.HideAndDontSave;
        StoreScreenshot(capturedTableName, capturedEntryKey, texture);
        metadataRepository.MarkScreenshotDirty(
            new EntryId(capturedTableName, capturedEntryKey));
        UpdateScreenShotPreview();
        Debug.Log(
            $"Captured Play Mode screenshot for '{capturedTableName}/{capturedEntryKey}' " +
            $"({texture.width}x{texture.height}).");
    }

    private void OnScreenShotFailed(string error)
    {
        captureRunner = null;
        FinishScreenShotCapture();

        if (disposed)
            return;

        hostWindow?.Focus();
        Debug.LogError(error);
        EditorUtility.DisplayDialog("ScreenShot", error, "OK");
    }

    private void FinishScreenShotCapture()
    {
        capturePending = false;
        view.SetCapturing(false);
    }

    private void UpdateScreenShotPreview()
    {
        var tableName = SelectedTableName;
        var entryKey = SelectedEntryKey;
        var entryId = SelectedEntry;
        var identity = GetEntryIdentity(tableName, entryKey);
        if (TryGetScreenshot(tableName, entryKey, out var texture))
        {
            view.ShowTexture(entryId.Value, texture);
        }
        else
        {
            if (remoteLoadsInProgress.Contains(identity))
            {
                view.ShowLoading(entryId.Value);
            }
            else if (remoteLoadErrors.TryGetValue(identity, out var error))
            {
                view.ShowError(error);
            }
            else
            {
                view.ShowEmpty(entryId);
            }
        }
    }

    private void StoreScreenshot(string tableName, string entryKey, Texture2D texture)
    {
        var entryId = new EntryId(tableName, entryKey);
        if (screenshotCache.TryGet(entryId, out var previousTexture) &&
            previousTexture != texture)
        {
            if (IsSelectedEntry(tableName, entryKey))
                view.ShowEmpty(entryId);
        }

        screenshotCache.Store(entryId, texture);
        remoteLoadErrors.Remove(GetEntryIdentity(tableName, entryKey));
    }

    private bool HasScreenshot(string tableName, string entryKey)
    {
        var entryId = new EntryId(tableName, entryKey);
        return entryId.IsValid && screenshotCache.Contains(entryId);
    }

    private bool TryGetScreenshot(string tableName,string entryKey,out Texture2D texture)
    {
        var entryId = new EntryId(tableName, entryKey);
        texture = null;
        return entryId.IsValid && screenshotCache.TryGet(entryId, out texture);
    }

    private bool IsSelectedEntry(string tableName, string entryKey)
    {
        return SelectedEntry.HasValue &&
               SelectedEntry.Value == new EntryId(tableName, entryKey);
    }

    private static string GetEntryIdentity(string tableName, string entryKey)
    {
        return (tableName ?? string.Empty) + "\n" + (entryKey ?? string.Empty);
    }

    /// <summary>
    /// 将指定 Entry 的内存截图编码为 Export 使用的 PNG 数据。
    /// </summary>
    private byte[] GetScreenshotPngForExport(string tableName, string entryKey)
    {
        var entryId = new EntryId(tableName, entryKey);
        if (!entryId.IsValid || !screenshotCache.Contains(entryId))
            return null;

        try
        {
            var pngData = screenshotCache.EncodePng(entryId);
            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogWarning(
                    $"Screenshot PNG encoding returned no data for '{tableName}/{entryKey}'.");
                return null;
            }

            return pngData;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"Failed to encode screenshot for '{tableName}/{entryKey}' as PNG: " +
                exception.Message);
            return null;
        }
    }

    private void ClearScreenshots()
    {
        disposed = true;
        captureRunner?.Cancel();
        captureRunner = null;
        capturePending = false;
        remoteLoadsInProgress.Clear();
        remoteLoadErrors.Clear();
        view.CaptureRequested -= Capture;
        view.DeleteRequested -= Delete;
        view.Clear();

        screenshotCache.Clear();
        //由于目前没有图片本地持久化功能，所以默认dispose将所有meta_ScreenshotDirty置为false
        metadataRepository.Dispose();
    }
}
}
