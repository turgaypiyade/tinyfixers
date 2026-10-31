using System;
using UnityEngine;

/// <summary>Public client configuration only. Private provider secrets belong exclusively in Firebase.</summary>
[Serializable]
public sealed class SocialSignInConfig
{
    public string googleIosClientId;
    public string googleWebClientId;
    public string facebookAppId;
    public string facebookClientToken;
    private static SocialSignInConfig instance;
    public static SocialSignInConfig Current
    {
        get
        {
            if (instance != null) return instance;
            var json = Resources.Load<TextAsset>("AuthProviderConfig.local");
            instance = json == null ? new SocialSignInConfig() : JsonUtility.FromJson<SocialSignInConfig>(json.text);
            return instance ??= new SocialSignInConfig();
        }
    }
    public static bool ValidGoogleClient(string value) => !string.IsNullOrWhiteSpace(value)
        && value.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)
        && value.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) < 0;
}
