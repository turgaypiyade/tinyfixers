using UnityEngine;

/// <summary>
/// Ana menü müziği. Kütüphane atanmışsa oyuncunun SEÇTİĞİ parçayı çalar
/// (MusicState.SelectedTrack); seçim değişince (profil sayfasından yeni parça)
/// CANLI geçiş yapar. Kütüphane yoksa eski davranış: sabit menuMusic clip'i.
/// </summary>
public class MainMenuMusicStarter : MonoBehaviour
{
    [Header("Seçilebilir müzik (opsiyonel)")]
    [Tooltip("Atanırsa oyuncunun seçtiği parça çalar; boşsa aşağıdaki sabit clip.")]
    [SerializeField] private MusicLibrary library;

    [Header("Sabit müzik (kütüphane yoksa)")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private void Awake()
    {
        // Kütüphaneyi global kaynağa yaz → profil ekranı/popup kendi ref'i olmadan
        // seçili parçanın adını buradan okur (tek yere atamak yeter).
        if (library != null) MusicState.Library = library;
    }

    private void OnEnable() => MusicState.OnChanged += PlaySelected;
    private void OnDisable() => MusicState.OnChanged -= PlaySelected;

    private void Start()
    {
        EnsureMusicManager();
        if (MusicManager.Instance == null)
        {
            Debug.LogWarning("[MainMenuMusicStarter] MusicManager bulunamadı.");
            return;
        }
        PlaySelected();
    }

    private void PlaySelected()
    {
        if (MusicManager.Instance == null) return;

        // Kütüphane + geçerli parça varsa onu çal.
        if (MusicState.TryGetSelectedTrack(out var selectedClip, out var selectedVolume))
        {
            MusicManager.Instance.Play(selectedClip, selectedVolume);
            return;
        }

        // Fallback: sabit clip.
        if (menuMusic != null)
            MusicManager.Instance.Play(menuMusic, volume);
    }

    private void EnsureMusicManager()
    {
        if (MusicManager.Instance != null) return;

        GameObject go = new GameObject("MusicManager");
        go.AddComponent<AudioSource>();
        go.AddComponent<MusicManager>();
    }
}
