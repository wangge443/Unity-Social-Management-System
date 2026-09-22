using System;
using System.IO;
using SocialSystem.Client.Auth;
using SocialSystem.Client.Network;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ClientProjectSetup
{
    public const string ScenePath = "Assets/Scenes/LoginScene.unity";

    [MenuItem("SocialSystem/Create or Open LoginScene")]
    public static void CreateLoginScene()
    {
        PlayerSettings.companyName = "SocialSystem";
        PlayerSettings.productName = "SocialSystem Client";
        PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
        PlayerSettings.defaultScreenWidth = 960;
        PlayerSettings.defaultScreenHeight = 540;
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera));
        camera.tag = "MainCamera";
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.08f, 0.11f, 0.16f);
        var services = new GameObject("ClientServices");
        var tokens = services.AddComponent<TokenManager>();
        var api = services.AddComponent<ApiClient>();
        api.tokenManager = tokens;
        var auth = services.AddComponent<AuthManager>();
        auth.apiClient = api;
        auth.tokenManager = tokens;

        var canvasObject = new GameObject("LoginCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.matchWidthOrHeight = 0.5f;
        var panel = Box("LoginPanel", canvasObject.transform, new Vector2(0, 0), new Vector2(460, 430), new Color(0.14f, 0.18f, 0.24f));
        Label("Title", panel.transform, "SocialSystem Login", new Vector2(0, 162), new Vector2(410, 44), 28);
        Label("Server", panel.transform, "http://localhost:5080", new Vector2(0, 119), new Vector2(410, 28), 16);
        var username = Input("Username", panel.transform, "Username", 55, false);
        var password = Input("Password", panel.transform, "Password", -10, true);
        var buttonObject = Box("LoginButton", panel.transform, new Vector2(0, -80), new Vector2(360, 48), new Color(0.12f, 0.48f, 0.8f));
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        Label("Text", buttonObject.transform, "Log in", Vector2.zero, new Vector2(340, 44), 22);
        var status = Label("Status", panel.transform, "Start the API, then enter an existing account.", new Vector2(0, -153), new Vector2(400, 70), 16);
        var view = canvasObject.AddComponent<LoginSceneController>();
        view.authManager = auth;
        view.usernameInput = username;
        view.passwordInput = password;
        view.loginButton = button;
        view.statusText = status;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("PASS: LoginScene created with UGUI and API services.");
    }

    private static GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return go;
    }
    private static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }
    private static InputField Input(string name, Transform parent, string hint, float y, bool password)
    {
        var go = Box(name, parent, new Vector2(0, y), new Vector2(360, 48), Color.white);
        var input = go.AddComponent<InputField>();
        var text = Label("Text", go.transform, "", Vector2.zero, new Vector2(330, 40), 20);
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        var placeholder = Label("Placeholder", go.transform, hint, Vector2.zero, new Vector2(330, 40), 20);
        placeholder.color = Color.gray;
        placeholder.alignment = TextAnchor.MiddleLeft;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        input.characterLimit = password ? 128 : 32;
        return input;
    }

    public static void CreateAndBuild()
    {
        CreateLoginScene();
        Directory.CreateDirectory("Build");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/SocialSystem.Client.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows client build failed: " + report.summary.result);
        Debug.Log("PASS: Windows development client built.");
    }
}
