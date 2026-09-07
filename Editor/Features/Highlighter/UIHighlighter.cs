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
using UnityEngine;
using UnityEngine.UI;

namespace EciFlowConnector.Editor.Features.Highlighter
{

internal enum UIHighlightMode
{
    Border = 0,
    Arrow = 1,
    None = 2
}

internal sealed class UIHighlighter
{
    private readonly float BorderPadding = 6f;
    private readonly float ArrowMargin = 36f;
    private readonly string ModePrefKey = "ECIFlowConnector_HighlighterMode";
    private readonly string BorderColorPrefKey = "ECIFlowConnector_HighlighterBorderColor";
    private readonly string BorderThicknessPrefKey = "ECIFlowConnector_HighlighterBorderThickness";
    private readonly string ArrowColorPrefKey = "ECIFlowConnector_HighlighterArrowColor";
    private readonly string ArrowSizePrefKey = "ECIFlowConnector_HighlighterArrowSize";
    private readonly Color DefaultColor = new Color(1f, 0.72f, 0.12f, 1f);

    private readonly List<HighlightVisual> Visuals = new List<HighlightVisual>();
    private GameObject overlayRoot;
    private Canvas overlayCanvas;
    private RectTransform overlayRect;
    private Texture2D arrowTexture;
    private Sprite arrowSprite;
    private UIHighlightMode mode;
    private Color borderColor;
    private float borderThickness;
    private Color arrowColor;
    private float arrowSize;

    internal UIHighlightMode Mode
    {
        get => mode;
        set
        {
            mode = value;
            EditorPrefs.SetInt(ModePrefKey, (int)value);
            if (value == UIHighlightMode.None)
                Clear();
        }
    }

    internal Color BorderColor
    {
        get => borderColor;
        set
        {
            borderColor = value;
            SaveColor(BorderColorPrefKey, value);
        }
    }

    internal float BorderThickness
    {
        get => borderThickness;
        set
        {
            borderThickness = Mathf.Clamp(value, 1f, 20f);
            EditorPrefs.SetFloat(BorderThicknessPrefKey, borderThickness);
        }
    }

    internal Color ArrowColor
    {
        get => arrowColor;
        set
        {
            arrowColor = value;
            SaveColor(ArrowColorPrefKey, value);
        }
    }

    internal float ArrowSize
    {
        get => arrowSize;
        set
        {
            arrowSize = Mathf.Clamp(value, 12f, 128f);
            EditorPrefs.SetFloat(ArrowSizePrefKey, arrowSize);
        }
    }

    internal UIHighlighter()
    {
        mode = (UIHighlightMode)EditorPrefs.GetInt(
            ModePrefKey,
            (int)UIHighlightMode.Border);
        borderColor = LoadColor(BorderColorPrefKey, DefaultColor);
        borderThickness = Mathf.Clamp(
            EditorPrefs.GetFloat(BorderThicknessPrefKey, 3f),
            1f,
            20f);
        arrowColor = LoadColor(ArrowColorPrefKey, DefaultColor);
        arrowSize = Mathf.Clamp(
            EditorPrefs.GetFloat(ArrowSizePrefKey, 32f),
            12f,
            128f);
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
    }

    internal void Dispose()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Clear;
        Clear();
    }

    private Color LoadColor(string key, Color fallback)
    {
        var html = EditorPrefs.GetString(key, string.Empty);
        return !string.IsNullOrEmpty(html) &&
               ColorUtility.TryParseHtmlString($"#{html}", out var color)
            ? color
            : fallback;
    }

