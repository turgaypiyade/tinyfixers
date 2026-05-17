using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameLocalization
{
    private const string LanguagePrefsKey = "language_code";
    private const string ResourcePath = "Localization/tinyfixers_localization";

    private static bool loaded;
    private static string defaultLanguage = "tr";
    private static string currentLanguage;
    private static readonly Dictionary<string, Dictionary<string, string>> tables = new();

    public static event Action OnLanguageChanged;

    public static string CurrentLanguage
    {
        get
        {
            EnsureLoaded();
            return currentLanguage;
        }
    }

    public static void SetLanguage(string languageCode)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(languageCode))
            languageCode = defaultLanguage;

        if (!tables.ContainsKey(languageCode))
            languageCode = defaultLanguage;

        if (currentLanguage == languageCode)
            return;

        currentLanguage = languageCode;
        PlayerPrefs.SetString(LanguagePrefsKey, currentLanguage);
        OnLanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (TryGetValue(currentLanguage, key, out var value))
            return value;

        if (TryGetValue(defaultLanguage, key, out value))
            return value;

        Debug.LogWarning($"[GameLocalization] Key not found: '{key}'");
        return key;
    }

    public static string GetFormat(string key, params object[] args)
    {
        string format = Get(key);

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static bool TryGetValue(string languageCode, string key, out string value)
    {
        value = null;
        return !string.IsNullOrEmpty(languageCode) &&
               tables.TryGetValue(languageCode, out var table) &&
               table.TryGetValue(key, out value);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        tables.Clear();

        TextAsset json = Resources.Load<TextAsset>(ResourcePath);
        if (json != null && !string.IsNullOrWhiteSpace(json.text))
        {
            var data = JsonUtility.FromJson<LocalizationFile>(json.text);
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.defaultLanguage))
                    defaultLanguage = data.defaultLanguage;

                if (data.languages != null)
                {
                    for (int i = 0; i < data.languages.Length; i++)
                    {
                        var language = data.languages[i];
                        if (language == null || string.IsNullOrWhiteSpace(language.code))
                            continue;

                        var table = new Dictionary<string, string>();
                        if (language.entries != null)
                        {
                            for (int j = 0; j < language.entries.Length; j++)
                            {
                                var entry = language.entries[j];
                                if (entry == null || string.IsNullOrEmpty(entry.key))
                                    continue;

                                table[entry.key] = entry.value ?? string.Empty;
                            }
                        }

                        tables[language.code] = table;
                    }
                }
            }
        }

        if (!tables.ContainsKey(defaultLanguage))
            tables[defaultLanguage] = new Dictionary<string, string>();

        currentLanguage = PlayerPrefs.GetString(LanguagePrefsKey, defaultLanguage);
        if (!tables.ContainsKey(currentLanguage))
            currentLanguage = defaultLanguage;
    }

    [Serializable]
    private class LocalizationFile
    {
        public string defaultLanguage;
        public LanguageTable[] languages;
    }

    [Serializable]
    private class LanguageTable
    {
        public string code;
        public LocalizationEntry[] entries;
    }

    [Serializable]
    private class LocalizationEntry
    {
        public string key;
        public string value;
    }
}
