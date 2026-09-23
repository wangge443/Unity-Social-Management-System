#if UNITY_EDITOR
using System.IO;
using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class ClientUiCapture
{
    public static void Save(Canvas canvas, string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        Capture(canvas, name, 960, 540);
        Capture(canvas, name, 1280, 720);
    }
    public static void SavePosts(Canvas canvas, string name, System.Action validate)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        Capture(canvas, name, 960, 540, validate);
        Capture(canvas, name, 1280, 720, validate);
        Capture(canvas, name, 2560, 1440, validate);
    }
    private static void Capture(Canvas canvas, string name, int width, int height, System.Action validate = null)
    {
        var scrolls = canvas.GetComponentsInChildren<ScrollRect>();
        var positions = new Vector2[scrolls.Length];
        for (var i = 0; i < scrolls.Length; i++) positions[i] = scrolls[i].normalizedPosition;
        var mode = canvas.renderMode;
        var worldCamera = canvas.worldCamera;
        var plane = canvas.planeDistance;
        var cameraObject = new GameObject("UiCaptureCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = SocialTheme.Background;
        var target = new RenderTexture(width, height, 24);
        var previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in scrolls) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < scrolls.Length; i++) scrolls[i].normalizedPosition = positions[i];
            camera.Render();
            validate?.Invoke();
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/UI"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + "-" + width + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = plane;
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.Destroy(target);
            if (pixels != null) Object.Destroy(pixels);
            Object.Destroy(cameraObject);
            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < scrolls.Length; i++) scrolls[i].normalizedPosition = positions[i];
        }
    }
}
#endif
