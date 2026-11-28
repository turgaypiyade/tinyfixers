using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bildirim izni ön-popup'ı: OS iznini oyuncu oyunu tanıdıktan sonra, ana menüde bir kez sorar
/// ("Canların dolunca haber verelim mi?"). "Evet" → <see cref="LocalNotificationService.Accept"/> (OS izni),
/// "Hayır"/kapat → <see cref="LocalNotificationService.Decline"/>; bir daha kendiliğinden sorulmaz,
/// Ayarlar'daki Bildirimler anahtarıyla açılabilir.
///
/// Not: oyun fontunda emoji glifi yok → popup metinleri emojisiz; emojiler yalnız sistem bildirimlerinde.
/// </summary>
public sealed class NotificationPermissionPrompt : MonoBehaviour
{
    /// Bu seviyeye gelen (ilk level'ı bitiren) oyuncuya sorulur. 2026-11-17: 4→2 — ertesi gün hatırlatması (D1) için
    /// izin ilk günden alınmalı; 1–4 oyun içinde sürdüğü için pratikte ilk ana menü ziyaretinde sorulur. Daha önce
    /// sorulmuş (ileri) oyuncuyu etkilemez.
    private const int PromptFromLevel = 2;
    private const float ShowDelaySeconds = 1.5f;

    /// Oyun içi peş peşe oyunda (L1–4 menüsüz, <see cref="LevelEndSimplePopupController"/>) sorulduğu level:
    /// 2026-11-28 — ana menü L5'ten sonra açıldığı için erken bırakanlar (%~40) hiç sorulmuyor, D1 bildirimi gitmiyordu.
    /// L2 kazanılıp "Sonraki Seviye"ye basılınca sorulur; L1'de sormak (oyunu tanımadan) reddi artırır.
    public const int InGamePromptLevel = 2;

    private static bool s_scheduledThisSession;

    private System.Action onAnswered;
    private bool inGame;
    private bool answered;

    /// Bu oturumda popup açıldı/açılacak — diğer ana menü pencereleri (mağaza değerlendirmesi) üst üste binmesin.
    public static bool WillShowThisSession => s_scheduledThisSession;

    /// Popup ekranda (kurulmuş). Ana menü öğreticileri bu sürede başlamaz (ProfileUnlockTutorial.IsMenuBusyCore).
    public static bool IsShowing { get; private set; }

    /// Ana menü açılışında çağrılır; koşullar uygunsa kısa bir gecikmeyle popup'ı açar.
    public static void TryShow(Component anyMainMenuUi)
    {
        if (s_scheduledThisSession || anyMainMenuUi == null) return;
        if (LocalNotificationService.Consent != LocalNotificationService.ConsentState.NotAsked) return;
        if (CurrentLevel.Global < PromptFromLevel) return;

        var canvas = anyMainMenuUi.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        s_scheduledThisSession = true;
        var go = new GameObject("NotificationPermissionPrompt", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.layer = canvas.gameObject.layer;   // Screen Space Camera culling tuzağı
        go.AddComponent<NotificationPermissionPrompt>();
    }

    /// Oyun içi peş peşe oyun: koşul uygunsa popup'ı açar, cevaptan sonra <paramref name="onAnswered"/> çağrılır
    /// (sonraki level'a geçiş). false → popup açılmadı, çağıran hemen devam eder.
    public static bool TryShowBeforeNextLevel(Component host, int completedLevel, System.Action onAnswered)
    {
        if (s_scheduledThisSession || host == null || onAnswered == null) return false;
        if (completedLevel < InGamePromptLevel) return false;
        if (LocalNotificationService.Consent != LocalNotificationService.ConsentState.NotAsked) return false;

        var canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        canvas = canvas.rootCanvas;

        s_scheduledThisSession = true;
        var go = new GameObject("NotificationPermissionPrompt", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.layer = canvas.gameObject.layer;   // Screen Space Camera culling tuzağı
        var prompt = go.AddComponent<NotificationPermissionPrompt>();
        prompt.inGame = true;
        prompt.onAnswered = onAnswered;
        return true;
    }

    // Kök obje boş başlar (görsel yok); gecikmeden sonra kurulur.
    private IEnumerator Start()
    {
        if (inGame)
        {
            // Oyuncu düğmeye bastı → bekletmeden aç; ana menü pencereleri (çark/öğretici) bu sahnede yok.
            Build();
            IsShowing = true;
            transform.SetAsLastSibling();
            yield break;
        }
        yield return new WaitForSecondsRealtime(ShowDelaySeconds);
        // Günlük çark açılıştaki ilk pencere (kullanıcı 2026-11-18): çark kapanana dek bekle.
        while (DailySlotEventButton.WheelHasPriority) yield return new WaitForSecondsRealtime(0.25f);
        // Profil öğreticisi ekrandaysa üstüne açılma: karartma popup'ın butonlarını örter, oyuncu kilitlenir
        // (2026-11-22, level atlayarak görüldü). Öğretici bitince sorulur.
        while (ProfileUnlockTutorial.IsVisible) yield return new WaitForSecondsRealtime(0.25f);
        if (LocalNotificationService.Consent != LocalNotificationService.ConsentState.NotAsked)
        {
            Destroy(gameObject);
            yield break;
        }
        Build();
        IsShowing = true;
        transform.SetAsLastSibling();
    }

    private void OnDestroy() => IsShowing = false;

    private void Answer(bool accepted)
    {
        if (answered) return;   // çift dokunuş: tek cevap, tek geçiş
        answered = true;
        if (accepted) LocalNotificationService.Accept();
        else LocalNotificationService.Decline();
        // Ölçüm: izin oranı + nerede soruldu (1 = oyun içi L2, 0 = ana menü). OS izninin sonucu ayrıca bilinmez.
        GameAnalytics.LogEvent("notif_consent", ("accepted", accepted ? 1 : 0),
            ("level", CurrentLevel.Global), ("in_game", inGame ? 1 : 0));
        var next = onAnswered;
        onAnswered = null;
        Destroy(gameObject);
        next?.Invoke();
    }

    private void Build()
    {
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        var skin = CommonPopupSkin.Shared;
        var view = CommonPopupView.Create(transform, GameLocalization.Get("notif_prompt_title"), () => Answer(false));
        // Kullanıcı ayarı (2026-10-27): başlık kutusu üstten 20 yukarı (Inspector Top = -20), yay 17°.
        var titleRt = view.Title.rectTransform;
        titleRt.offsetMax = new Vector2(titleRt.offsetMax.x, 20f);
        if (view.Title.TryGetComponent(out TextArcEffect arc)) arc.SetArcAngle(17f);
        var body = CommonPopupView.Text(view.Body, "Message", GameLocalization.Get("notif_prompt_body"), 46, skin.bodyTextColor, readable: true);
        CommonPopupView.Region(body.rectTransform, new Rect(0, 0, 1, 1));

        var no = CommonPopupView.Button(view.Actions, "BtnNo", GameLocalization.Get("notif_prompt_no"),
            () => Answer(false), skin.secondaryButton);
        CommonPopupView.Region((RectTransform)no.transform, CommonPopupView.ActionRegion(0, 2));
        var yes = CommonPopupView.Button(view.Actions, "BtnYes", GameLocalization.Get("notif_prompt_yes"),
            () => Answer(true), skin.continueButton);
        CommonPopupView.Region((RectTransform)yes.transform, CommonPopupView.ActionRegion(1, 2));
    }
}
