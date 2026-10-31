#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>Catalog maintenance and mobile release configuration checks; never starts a build.</summary>
public sealed class MonetizationBuildSetup : IPreprocessBuildWithReport
{
    private const string AdSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string ConfigPath = "Assets/_Project/Resources/MonetizationConfig.json";
    public int callbackOrder => -2000;

    [MenuItem("Tools/TinyFixers/Monetization/Sync Unity IAP Catalog")]
    public static void SyncCatalog()
    {
        var source = ShopCatalog.Shared;
        if (source == null) throw new InvalidOperationException("Resources/ShopCatalog is missing.");
        var catalog = ProductCatalog.LoadDefaultCatalog();
        catalog.enableCodelessAutoInitialization = false; // StoreIap owns initialization and delivery.
        catalog.enableUnityGamingServicesAutoInitialization = false;
        var text = Resources.Load<TextAsset>("Localization/tinyfixers_localization");
        var translations = text == null ? null : JsonUtility.FromJson<LocalizationDocument>(text.text);
        var ids = new HashSet<string>();
        foreach (var offer in source.RealMoneyOffers())
        {
            if (!ids.Add(offer.StoreProductId)) throw new InvalidOperationException("Duplicate product: " + offer.StoreProductId);
            var product = catalog.allProducts.FirstOrDefault(p => p.id == offer.StoreProductId);
            if (product == null)
            {
                product = new ProductCatalogItem { id = offer.StoreProductId };
                catalog.Add(product);
            }
            product.type = ProductType.Consumable;
            product.defaultDescription.googleLocale = TranslationLocale.en_US;
            string english = Title(translations, "en", offer);
            product.defaultDescription.Title = english;
            product.defaultDescription.Description = "Wonder Fixers - " + english;
            var turkish = product.GetOrCreateDescription(TranslationLocale.tr_TR);
            turkish.Title = Title(translations, "tr", offer);
            turkish.Description = "Wonder Fixers - " + turkish.Title;
            if (TryReadUsdPrice(offer.priceLabel, out decimal usd)) product.googlePrice.value = usd;
        }
        // Keep manual metadata and extra products. Explicit $ labels are the approved USD base prices.
        // Saving this local catalog does not create products or set prices in store consoles.
        File.WriteAllText(ProductCatalog.kCatalogPath, ProductCatalog.Serialize(catalog));
        AssetDatabase.ImportAsset(ProductCatalog.kCatalogPath);
        Debug.Log($"[Monetization] Synced {ids.Count} consumables to Unity IAP Catalog.");
    }

    internal static bool TryReadUsdPrice(string label, out decimal usd)
    {
        usd = 0;
        return !string.IsNullOrEmpty(label) && label.StartsWith("$", StringComparison.Ordinal)
            && decimal.TryParse(label.Substring(1).Trim(), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out usd) && usd > 0;
    }

    private static string Title(LocalizationDocument document, string language, ShopOffer offer) =>
        document?.languages?.FirstOrDefault(l => l.code == language)?.entries
            ?.FirstOrDefault(e => e.key == "shop_offer_" + offer.id)?.value ?? offer.displayName;

    [Serializable] private sealed class LocalizationDocument { public LocalizationLanguage[] languages; }
    [Serializable] private sealed class LocalizationLanguage { public string code; public LocalizationEntry[] entries; }
    [Serializable] private sealed class LocalizationEntry { public string key, value; }

    [MenuItem("Tools/TinyFixers/Monetization/Check Android Release Setup")]
    private static void CheckAndroid() => CheckFromMenu(BuildTarget.Android);
    [MenuItem("Tools/TinyFixers/Monetization/Check iOS Release Setup")]
    private static void CheckIos() => CheckFromMenu(BuildTarget.iOS);

    private static void CheckFromMenu(BuildTarget target)
    {
        var errors = Validate(target, false);
        if (errors.Count == 0) Debug.Log($"[Monetization] {target} local configuration passed. Store/device testing is still required.");
        else Debug.LogError("[Monetization] " + string.Join("\n", errors));
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android && report.summary.platform != BuildTarget.iOS) return;
        bool development = (report.summary.options & BuildOptions.Development) != 0;
        var errors = Validate(report.summary.platform, development);
        if (errors.Count > 0) throw new BuildFailedException("Monetization setup is incomplete:\n" + string.Join("\n", errors));
    }

    private static List<string> Validate(BuildTarget target, bool development)
    {
        var errors = new List<string>();
        var group = target == BuildTarget.Android ? BuildTargetGroup.Android : BuildTargetGroup.iOS;
        var named = NamedBuildTarget.FromBuildTargetGroup(group);
        var symbols = PlayerSettings.GetScriptingDefineSymbols(named).Split(';');
        if (!symbols.Contains("TF_IAP")) errors.Add("Enable TF_IAP for " + target);
        if (!symbols.Contains("TF_ADMOB")) errors.Add("Enable TF_ADMOB for " + target);
        if (symbols.Any(s => s.StartsWith("IAP_FAKE_STORE", StringComparison.Ordinal)))
            errors.Add("Remove IAP_FAKE_STORE defines for " + target);

        var source = ShopCatalog.Shared;
        var iap = ProductCatalog.LoadDefaultCatalog();
        if (iap.enableCodelessAutoInitialization) errors.Add("Disable codeless IAP auto initialization; StoreIap owns purchases.");
        var ids = new HashSet<string>();
        if (source != null)
            foreach (var offer in source.RealMoneyOffers())
            {
                string id = offer.StoreProductId;
                if (!ids.Add(id)) errors.Add("Duplicate ShopCatalog product ID: " + id);
                if (!iap.allProducts.Any(p => p.id == id && p.type == ProductType.Consumable))
                    errors.Add("Sync Unity IAP Catalog; missing consumable " + id);
                if (offer.groups == null || !offer.groups.Any(g => g?.grants != null && g.grants.Any(r => r != null)))
                    errors.Add("Product has no rewards: " + id);
            }
        if (ids.Count == 0) errors.Add("ShopCatalog must contain real-money products.");

        var settings = AssetDatabase.LoadMainAssetAtPath(AdSettingsPath);
        if (settings == null) errors.Add("Google Mobile Ads Settings asset is missing.");
        else
        {
            var serialized = new SerializedObject(settings);
            string appId = serialized.FindProperty(target == BuildTarget.Android ? "adMobAndroidAppId" : "adMobIOSAppId")?.stringValue;
            if (string.IsNullOrWhiteSpace(appId) || (!development && !MonetizationConfig.IsLiveId(appId, true)))
                errors.Add("Set the real " + target + " AdMob App ID in Assets > Google Mobile Ads > Settings.");
            if (File.Exists("Assets/ExternalDependencyManager/Editor/Google.VersionHandler.dll")
                && serialized.FindProperty("enableExternalDependencyManager")?.boolValue == true)
                errors.Add("Disable AdMob's EDM package installation; this project already includes Firebase's legacy EDM.");
        }

        if (!File.Exists(ConfigPath)) errors.Add("MonetizationConfig.json is missing.");
        else
        {
            var config = JsonUtility.FromJson<MonetizationConfig>(File.ReadAllText(ConfigPath));
            string unit = config == null ? null : config.RewardedId(target == BuildTarget.Android, development);
            if (string.IsNullOrEmpty(unit)) errors.Add("Set the real " + target + " rewarded ad unit ID in " + ConfigPath);
        }
        return errors;
    }
}
#endif
