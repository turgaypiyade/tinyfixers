using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DailySlotEventButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject notifBadge;
    [SerializeField] private FortuneWheelController fortuneWheel;

    [Header("Bildirim rozeti (ortak UiBadge görünümü)")]
    [SerializeField, Min(8f)] private float badgeSize = 40f;
    [Tooltip("Butonun sağ üst köşesine göre rozet merkezi (yuvarlak ikonun kenarına oturur).")]
    [SerializeField] private Vector2 badgeCornerOffset = new Vector2(-26f, -26f);

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
        if (notifBadge != null && notifBadge.TryGetComponent(out Image badgeImage))
            UiBadge.StyleDot(badgeImage, badgeSize, badgeCornerOffset);
    }

    private void OnEnable()
    {
        RefreshBadge();
        // Sekme değişimiyle yarıda kalan "sakin olunca aç" beklemesi Ana Menü'ye dönünce sürer.
        if (retryWhenCalm && AutoOpenEligible()) StartCoroutine(AutoOpenRoutine(holdPriority: false));
    }

    // ── Günün ilk ana menü ziyaretinde çark kendiliğinden açılır — AÇILIŞIN İLK PENCERESİ ─────────────────────────────
    // Kullanıcı 2026-11-18: "hak varsa ekrana girer girmez, başka şey açılmadan çark açılsın, önceliği olsun".
    // Hak varsa menü yüklenir yüklenmez çark açılır; o sürede diğer açılış pencereleri (bildirim izni, event davetleri,
    // görev/profil/takım öğreticileri) WheelHasPriority'yi görüp çark kapanana dek bekler. Yerel gün başına en fazla bir
    // kez; level 1–5 (oyun içi ilk oturum) etkilenmez. Oyuncu o kısa sürede seviye penceresini açtıysa çark açılmaz.
    private const int AutoOpenFromLevel = 6;
    private const float AutoOpenDelaySeconds = 0.4f;     // menü giriş animasyonu otursun
    private const float AutoOpenMaxWaitSeconds = 8f;     // yükleme/kabul ekranı/ödül uçuşu en fazla bu kadar beklenir
    private const string KeyAutoOpenDay = "wheel_auto_open_day";
    // Hak kullanılmadıkça menüye dönüşlerde tekrar açılır (kullanıcı 2026-11-19: kapatıp dönünce bir daha çıkmadı),
    // ama günde en fazla 3 kez ve iki açılış arası en az 3 dk — her seviye dönüşünde ısrar etmesin.
    private const string KeyAutoOpenCount = "wheel_auto_open_count";
    private const string KeyAutoOpenLast = "wheel_auto_open_last";
    private const int AutoOpenMaxPerDay = 3;
    private const double AutoOpenMinGapSeconds = 180;

    private static bool s_autoOpenPending;

    /// Çark bu ziyarette açılmak üzere ya da açık: diğer açılış pencereleri/öğreticiler beklemeli.
    public static bool WheelHasPriority => s_autoOpenPending || FortuneWheelController.IsOpen;

    private IEnumerator Start()
    {
        if (!AutoOpenEligible()) yield break;
        yield return AutoOpenRoutine(holdPriority: true);
    }

    // Günlük kurallar: hak var, level ≥6, Usta Ligi değil, günde ≤3, iki açılış arası ≥3 dk.
    private bool AutoOpenEligible()
    {
        if (fortuneWheel == null || !FortuneWheelController.HasAvailableSpin() || FortuneWheelController.IsOpen) return false;
        if (CurrentLevel.Global < AutoOpenFromLevel || MasterLeague.IsPlaying) return false;
        string today = DateTime.Now.ToString("yyyyMMdd");
        int countToday = PlayerPrefs.GetString(KeyAutoOpenDay, "") == today ? PlayerPrefs.GetInt(KeyAutoOpenCount, 1) : 0;
        if (countToday >= AutoOpenMaxPerDay) return false;
        double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return countToday == 0 || now - double.Parse(PlayerPrefs.GetString(KeyAutoOpenLast, "0")) >= AutoOpenMinGapSeconds;
    }

    // Kullanıcı 2026-11-22 (kuzen: hak vardı, çark açılmadı): eskiden 8 sn içinde menü sakinleşmezse ya da ekranda
    // pencere varsa o ziyaret için VAZGEÇİLİYORDU. Artık önce öncelikli denenir (eski davranış); olmazsa öncelik bırakılır
    // (öğreticiler/davetler çarkı beklerken çark da onları beklemesin → kilitlenme yok) ve menü sakinleşince açılır.
    // Sekme değişip panel gizlenirse coroutine durur; Ana Menü'ye dönülünce OnEnable kaldığı yerden sürdürür.
    private bool retryWhenCalm;
    private const float CalmPollSeconds = 0.5f;   // bekleme sırasında popup taraması seyrek (FindObjectsByType)

    private IEnumerator AutoOpenRoutine(bool holdPriority)
    {
        if (holdPriority)
        {
            s_autoOpenPending = true;
            try
            {
                yield return new WaitForSecondsRealtime(AutoOpenDelaySeconds);
                // Yalnız gerçek engeller beklenir (yükleme ekranı, şartlar kabulü, ödül uçuşu, satın alma/sandık penceresi).
                float deadline = Time.unscaledTime + AutoOpenMaxWaitSeconds;
                while (ProfileUnlockTutorial.IsMenuBusyCore() && Time.unscaledTime <= deadline)
                {
                    if (this == null || !isActiveAndEnabled) yield break;
                    yield return new WaitForSecondsRealtime(0.2f);
                }
                if (this == null || !isActiveAndEnabled) yield break;
                if (!ProfileUnlockTutorial.IsMenuBusyCore() && !AnyPopupVisible())
                {
                    TryOpenNow();
                    yield break;
                }
            }
            finally { s_autoOpenPending = false; }
        }

        // Önceliksiz bekleme: menü sakin + pencere yok → aç. Kurallar her turda yeniden kontrol edilir.
        retryWhenCalm = true;
        while (retryWhenCalm)
        {
            if (this == null || !isActiveAndEnabled) yield break;
            if (!AutoOpenEligible()) { retryWhenCalm = false; yield break; }
            if (!ProfileUnlockTutorial.IsMenuBusyCore() && !AnyPopupVisible() && !WheelBlockedByOthers())
            {
                retryWhenCalm = false;
                TryOpenNow();
                yield break;
            }
            yield return new WaitForSecondsRealtime(CalmPollSeconds);
        }
    }

    // Çark önceliği bıraktıktan sonra başlayan öğretici/overlay'in üstüne açılmasın.
    private static bool WheelBlockedByOthers() => FindFirstObjectByType<TutorialOverlayController>() != null;

    private void TryOpenNow()
    {
        if (!AutoOpenEligible()) return;
        string today = DateTime.Now.ToString("yyyyMMdd");
        int countToday = PlayerPrefs.GetString(KeyAutoOpenDay, "") == today ? PlayerPrefs.GetInt(KeyAutoOpenCount, 1) : 0;
        PlayerPrefs.SetString(KeyAutoOpenDay, today);
        PlayerPrefs.SetInt(KeyAutoOpenCount, countToday + 1);
        PlayerPrefs.SetString(KeyAutoOpenLast, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
        fortuneWheel.Open();
        RefreshBadge();
    }

    private void OnDestroy() => s_autoOpenPending = false;

    private static bool AnyPopupVisible()
    {
        var popups = FindObjectsByType<PopupEntranceAnimator>(FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++) if (popups[i].IsVisible) return true;
        return false;
    }

    private void OnClicked()
    {
        if (fortuneWheel != null) fortuneWheel.Open();
        RefreshBadge();
    }

    private void RefreshBadge()
    {
        if (notifBadge != null)
            notifBadge.SetActive(FortuneWheelController.HasAvailableSpin());
    }
}
