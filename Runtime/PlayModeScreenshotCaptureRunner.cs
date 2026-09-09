#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace EciFlowConnector.Runtime
{

public sealed class PlayModeScreenshotCaptureRunner : MonoBehaviour
{
    private Action<Texture2D> onCompleted;
    private Action<string> onFailed;

    public static PlayModeScreenshotCaptureRunner Begin(
        Action<Texture2D> completed,
        Action<string> failed)
    {
        if (!Application.isPlaying)
        {
            failed?.Invoke("Play Mode is not running.");
            return null;
        }

        var runnerObject = new GameObject(
            "[ECI Play Mode Screenshot Capture]",
            typeof(PlayModeScreenshotCaptureRunner))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        DontDestroyOnLoad(runnerObject);

        var runner = runnerObject.GetComponent<PlayModeScreenshotCaptureRunner>();
        runner.onCompleted = completed;
        runner.onFailed = failed;
        runner.StartCoroutine(runner.CaptureAtEndOfFrame());
        return runner;
    }

    public void Cancel()
    {
        onCompleted = null;
        onFailed = null;
        if (this != null)
            DestroyImmediate(gameObject);
    }

    private IEnumerator CaptureAtEndOfFrame()
    {
        // Allow the newly focused Game View to render a complete frame first.
        yield return null;
        yield return new WaitForEndOfFrame();

        if (!Application.isPlaying)
        {
            Fail("Play Mode ended before the screenshot could be captured.");
            yield break;
        }

        Texture2D texture = null;
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (Exception exception)
        {
            Fail($"CaptureScreenshotAsTexture failed: {exception.Message}");
            yield break;
        }

        if (texture == null)
        {
            Fail(
                "CaptureScreenshotAsTexture returned no texture. " +
                "Make sure the Game View is visible and Play Mode is running.");
            yield break;
        }

        var completed = onCompleted;
        onCompleted = null;
        onFailed = null;
        Destroy(gameObject);
        completed?.Invoke(texture);
    }

    private void Fail(string message)
    {
        var failed = onFailed;
        onCompleted = null;
        onFailed = null;
        Destroy(gameObject);
        failed?.Invoke(message);
    }
}
}
#endif
