using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Cloud save (Docs/ProductionPlan.md P1): oyuncu ilerlemesi Firestore
/// users/{uid}/save/main dokümanında yedeklenir; reinstall/cihaz değişiminde geri gelir.
///
/// Akış:
///  1) Auth hazır olunca bulut dokümanı çekilir.
///  2) Çakışma politikası v1: EN YÜKSEK İLERLEME KAZANIR — bulut level > yerel level ise
///     bulut yerele yazılır (restore), değilse yerel buluta itilir.
///  3) Restore kararı verilmeden HİÇBİR push yapılmaz (taze kurulumun default'ları
///     dolu bulutun üstüne yazılmasın). Fetch hata verirse backoff ile denenir.
///  4) Sonrası: değişim olaylarında (coin/yıldız/skor/arkadaş) + periyodik dirty-check +
///     uygulama pause/quit anında push. Firestore offline cache'i sayesinde çevrimdışı
///     push'lar bir sonraki açılışta senkronlanır.
///
/// Restore erken (intro/loading sırasında) gelir; o âna dek ekrana bağlanmış eski
/// değerler olabilir — OnRestored'a abone olan ekranlar kendini tazeler.
/// </summary>
public static class FirebaseCloudSaveService
{
    private const int SaveVersion = 1;
    private const float DirtyCheckInterval = 15f;   // sn — dirty ise push
    private const float FetchRetryDelay = 10f;      // sn — restore fetch backoff

    /// <summary>Buluttan yerel üzerine veri yazıldığında tetiklenir (ekranlar tazelensin).</summary>
    public static event Action OnRestored;

    /// <summary>Restore kararı verildi mi (başarılı fetch + uygula/it). Push'lar bundan önce kapalı.</summary>
    public static bool RestoreResolved { get; private set; }

    private static bool dirty;
    private static bool pushInFlight;
    private static int lastPushedLevel = -1;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    private static DocumentReference SaveDoc =>
        Db.Collection("users").Document(FirebaseAuthService.UserId)
          .Collection("save").Document("main");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("CloudSave");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<CloudSaveBehaviour>();

        FirebaseAuthService.OnReady += StartRestore;

        // Oyuncu verisini değiştiren olaylar → dirty (push periyodik atılır).
        PlayerWallet.OnCoinsChanged += _ => MarkDirty();
        PlayerWallet.OnTotalStarsChanged += _ => MarkDirty();
        PlayerWallet.OnTotalScoreChanged += _ => MarkDirty();
        FriendState.OnChanged += MarkDirty;
    }

    /// <summary>Kalıcı oyuncu verisi değişti — bir sonraki döngüde buluta yazılır.</summary>
    public static void MarkDirty() => dirty = true;

    // ── Restore ─────────────────────────────────────────────────────

    private static void StartRestore()
    {
        SaveDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning($"[CloudSave] restore fetch hatası, tekrar denenecek: {task.Exception?.GetBaseException().Message}");
                CloudSaveBehaviour.Instance?.ScheduleRetry(FetchRetryDelay, StartRestore);
                return;
            }

            var snap = task.Result;
            int localLevel = PlayerPrefs.GetInt("current_level", 1);

            if (snap.Exists && snap.TryGetValue<long>("level", out long cloudLevel)
                && cloudLevel > localLevel
                && snap.TryGetValue<Dictionary<string, object>>("data", out var cloudData))
            {
                CloudSaveManifest.Apply(cloudData);
                MusicState.ReloadFromPrefs();
                Debug.Log($"[CloudSave] RESTORE ✅ bulut level {cloudLevel} > yerel {localLevel} → bulut uygulandı ({cloudData.Count} anahtar)");
                RestoreResolved = true;
                OnRestored?.Invoke();
            }
            else
            {
                // Yerel ileride (veya bulut boş) → yereli buluta it.
                Debug.Log($"[CloudSave] restore kararı: yerel kazandı (yerel {localLevel}, bulut {(snap.Exists ? "var" : "yok")}) → push");
                RestoreResolved = true;
                Push();
            }
        });
    }

    // ── Push ────────────────────────────────────────────────────────

    /// <summary>Yerel manifest verisini buluta yazar (merge). Restore çözülmeden çalışmaz.</summary>
    public static void Push()
    {
        if (!RestoreResolved || !FirebaseAuthService.IsReady || pushInFlight) return;

        var data = CloudSaveManifest.Collect();
        int level = PlayerPrefs.GetInt("current_level", 1);

        var doc = new Dictionary<string, object>
        {
            { "data", data },
            { "level", level },
            { "ver", SaveVersion },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        dirty = false;
        lastPushedLevel = level;
        pushInFlight = true;

        // Oyuncu dizini profili de aynı ritimde tazelenir (isim/bölüm/avatar değiştiyse).
        PlayerDirectoryService.UpsertIfChanged();

        SaveDoc.SetAsync(doc, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            pushInFlight = false;
            if (task.IsFaulted || task.IsCanceled)
            {
                dirty = true;   // sıradaki döngüde tekrar dene (offline'da SDK zaten kuyruklar)
                Debug.LogWarning($"[CloudSave] push hatası: {task.Exception?.GetBaseException().Message}");
            }
        });
    }

    // ── Host MonoBehaviour: periyodik dirty-check + pause/quit push ──

    private sealed class CloudSaveBehaviour : MonoBehaviour
    {
        public static CloudSaveBehaviour Instance { get; private set; }
        private float timer;

        private void Awake() => Instance = this;

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer < DirtyCheckInterval) return;
            timer = 0f;

            // current_level'in değişim olayı yok — periyodik karşılaştır.
            if (!dirty && RestoreResolved &&
                PlayerPrefs.GetInt("current_level", 1) != lastPushedLevel)
                dirty = true;

            if (dirty) Push();
        }

        // Mobilde uygulamadan çıkışın güvenilir sinyali pause'dur; quit her zaman gelmez.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Push();
        }

        private void OnApplicationQuit() => Push();

        public void ScheduleRetry(float delay, Action action)
            => StartCoroutine(CoRetry(delay, action));

        private System.Collections.IEnumerator CoRetry(float delay, Action action)
        {
            yield return new WaitForSecondsRealtime(delay);
            action?.Invoke();
        }
    }
}
