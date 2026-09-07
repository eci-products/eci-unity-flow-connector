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
using UnityEngine;

/// <summary>
/// 统一管理截图纹理的内存缓存和销毁生命周期。
/// </summary>
namespace EciFlowConnector.Editor.Infrastructure.Images
{

internal sealed class ScreenshotCache : IDisposable
{
    private readonly Dictionary<EntryId, Texture2D> textures = new();

    internal bool Contains(EntryId entryId) =>
        textures.TryGetValue(entryId, out var texture) && texture != null;

    internal bool TryGet(EntryId entryId, out Texture2D texture)
    {
        return textures.TryGetValue(entryId, out texture) && texture != null;
    }

    internal void Store(EntryId entryId, Texture2D texture)
    {
        if (!entryId.IsValid)
            throw new ArgumentException("A valid EntryId is required.", nameof(entryId));
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));

        if (textures.TryGetValue(entryId, out var previousTexture) &&
            previousTexture != null && previousTexture != texture)
        {
            UnityEngine.Object.DestroyImmediate(previousTexture);
        }

        textures[entryId] = texture;
    }

    internal void RemoveCache(EntryId entryId, Texture2D texture)
    {
        if (!entryId.IsValid)
            throw new ArgumentException("A valid EntryId is required.", nameof(entryId));
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));
        UnityEngine.Object.DestroyImmediate(texture);
        textures.Remove(entryId);
    }

    internal byte[] EncodePng(EntryId entryId)
    {
        if (!TryGet(entryId, out var texture))
            return null;

        return texture.EncodeToPNG();
    }

    internal void Clear()
    {
        foreach (var texture in textures.Values)
        {
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
        }

        textures.Clear();
    }

    public void Dispose() => Clear();
}
}
