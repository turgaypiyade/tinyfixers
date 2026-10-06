using System.Collections.Generic;
using System.Text;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Level ayarlarını yeni sürüm yayınlamadan, HERKES için merkezden değiştirme (Firestore).
/// Şu an: hamle sayısı. Örn. level 100'ü 100 kişiden 10'u geçiyorsa Console'dan 23 → 27 yapılır;
/// oyuncular bir sonraki açılışta (veya uygulamaya dönüşte) yeni değeri alır.
///
/// Firestore: config/levels dokümanı, "moves" MAP alanı → anahtar = level no (metin), değer = hamle (sayı)
///   moves: { "100": 27, "150": 30 }
/// Kural: config/* herkes okur, kimse yazamaz (yalnız Console). firestore.rules'a eklendi — Console'da Publish gerekir.
///
/// Değerler cihazda önbelleğe yazılır (PlayerPrefs) → çevrimdışıyken ve ilk okuma bitmeden de son bilinen değer
/// kullanılır. Uygulama: GridSpawner level'ın runtime kopyasına yazar → HUD/yıldız/skor hepsi aynı değeri görür.
/// Simülasyon etkilenmez (level dosyasının ham değerini ölçer).
/// </summary>
public static class LevelRemoteTuning
{
    private const string CacheKey = "remote_level_moves";   // "100:27,150:30"
    private const float RefreshAfterSeconds = 10f * 60f;
    private const int MinMoves = 1;
    private const int MaxMoves = 200;

    private static readonly Dictionary<int, int> s_moves = new();
    private static bool s_loaded;
    private static float s_lastFetchTime = -999f;
    private static bool s_fetchInFlight;

    /// Bu level için geçerli hamle sayısı (uzaktan ayar yoksa level dosyasındaki değer).
    public static int MovesFor(int level, int fileMoves)
    {
        if (RuntimeSimulationSession.IsActive) return fileMoves;
        EnsureLoaded();
        return s_moves.TryGetValue(level, out int moves) ? moves : fileMoves;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
        EnsureLoaded();
        var go = new GameObject("[LevelRemoteTuning]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Hook>();
        FirebaseAuthService.OnReady += Fetch;
    }

    private static void Fetch()
    {
        if (s_fetchInFlight || !FirebaseAuthService.IsReady) return;
        s_fetchInFlight = true;
        s_lastFetchTime = Time.realtimeSinceStartup;

        FirebaseFirestore.DefaultInstance.Collection("config").Document("levels").GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                s_fetchInFlight = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[LevelRemoteTuning] okunamadı: {task.Exception?.GetBaseException().Message}");
                    return;
                }

                var parsed = new Dictionary<int, int>();
                var snap = task.Result;
                if (snap.Exists && snap.TryGetValue("moves", out Dictionary<string, object> map) && map != null)
                {
                    foreach (var kv in map)
                    {
                        if (!int.TryParse(kv.Key, out int level) || level <= 0) continue;
                        if (!TryToInt(kv.Value, out int moves)) continue;
                        parsed[level] = Mathf.Clamp(moves, MinMoves, MaxMoves);
                    }
                }

                s_moves.Clear();
                foreach (var kv in parsed) s_moves[kv.Key] = kv.Value;
                SaveCache();
                Debug.Log($"[LevelRemoteTuning] {s_moves.Count} level hamle ayarı yüklendi.");
            });
    }

    private static bool TryToInt(object value, out int result)
    {
        switch (value)
        {
            case long l: result = (int)l; return true;
            case int i: result = i; return true;
            case double d: result = Mathf.RoundToInt((float)d); return true;
            case string s when int.TryParse(s, out int p): result = p; return true;
            default: result = 0; return false;
        }
    }

    private static void EnsureLoaded()
    {
        if (s_loaded) return;
        s_loaded = true;
        foreach (var pair in PlayerPrefs.GetString(CacheKey, "").Split(','))
        {
            var parts = pair.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int level) && int.TryParse(parts[1], out int moves))
                s_moves[level] = Mathf.Clamp(moves, MinMoves, MaxMoves);
        }
    }

    private static void SaveCache()
    {
        var sb = new StringBuilder();
        foreach (var kv in s_moves)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(kv.Key).Append(':').Append(kv.Value);
        }
        PlayerPrefs.SetString(CacheKey, sb.ToString());
        PlayerPrefs.Save();
    }

    private sealed class Hook : MonoBehaviour
    {
        private void OnApplicationPause(bool paused)
        {
            if (!paused && Time.realtimeSinceStartup - s_lastFetchTime >= RefreshAfterSeconds)
                Fetch();
        }
    }
}
