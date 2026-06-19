using UnityEngine;

/// <summary>
/// Dünya haritası: toplam yıldıza (PlayerWallet.TotalStars) göre bölgelerin sisini açar.
/// İlk açılışta hazır olanlar anında açık; oyun sırasında yıldız artarsa yeni açılan
/// bölge animasyonla (fade) reveal olur.
/// </summary>
public sealed class WorldMapController : MonoBehaviour
{
    [Tooltip("Haritadaki tüm bölgeler. Sıra önemsiz; her biri kendi unlockStars'ına göre açılır.")]
    [SerializeField] private WorldMapRegion[] regions;

    private int lastStars = -1;

    private void OnEnable()
    {
        PlayerWallet.OnTotalStarsChanged += OnStarsChanged;
        int total = PlayerWallet.TotalStars;
        ApplyAll(total, animate: false);   // ilk açılış: anında (animasyonsuz)
        lastStars = total;
    }

    private void OnDisable()
    {
        PlayerWallet.OnTotalStarsChanged -= OnStarsChanged;
    }

    private void OnStarsChanged(int total)
    {
        // Yıldız ARTTIYSA yeni açılanlar animasyonla reveal olsun; azalma/eşitlikte anında.
        ApplyAll(total, animate: total > lastStars);
        lastStars = total;
    }

    private void ApplyAll(int totalStars, bool animate)
    {
        if (regions == null) return;
        for (int i = 0; i < regions.Length; i++)
            if (regions[i] != null) regions[i].Apply(totalStars, animate);
    }
}
