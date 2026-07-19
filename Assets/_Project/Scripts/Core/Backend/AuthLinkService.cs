using System;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Sosyal login (Docs/ProductionPlan.md P4): mevcut ANONİM Firebase hesabını
/// Google / Apple / Facebook kimliğine BAĞLAR (link) — uid değişmez, ilerleme kaybolmaz.
///
/// Akış:
///  1) Provider SDK'sı token üretir (Google idToken, Apple idToken+rawNonce, FB accessToken).
///     SDK'lar projeye eklenene dek ilgili TokenProvider null'dır → buton "yakında" kalır.
///  2) LinkWithCredential: başarı → aynı uid artık kalıcı kimlikli.
///  3) Çakışma ("credential already in use" — bu sosyal hesap BAŞKA bir uid'e bağlı):
///     OnLinkConflict tetiklenir; kullanıcı "o hesaba geç" derse SwitchToExisting →
///     SignInWithCredential (uid DEĞİŞİR; cloud save yeni uid'den restore edilir,
///     "yüksek level kazanır" politikası veri kaybını önler).
///
/// KULLANICI ADIMLARI (console/SDK):
///  - Firebase Console → Authentication → Sign-in method → Google/Apple/Facebook aç.
///  - Google Sign-In SDK + iOS URL scheme; Apple: SIWA plugin (iOS'ta üçüncü parti login
///    sunuluyorsa Apple ZORUNLU); Facebook SDK + App ID. Ayrıntı: ProductionPlan P4.
/// </summary>
public static class AuthLinkService
{
    public enum Provider { Google, Apple, Facebook }

    /// <summary>Bağlama başarıyla bitti (provider adıyla).</summary>
    public static event Action<Provider> OnLinked;

    /// <summary>Bu sosyal hesap başka bir oyuncu kaydına bağlı — kullanıcıya sor.</summary>
    public static event Action<Provider, Credential> OnLinkConflict;

    /// <summary>Hata (iptal dahil) — UI mesaj gösterebilir.</summary>
    public static event Action<Provider, string> OnLinkFailed;

    /// <summary>
    /// Provider token sağlayıcıları — ilgili SDK entegre edilince atanır
    /// (örn. GoogleSignInBridge Google'ı doldurur). null = buton pasif/"yakında".
    /// Callback: (credential, hata) — kullanıcı iptalinde her ikisi null.
    /// </summary>
    public static Action<Action<Credential, string>> GoogleTokenProvider;
    public static Action<Action<Credential, string>> AppleTokenProvider;
    public static Action<Action<Credential, string>> FacebookTokenProvider;

    public static bool IsAvailable(Provider p) => p switch
    {
        Provider.Google => GoogleTokenProvider != null,
        Provider.Apple => AppleTokenProvider != null,
        Provider.Facebook => FacebookTokenProvider != null,
        _ => false,
    };

    /// <summary>Şu anki kullanıcı kalıcı bir kimliğe bağlı mı (anonim değil mi)?</summary>
    public static bool IsLinked
    {
        get
        {
            var user = FirebaseAuth.DefaultInstance?.CurrentUser;
            return user != null && !user.IsAnonymous;
        }
    }

    public static void Link(Provider provider)
    {
        var fetch = provider switch
        {
            Provider.Google => GoogleTokenProvider,
            Provider.Apple => AppleTokenProvider,
            Provider.Facebook => FacebookTokenProvider,
            _ => null,
        };

        if (fetch == null)
        {
            OnLinkFailed?.Invoke(provider, "Bu giriş yöntemi henüz aktif değil.");
            return;
        }

        fetch((credential, error) =>
        {
            if (credential == null)
            {
                if (!string.IsNullOrEmpty(error))
                    OnLinkFailed?.Invoke(provider, error);
                return;   // sessiz iptal
            }
            LinkCredential(provider, credential);
        });
    }

    private static void LinkCredential(Provider provider, Credential credential)
    {
        var user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            OnLinkFailed?.Invoke(provider, "Oturum hazır değil, tekrar dene.");
            return;
        }

        user.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled)
            {
                Debug.Log($"[AuthLink] {provider} bağlandı ✅ uid korunmuş: {user.UserId}");
                OnLinked?.Invoke(provider);
                return;
            }

            // Çakışma: bu sosyal kimlik başka uid'e bağlı.
            var baseEx = task.Exception?.GetBaseException();
            if (baseEx is FirebaseAccountLinkException || (baseEx?.Message?.Contains("already in use") ?? false)
                || (baseEx?.Message?.Contains("already associated") ?? false))
            {
                Debug.Log($"[AuthLink] {provider} çakışma — mevcut kayıt var, kullanıcıya soruluyor.");
                OnLinkConflict?.Invoke(provider, credential);
                return;
            }

            OnLinkFailed?.Invoke(provider, baseEx?.Message ?? "Bağlantı hatası.");
        });
    }

    /// <summary>
    /// Çakışmada kullanıcı "o hesaba geç" derse: sosyal kimliğin BAĞLI OLDUĞU hesaba
    /// giriş yapılır (uid değişir). Ardından cloud save yeni uid'den restore edilir —
    /// mevcut cihaz ilerlemesi "yüksek level kazanır" kuralıyla korunur/birleşir.
    /// </summary>
    public static void SwitchToExisting(Provider provider, Credential credential)
    {
        FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                OnLinkFailed?.Invoke(provider, task.Exception?.GetBaseException().Message ?? "Giriş hatası.");
                return;
            }

            Debug.Log($"[AuthLink] {provider} mevcut hesaba geçildi → uid: {task.Result.UserId}");
            OnLinked?.Invoke(provider);
            // Not: FirebaseAuthService.UserId eski uid'i tutuyor olabilir; tam geçiş
            // uygulama yeniden başlatılınca netleşir. UI "yeniden başlat" önerir.
        });
    }
}
