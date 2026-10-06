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
    /// Bu seviyeye gelen (ilk 3 level'ı bitiren) oyuncuya sorulur.
    private const int PromptFromLevel = 4;
    private const float ShowDelaySeconds = 1.5f;

    private static bool s_scheduledThisSession;

    /// Bu oturumda popup açıldı/açılacak — diğer ana menü pencereleri (mağaza değerlendirmesi) üst üste binmesin.
    public static bool WillShowThisSession => s_scheduledThisSession;

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

    // Kök obje boş başlar (görsel yok); gecikmeden sonra kurulur.
    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(ShowDelaySeconds);
        if (LocalNotificationService.Consent != LocalNotificationService.ConsentState.NotAsked)
        {
            Destroy(gameObject);
            yield break;
        }
        Build();
        transform.SetAsLastSibling();
    }

    private void Answer(bool accepted)
    {
        if (accepted) LocalNotificationService.Accept();
        else LocalNotificationService.Decline();
        Destroy(gameObject);
    }

    private void Build()
    {
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        var skin = CommonPopupSkin.Shared;
        var view = CommonPopupView.Create(transform, GameLocalization.Get("notif_prompt_title"), () => Answer(false));
        var body = CommonPopupView.Text(view.Body, "Message", GameLocalization.Get("notif_prompt_body"), 46, skin.bodyTextColor);
        CommonPopupView.Region(body.rectTransform, new Rect(0, 0, 1, 1));

        var no = CommonPopupView.Button(view.Actions, "BtnNo", GameLocalization.Get("notif_prompt_no"),
            () => Answer(false), skin.secondaryButton);
        CommonPopupView.Region((RectTransform)no.transform, CommonPopupView.ActionRegion(0, 2));
        var yes = CommonPopupView.Button(view.Actions, "BtnYes", GameLocalization.Get("notif_prompt_yes"),
            () => Answer(true), skin.continueButton);
        CommonPopupView.Region((RectTransform)yes.transform, CommonPopupView.ActionRegion(1, 2));
    }
}
