using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dünya haritasının beyni. Sıralı bölge listesini tutar, açılma durumlarını yükler,
/// yıldız HARCAYARAK bölge açar (sis kalkar + kutlama VFX/SFX).
///
/// Görev listesi (RegionUnlockListPanel) buradaki sıralı listeyi okur ve TryUnlock çağırır.
/// </summary>
public sealed class WorldMapController : MonoBehaviour
{
    [Tooltip("Açılış SIRASIYLA tüm bölgeler. Liste, kilitli bölgeleri bu sırayla gösterir.")]
    [SerializeField] private WorldMapRegion[] regions;

    [Header("Kutlama (açılınca, bölgenin RevealFocus'unda)")]
    [SerializeField] private GameObject sparkleBurstPrefab;
    [SerializeField] private GameObject confettiPrefab;
    [SerializeField, Min(0f)] private float vfxLifetime = 2f;
    [Tooltip("Yıldızlar hedefe varsın diye sis kalkmadan önceki bekleme (sinematik senkron).")]
    [SerializeField, Min(0f)] private float starImpactDelay = 0.45f;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip unlockSfx;

    /// <summary>Bir bölge açıldığında (sis kalkma bitince) tetiklenir.</summary>
    public event Action<WorldMapRegion> OnRegionUnlocked;

    /// <summary>Şu an bir sis kalkma animasyonu sürüyor mu? (panel buna göre bekler)</summary>
    public bool IsRevealing { get; private set; }

    public IReadOnlyList<WorldMapRegion> Regions => regions;

    private void OnEnable() => ApplyAllInstant();

    private void ApplyAllInstant()
    {
        if (regions == null) return;
        for (int i = 0; i < regions.Length; i++)
            if (regions[i] != null) regions[i].ApplyInstant();
    }

    /// <summary>Sırayla ilk N kilitli bölgeyi döndürür (görev listesi için).</summary>
    public List<WorldMapRegion> GetLockedRegions(int max = int.MaxValue)
    {
        var list = new List<WorldMapRegion>();
        if (regions == null) return list;
        for (int i = 0; i < regions.Length && list.Count < max; i++)
        {
            var r = regions[i];
            if (r != null && !r.IsUnlocked) list.Add(r);
        }
        return list;
    }

    public bool AllUnlocked => GetLockedRegions(1).Count == 0;

    public bool CanUnlock(WorldMapRegion region) =>
        region != null && !region.IsUnlocked && !IsRevealing &&
        PlayerWallet.HasEnoughStars(region.StarCost);

    /// <summary>
    /// Bölgeyi açmayı dener: yıldız yeterliyse harcar, sis kalkma sekansını başlatır.
    /// Yıldız uçma sinematiğini panel yürütür; bu yalnızca harca + reveal yapar.
    /// </summary>
    public bool TryUnlock(WorldMapRegion region)
    {
        if (region == null || region.IsUnlocked) return false;
        if (IsRevealing) return false;
        if (!PlayerWallet.HasEnoughStars(region.StarCost)) return false;
        if (!PlayerWallet.SpendStars(region.StarCost)) return false;

        StartCoroutine(UnlockSequence(region));
        return true;
    }

    private IEnumerator UnlockSequence(WorldMapRegion region)
    {
        IsRevealing = true;

        // Yıldızlar yola çıkıp hedefe varana kadar bekle (panel paralel uçuruyor).
        if (starImpactDelay > 0f)
            yield return new WaitForSeconds(starImpactDelay);

        var focus = region.RevealFocus;
        SpawnVfx(sparkleBurstPrefab, focus);
        SpawnVfx(confettiPrefab, focus);
        if (GameSettings.SoundEnabled && unlockSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(unlockSfx);

        yield return region.RevealRoutine();

        IsRevealing = false;
        OnRegionUnlocked?.Invoke(region);
    }

    private void SpawnVfx(GameObject prefab, RectTransform at)
    {
        if (prefab == null || at == null) return;
        var go = Instantiate(prefab, at);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
        if (vfxLifetime > 0f) Destroy(go, vfxLifetime);
    }
}
