using UnityEditor;
using UnityEngine;

public sealed class PlayerPrefsDebugTool : EditorWindow
{
    private const string KeyCoins = "player_coins";
    private const string KeyTotalStars = "player_total_stars";
    private const string KeyLevelStarsPrefix = "level_stars_";
    private const string KeyCurrentLevel = "current_level";
    private const int MaxLevelStarsReset = 500;

    private int setCoinsTo;
    private int setTotalStarsTo;
    private int setCurrentLevelTo;
    private Vector2 scroll;

    [MenuItem("TinyFixers/Debug/PlayerPrefs Tool")]
    private static void Open()
    {
        var window = GetWindow<PlayerPrefsDebugTool>("PlayerPrefs Tool");
        window.minSize = new Vector2(360f, 420f);
        window.RefreshEditableFields();
        window.Show();
    }

    private void OnEnable()
    {
        RefreshEditableFields();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawCurrentValues();
        EditorGUILayout.Space(12f);
        DrawEditableValues();
        EditorGUILayout.Space(12f);
        DrawResetButtons();

        EditorGUILayout.EndScrollView();
    }

    private void DrawCurrentValues()
    {
        EditorGUILayout.LabelField("Current Values", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("PlayerWallet.Coins", PlayerWallet.Coins);
            EditorGUILayout.IntField("PlayerWallet.TotalStars", PlayerWallet.TotalStars);
            EditorGUILayout.IntField("current_level", PlayerPrefs.GetInt(KeyCurrentLevel, 1));
        }

        if (GUILayout.Button("Refresh Fields"))
            RefreshEditableFields();
    }

    private void DrawEditableValues()
    {
        EditorGUILayout.LabelField("Set Values", EditorStyles.boldLabel);

        setCoinsTo = Mathf.Max(0, EditorGUILayout.IntField("Set Coins To", setCoinsTo));
        if (GUILayout.Button("Apply Coins"))
        {
            if (Confirm("Set Coins", $"Set {KeyCoins} to {setCoinsTo}?"))
                SetIntAndSave(KeyCoins, setCoinsTo);
        }

        EditorGUILayout.Space(4f);
        setTotalStarsTo = Mathf.Max(0, EditorGUILayout.IntField("Set Total Stars To", setTotalStarsTo));
        if (GUILayout.Button("Apply Total Stars"))
        {
            if (Confirm("Set Total Stars", $"Set {KeyTotalStars} to {setTotalStarsTo}?"))
                SetIntAndSave(KeyTotalStars, setTotalStarsTo);
        }

        EditorGUILayout.Space(4f);
        setCurrentLevelTo = Mathf.Max(1, EditorGUILayout.IntField("Set Current Level To", setCurrentLevelTo));
        if (GUILayout.Button("Apply Current Level"))
        {
            if (Confirm("Set Current Level", $"Set {KeyCurrentLevel} to {setCurrentLevelTo}?"))
                SetIntAndSave(KeyCurrentLevel, setCurrentLevelTo);
        }
    }

    private void DrawResetButtons()
    {
        EditorGUILayout.LabelField("Reset", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset Coins Only"))
        {
            if (Confirm("Reset Coins", $"Set {KeyCoins} to 0?"))
                SetIntAndSave(KeyCoins, 0);
        }

        if (GUILayout.Button("Reset Stars Only"))
        {
            if (Confirm("Reset Stars", $"Delete {KeyTotalStars} and {KeyLevelStarsPrefix}1..{MaxLevelStarsReset}?"))
                ResetStarsOnly();
        }

        if (GUILayout.Button("Reset Level Progress"))
        {
            if (Confirm("Reset Level Progress", $"Set {KeyCurrentLevel} to 1?"))
                SetIntAndSave(KeyCurrentLevel, 1);
        }

        if (GUILayout.Button("Reset Wallet + Stars + Progress"))
        {
            if (Confirm("Reset Wallet + Stars + Progress", "Reset coins, total stars, level stars, and current_level?"))
                ResetWalletStarsAndProgress();
        }

        EditorGUILayout.Space(8f);

        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
        if (GUILayout.Button("Delete All PlayerPrefs"))
        {
            if (EditorUtility.DisplayDialog(
                    "Delete All PlayerPrefs",
                    "This will delete every PlayerPrefs key for this project. Continue?",
                    "Delete All",
                    "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                RefreshEditableFields();
            }
        }
        GUI.backgroundColor = previousBackgroundColor;
    }

    private void RefreshEditableFields()
    {
        setCoinsTo = Mathf.Max(0, PlayerWallet.Coins);
        setTotalStarsTo = Mathf.Max(0, PlayerWallet.TotalStars);
        setCurrentLevelTo = Mathf.Max(1, PlayerPrefs.GetInt(KeyCurrentLevel, 1));
        Repaint();
    }

    private static bool Confirm(string title, string message)
    {
        return EditorUtility.DisplayDialog(title, message, "Confirm", "Cancel");
    }

    private void SetIntAndSave(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        RefreshEditableFields();
    }

    private void ResetStarsOnly()
    {
        PlayerPrefs.DeleteKey(KeyTotalStars);

        for (int i = 1; i <= MaxLevelStarsReset; i++)
            PlayerPrefs.DeleteKey(KeyLevelStarsPrefix + i);

        PlayerPrefs.Save();
        RefreshEditableFields();
    }

    private void ResetWalletStarsAndProgress()
    {
        PlayerPrefs.SetInt(KeyCoins, 0);
        PlayerPrefs.SetInt(KeyCurrentLevel, 1);
        PlayerPrefs.DeleteKey(KeyTotalStars);

        for (int i = 1; i <= MaxLevelStarsReset; i++)
            PlayerPrefs.DeleteKey(KeyLevelStarsPrefix + i);

        PlayerPrefs.Save();
        RefreshEditableFields();
    }
}
