using UnityEditor;
using UnityEngine;

public sealed class PlayerPrefsDebugTool : EditorWindow
{
    private const string KeyCoins = "player_coins";
    private const string KeyTotalStars = "player_total_stars";
    private const string KeyLevelStarsPrefix = "level_stars_";
    private const string KeyCurrentLevel = "current_level";

    private const string KeyPendingStarReward = "pending_star_reward";
    private const string KeyPendingStarBefore = "pending_star_before";
    private const string KeyPendingStarAfter = "pending_star_after";

    private const int MaxLevelStarsReset = 500;

    private int setCoinsTo;
    private int setTotalStarsTo;
    private int setCurrentLevelTo;

    private int inspectLevelStarsLevel;
    private int setLevelStarsTo;

    private Vector2 scroll;

    [MenuItem("TinyFixers/Debug/PlayerPrefs Tool")]
    private static void Open()
    {
        var window = GetWindow<PlayerPrefsDebugTool>("PlayerPrefs Tool");
        window.minSize = new Vector2(420f, 560f);
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
        DrawLevelStarsValues();

        EditorGUILayout.Space(12f);
        DrawPendingStarValues();

        EditorGUILayout.Space(12f);
        DrawResetButtons();

        EditorGUILayout.EndScrollView();
    }

    private void DrawCurrentValues()
    {
        EditorGUILayout.LabelField("Current Values", EditorStyles.boldLabel);

        int currentLevel = PlayerPrefs.GetInt(KeyCurrentLevel, 1);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("PlayerWallet.Coins", PlayerWallet.Coins);
            EditorGUILayout.IntField("PlayerWallet.TotalStars", PlayerWallet.TotalStars);
            EditorGUILayout.IntField("current_level", currentLevel);
            EditorGUILayout.IntField($"level_stars_{currentLevel}", GetLevelStars(currentLevel));
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
            {
                SetIntAndSave(KeyCurrentLevel, setCurrentLevelTo);
                inspectLevelStarsLevel = setCurrentLevelTo;
                setLevelStarsTo = GetLevelStars(inspectLevelStarsLevel);
            }
        }
    }

    private void DrawLevelStarsValues()
    {
        EditorGUILayout.LabelField("Per-Level Stars", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Kazanilmis yildizlar PlayerPrefs icinde level_stars_<level> key'i ile tutulur. Ornek: Level 1 => level_stars_1.",
            MessageType.Info);

        inspectLevelStarsLevel = Mathf.Max(1, EditorGUILayout.IntField("Inspect Level", inspectLevelStarsLevel));

        string key = GetLevelStarsKey(inspectLevelStarsLevel);
        int currentStars = GetLevelStars(inspectLevelStarsLevel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("PlayerPrefs Key", key);
            EditorGUILayout.IntField("Current Level Stars", currentStars);
        }

        setLevelStarsTo = Mathf.Clamp(EditorGUILayout.IntField("Set Level Stars To", setLevelStarsTo), 0, 3);

        if (GUILayout.Button("Apply Level Stars And Adjust Total"))
        {
            if (Confirm(
                    "Set Level Stars",
                    $"Set {key} to {setLevelStarsTo} and adjust {KeyTotalStars} by the delta?"))
            {
                SetLevelStarsAndAdjustTotal(inspectLevelStarsLevel, setLevelStarsTo);
            }
        }

        if (GUILayout.Button("Delete This Level Stars And Adjust Total"))
        {
            if (Confirm(
                    "Delete Level Stars",
                    $"Delete {key} and subtract its current value from {KeyTotalStars}?"))
            {
                DeleteLevelStarsAndAdjustTotal(inspectLevelStarsLevel);
            }
        }

        if (GUILayout.Button("Recalculate Total Stars From Level Keys"))
        {
            if (Confirm(
                    "Recalculate Total Stars",
                    $"Recalculate {KeyTotalStars} from {KeyLevelStarsPrefix}1..{MaxLevelStarsReset}?"))
            {
                RecalculateTotalStarsFromLevelKeys();
            }
        }
    }

    private void DrawPendingStarValues()
    {
        EditorGUILayout.LabelField("Pending Star Animation", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField(KeyPendingStarReward, PlayerPrefs.GetInt(KeyPendingStarReward, 0));
            EditorGUILayout.IntField(KeyPendingStarBefore, PlayerPrefs.GetInt(KeyPendingStarBefore, PlayerWallet.TotalStars));
            EditorGUILayout.IntField(KeyPendingStarAfter, PlayerPrefs.GetInt(KeyPendingStarAfter, PlayerWallet.TotalStars));
        }

        if (GUILayout.Button("Clear Pending Star Animation"))
        {
            if (Confirm("Clear Pending Star Animation", "Delete pending star animation PlayerPrefs keys?"))
            {
                PlayerPrefs.DeleteKey(KeyPendingStarReward);
                PlayerPrefs.DeleteKey(KeyPendingStarBefore);
                PlayerPrefs.DeleteKey(KeyPendingStarAfter);
                PlayerPrefs.Save();
                RefreshEditableFields();
            }
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

        if (inspectLevelStarsLevel <= 0)
            inspectLevelStarsLevel = setCurrentLevelTo;

        setLevelStarsTo = GetLevelStars(inspectLevelStarsLevel);

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

    private static string GetLevelStarsKey(int level)
    {
        return KeyLevelStarsPrefix + Mathf.Max(1, level);
    }

    private static int GetLevelStars(int level)
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(GetLevelStarsKey(level), 0), 0, 3);
    }

    private void SetLevelStarsAndAdjustTotal(int level, int stars)
    {
        level = Mathf.Max(1, level);
        stars = Mathf.Clamp(stars, 0, 3);

        int previousStars = GetLevelStars(level);
        int totalStars = Mathf.Max(0, PlayerPrefs.GetInt(KeyTotalStars, 0));
        int newTotalStars = Mathf.Max(0, totalStars + stars - previousStars);

        if (stars <= 0)
            PlayerPrefs.DeleteKey(GetLevelStarsKey(level));
        else
            PlayerPrefs.SetInt(GetLevelStarsKey(level), stars);

        PlayerPrefs.SetInt(KeyTotalStars, newTotalStars);
        PlayerPrefs.Save();

        RefreshEditableFields();
    }

    private void DeleteLevelStarsAndAdjustTotal(int level)
    {
        level = Mathf.Max(1, level);

        int previousStars = GetLevelStars(level);
        int totalStars = Mathf.Max(0, PlayerPrefs.GetInt(KeyTotalStars, 0));

        PlayerPrefs.DeleteKey(GetLevelStarsKey(level));
        PlayerPrefs.SetInt(KeyTotalStars, Mathf.Max(0, totalStars - previousStars));
        PlayerPrefs.Save();

        RefreshEditableFields();
    }

    private void RecalculateTotalStarsFromLevelKeys()
    {
        int total = 0;

        for (int i = 1; i <= MaxLevelStarsReset; i++)
            total += GetLevelStars(i);

        PlayerPrefs.SetInt(KeyTotalStars, total);
        PlayerPrefs.Save();

        RefreshEditableFields();
    }

    private void ResetStarsOnly()
    {
        PlayerPrefs.DeleteKey(KeyTotalStars);
        PlayerPrefs.DeleteKey(KeyPendingStarReward);
        PlayerPrefs.DeleteKey(KeyPendingStarBefore);
        PlayerPrefs.DeleteKey(KeyPendingStarAfter);

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
        PlayerPrefs.DeleteKey(KeyPendingStarReward);
        PlayerPrefs.DeleteKey(KeyPendingStarBefore);
        PlayerPrefs.DeleteKey(KeyPendingStarAfter);

        for (int i = 1; i <= MaxLevelStarsReset; i++)
            PlayerPrefs.DeleteKey(KeyLevelStarsPrefix + i);

        PlayerPrefs.Save();
        RefreshEditableFields();
    }
}