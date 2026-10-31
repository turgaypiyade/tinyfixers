using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Public AdMob unit IDs. App IDs are configured in Google Mobile Ads Settings.</summary>
[Serializable]
public sealed class MonetizationConfig
{
    public string androidRewardedAdUnitId;
    public string iosRewardedAdUnitId;

    public const string TestPublisher = "ca-app-pub-3940256099942544";
    public const string TestAndroidAppId = TestPublisher + "~3347511713";
    public const string TestIosAppId = TestPublisher + "~1458002511";
    public const string TestAndroidRewardedId = TestPublisher + "/5224354917";
    public const string TestIosRewardedId = TestPublisher + "/1712485313";

    private static MonetizationConfig instance;
    public static MonetizationConfig Current
    {
        get
        {
            if (instance != null) return instance;
            var asset = Resources.Load<TextAsset>("MonetizationConfig");
            return instance = asset == null ? new MonetizationConfig()
                : JsonUtility.FromJson<MonetizationConfig>(asset.text) ?? new MonetizationConfig();
        }
    }

    public static bool IsLiveId(string value, bool appId) => !string.IsNullOrEmpty(value)
        && !value.StartsWith(TestPublisher, StringComparison.Ordinal)
        && Regex.IsMatch(value, appId ? @"^ca-app-pub-\d{16}~\d{10}$" : @"^ca-app-pub-\d{16}/\d{10}$");

    public string RewardedId(bool android, bool development)
    {
        // Every development device uses Google's test inventory, even with production IDs configured.
        if (development) return android ? TestAndroidRewardedId : TestIosRewardedId;
        string value = android ? androidRewardedAdUnitId : iosRewardedAdUnitId;
        return IsLiveId(value, false) ? value : null;
    }
}
