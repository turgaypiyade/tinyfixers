using UnityEngine;

public class PlayerPrefsSaveStore : ISaveStore
{
    public string Load(string key, string defaultValue = "") =>
        PlayerPrefs.GetString(key, defaultValue);

    public void Save(string key, string value)
    {
        if (RuntimeSimulationSession.IsActive) return;
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }

    public void Delete(string key)
    {
        if (RuntimeSimulationSession.IsActive) return;
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