    private void SaveColor(string key, Color color)
    {
        EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(color));
    }

    internal void Show(IReadOnlyList<Transform> targets)
    {
        if (Mode == UIHighlightMode.None)
        {
            Clear();
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            Clear();
            Debug.Log("Flow Connector Warning: " + "Localization UI highlighting is only displayed in Play Mode.");
            return;
        }

        EnsureOverlay();
        EnsureVisualCount(targets.Count);

        for (var i = 0; i < Visuals.Count; i++)
        {
            var hasTarget = i < targets.Count && targets[i] is RectTransform;
            Visuals[i].Target = hasTarget ? (RectTransform)targets[i] : null;
            Visuals[i].SetActive(hasTarget);
        }

        Canvas.ForceUpdateCanvases();
        Draw();
    }

    internal void Clear()
    {
        Visuals.Clear();
        overlayCanvas = null;
        overlayRect = null;

        if (overlayRoot != null)
            Object.DestroyImmediate(overlayRoot);
        overlayRoot = null;

        if (arrowSprite != null)
            Object.DestroyImmediate(arrowSprite);
        arrowSprite = null;

        if (arrowTexture != null)
            Object.DestroyImmediate(arrowTexture);
        arrowTexture = null;
    }

    internal void Redraw()
    {
        if (Mode == UIHighlightMode.None)
        {
            Clear();
            return;
        }

        if (!EditorApplication.isPlaying || overlayRoot == null || overlayCanvas == null)
            return;

        Canvas.ForceUpdateCanvases();
        Draw();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.ExitingPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            Clear();
        }
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null)
            return;

        overlayRoot = CreateHiddenObject(
            "[ECI Localization UI Highlighter]",
            null,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Object.DontDestroyOnLoad(overlayRoot);

        overlayCanvas = overlayRoot.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = short.MaxValue;

        var scaler = overlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        overlayRect = overlayRoot.GetComponent<RectTransform>();
    }

    private void EnsureVisualCount(int count)
    {
        while (Visuals.Count < count)
            Visuals.Add(CreateVisual(Visuals.Count));
    }

    private HighlightVisual CreateVisual(int index)
    {
        var frameObject = CreateHiddenObject(
            $"Highlight Frame {index + 1}",
            overlayRect,
            typeof(RectTransform));
        var frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);

        var edges = new[]
        {
            CreateEdge("Top", frameRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, BorderThickness)),
            CreateEdge("Bottom", frameRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, BorderThickness)),
            CreateEdge("Left", frameRect, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(BorderThickness, 0f)),
            CreateEdge("Right", frameRect, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(BorderThickness, 0f))
        };

        var arrowObject = CreateHiddenObject(
            $"Direction Arrow {index + 1}",
            overlayRect,
            typeof(RectTransform),
            typeof(Image));
        var arrowRect = arrowObject.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = Vector2.one * ArrowSize;

        var arrowImage = arrowObject.GetComponent<Image>();
        arrowImage.sprite = GetArrowSprite();
        arrowImage.color = ArrowColor;
        arrowImage.raycastTarget = false;

        return new HighlightVisual(frameObject, frameRect, edges, arrowObject, arrowRect, arrowImage);
    }

    private Image CreateEdge(
        string name,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        var edgeObject = CreateHiddenObject(name, parent, typeof(RectTransform), typeof(Image));
        var rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        var image = edgeObject.GetComponent<Image>();
        image.color = BorderColor;
        image.raycastTarget = false;
        return image;
    }

    private GameObject CreateHiddenObject(
        string name,
        Transform parent,
        params System.Type[] components)
    {
        var gameObject = new GameObject(name, components)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private Sprite GetArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        const int size = 32;
        arrowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ECI Localization Direction Arrow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        var clear = new Color32(255, 255, 255, 0);
        var white = new Color32(255, 255, 255, 255);
        var center = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distanceFromCenter = Mathf.Abs(y - center);
                var shaft = x <= 17 && distanceFromCenter <= 3f;
                var arrowHead = x >= 10 && distanceFromCenter <= (size - 1 - x) * 0.48f;
                pixels[y * size + x] = shaft || arrowHead ? white : clear;
            }
        }

        arrowTexture.SetPixels32(pixels);
        arrowTexture.Apply(false, true);

        arrowSprite = Sprite.Create(
            arrowTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        arrowSprite.name = "ECI Localization Direction Arrow";
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }

    private void Draw()
    {
        if (!EditorApplication.isPlaying || overlayRect == null)
        {
            Clear();
            return;
        }

        var hasLiveTarget = false;
        foreach (var visual in Visuals)
        {
            if (visual.Target == null || !visual.Target.gameObject.activeInHierarchy)
            {
                visual.SetActive(false);
                continue;
            }

            hasLiveTarget = true;
            UpdateVisual(visual);
        }

        if (!hasLiveTarget)
            Clear();
    }

    private void UpdateVisual(HighlightVisual visual)
    {
        if (!TryGetScreenBounds(visual.Target, out var screenBounds))
        {
            visual.SetActive(false);
            return;
        }

        var viewportRect = overlayCanvas.pixelRect;
        var isVisible = screenBounds.Overlaps(viewportRect, true);

        var borderMode = Mode == UIHighlightMode.Border;
        visual.FrameObject.SetActive(borderMode && isVisible);
        visual.ArrowObject.SetActive(!borderMode);

        if (borderMode && isVisible)
        {
            var minScreen = new Vector2(screenBounds.xMin, screenBounds.yMin);
            var maxScreen = new Vector2(screenBounds.xMax, screenBounds.yMax);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRect, minScreen, null, out var minLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRect, maxScreen, null, out var maxLocal);

            visual.FrameRect.anchoredPosition = (minLocal + maxLocal) * 0.5f;
            visual.FrameRect.sizeDelta =
                new Vector2(Mathf.Abs(maxLocal.x - minLocal.x), Mathf.Abs(maxLocal.y - minLocal.y)) +
                Vector2.one * BorderPadding * 2f;

            ApplyBorderAppearance(visual);
        }

        if (!borderMode)
            UpdateArrow(visual, screenBounds, isVisible);
    }

    private void ApplyBorderAppearance(HighlightVisual visual)
    {
        var thickness = BorderThickness;
        for (var i = 0; i < visual.Edges.Length; i++)
        {
            var edge = visual.Edges[i];
            edge.color = BorderColor;
            edge.rectTransform.sizeDelta = i < 2
                ? new Vector2(0f, thickness)
                : new Vector2(thickness, 0f);
        }
    }

    private void UpdateArrow(HighlightVisual visual, Rect screenBounds, bool isVisible)
    {
        var arrowSize = ArrowSize;
        visual.ArrowRect.sizeDelta = Vector2.one * arrowSize;
        visual.ArrowImage.color = ArrowColor;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayRect,
            screenBounds.center,
            null,
            out var targetLocal);

        Vector2 arrowPosition;
        Vector2 direction;
        if (isVisible)
        {
            var placeOnLeft = targetLocal.x >= 0f;
            var horizontalOffset = screenBounds.width * 0.5f + arrowSize * 0.65f + 8f;
            arrowPosition = targetLocal +
                            (placeOnLeft ? Vector2.left : Vector2.right) * horizontalOffset;

            var rect = overlayRect.rect;
            arrowPosition.x = Mathf.Clamp(
                arrowPosition.x,
                rect.xMin + ArrowMargin,
                rect.xMax - ArrowMargin);
            arrowPosition.y = Mathf.Clamp(
                arrowPosition.y,
                rect.yMin + ArrowMargin,
                rect.yMax - ArrowMargin);
            direction = (targetLocal - arrowPosition).sqrMagnitude > 0.001f
                ? (targetLocal - arrowPosition).normalized
                : Vector2.right;
        }
        else
        {
            direction = targetLocal.sqrMagnitude > 0.001f
                ? targetLocal.normalized
                : Vector2.right;
            var available = overlayRect.rect.size * 0.5f - Vector2.one * ArrowMargin;
            var scaleX = Mathf.Abs(direction.x) > 0.001f
                ? available.x / Mathf.Abs(direction.x)
                : float.PositiveInfinity;
            var scaleY = Mathf.Abs(direction.y) > 0.001f
                ? available.y / Mathf.Abs(direction.y)
                : float.PositiveInfinity;
            arrowPosition = direction * Mathf.Min(scaleX, scaleY);
        }

        visual.ArrowRect.anchoredPosition = arrowPosition;
        visual.ArrowRect.localRotation =
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private bool TryGetScreenBounds(RectTransform target, out Rect bounds)
    {
        bounds = default;
        var targetCanvas = target.GetComponentInParent<Canvas>();
        Camera camera = null;
        if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;

        if (targetCanvas != null &&
            targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay &&
            camera == null)
        {
            return false;
        }

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < corners.Length; i++)
        {
            var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        if (float.IsInfinity(min.x) || float.IsInfinity(min.y) ||
            float.IsInfinity(max.x) || float.IsInfinity(max.y))
        {
            return false;
        }

        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private sealed class HighlightVisual
    {
        public HighlightVisual(
            GameObject frameObject,
            RectTransform frameRect,
            Image[] edges,
            GameObject arrowObject,
            RectTransform arrowRect,
            Image arrowImage)
        {
            FrameObject = frameObject;
            FrameRect = frameRect;
            Edges = edges;
            ArrowObject = arrowObject;
            ArrowRect = arrowRect;
            ArrowImage = arrowImage;
        }

        public RectTransform Target { get; set; }
        public GameObject FrameObject { get; }
        public RectTransform FrameRect { get; }
        public Image[] Edges { get; }
        public GameObject ArrowObject { get; }
        public RectTransform ArrowRect { get; }
        public Image ArrowImage { get; }

        public void SetActive(bool active)
        {
            FrameObject.SetActive(active);
            ArrowObject.SetActive(false);
        }
    }
}
}
