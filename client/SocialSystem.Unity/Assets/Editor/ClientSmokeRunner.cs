using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class ClientSmokeRunner
{
    static ClientSmokeRunner() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }
    public static void Run()
    {
        ClientProjectSetup.CreateLoginScene();
        SessionState.SetBool("SocialSystem.ClientSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("SocialSystem.ClientSmoke", false)) return;
        SessionState.SetBool("SocialSystem.ClientSmoke", false);
        if (System.Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_MODE") == "social")
            new GameObject("ClientSocialSmokeTest").AddComponent<ClientSocialSmokeTest>();
        else
            new GameObject("ClientConnectionSmokeTest").AddComponent<ClientConnectionSmokeTest>();
    }
}

