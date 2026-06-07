using UnityEngine;

public class PlayerPrefsSaveStore : ISaveStore
{
    public string Load(string key, string defaultValue = "") =>
        PlayerPrefs.GetString(key, defaultValue);

    public void Save(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }

    public void Delete(string key)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
