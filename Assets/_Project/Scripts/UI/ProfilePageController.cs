using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Profil sayfası. Büyük avatar dairesine tıklanınca ALTTAKİ avatar şeridi açılır (popup yok);
/// bir avatara dokununca büyük daire ANINDA güncellenir ve PlayerProfile'a kaydedilir
/// (AvatarView'lar OnChanged ile otomatik yenilenir). İsim inline düzenlenir, focus çıkınca kaydedilir.
/// Avatar/isim düzenleme 10. level sonrası açılır (PlayerProfile.CustomizationUnlocked).
///
/// Dairenin Button'ı → ToggleAvatarStrip(); sayfayı popup gibi açıyorsan Open()/Close() kullan.
/// </summary>
public sealed class ProfilePageController : MonoBehaviour
{
    [Header("Core")]
    [Tooltip("Tüm profil paneli. Sayfa kalıcıysa boş bırakılabilir.")]
    [SerializeField] private GameObject profileRoot;
    [SerializeField] private AvatarLibrary library;
    [SerializeField] private Button closeButton;

    [Header("Avatar (büyük daire = canlı önizleme)")]
    [SerializeField] private Image currentAvatarImage;
    [Tooltip("Büyük dairenin Button'ı — tıklanınca avatar şeridini açar/kapatır.")]
    [SerializeField] private Button avatarCircleButton;

    [Header("İsim")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField, Min(1)] private int nameMaxLength = 12;

    [Header("Avatar Şeridi (alttaki boş alan)")]
    [Tooltip("Şerit paneli (ScrollRect + 'Avatar Seç' başlığı). Başta gizli; daireye tıklayınca açılır.")]
    [SerializeField] private GameObject avatarStripRoot;
    [Tooltip("ScrollRect içeriği — HorizontalLayoutGroup'lu content. Avatar hücreleri buraya dizilir.")]
    [SerializeField] private Transform avatarStripContainer;
    [SerializeField] private AvatarOptionButton avatarOptionPrefab;
    [Tooltip("İstatistik paneli — şerit ile AYNI alanı paylaşır. Daireye tıklayınca gizlenir, " +
             "avatar seçilince/şerit kapanınca geri gelir.")]
    [SerializeField] private GameObject statsRoot;

    [Header("Kilit")]
    [Tooltip("Kilitliyken (10. level öncesi) gösterilecek ipucu. Opsiyonel.")]
    [SerializeField] private GameObject editLockedHint;

    private int selectedAvatarId;
    private bool stripBuilt;
    private readonly List<AvatarOptionButton> options = new();

    private void Awake()
    {
        if (avatarCircleButton != null) avatarCircleButton.onClick.AddListener(ToggleAvatarStrip);
        if (closeButton        != null) closeButton.onClick.AddListener(Close);

        if (nameInput != null)
        {
            nameInput.characterLimit = nameMaxLength;
            nameInput.onEndEdit.AddListener(CommitName);
        }

        if (avatarStripRoot != null) avatarStripRoot.SetActive(false);
        if (profileRoot     != null) profileRoot.SetActive(false);
    }

    // Sayfayı popup gibi açıyorsan dairenin/menünün butonu bunu çağırır.
    public void Open()
    {
        if (profileRoot != null) profileRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (avatarStripRoot != null) avatarStripRoot.SetActive(false);
        if (profileRoot     != null) profileRoot.SetActive(false);
    }

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        selectedAvatarId = PlayerProfile.AvatarId;
        ApplyPreview(selectedAvatarId);

        if (nameInput != null)
        {
            nameInput.SetTextWithoutNotify(PlayerProfile.PlayerName);
            nameInput.interactable = PlayerProfile.CustomizationUnlocked;
        }

        bool unlocked = PlayerProfile.CustomizationUnlocked;
        if (avatarCircleButton != null) avatarCircleButton.interactable = unlocked;
        if (editLockedHint     != null) editLockedHint.SetActive(!unlocked);

        // Açılışta varsayılan görünüm: istatistikler görünür, şerit kapalı.
        ShowAvatarStrip(false);
    }

    private void ToggleAvatarStrip()
    {
        if (!PlayerProfile.CustomizationUnlocked || avatarStripRoot == null)
            return;

        ShowAvatarStrip(!avatarStripRoot.activeSelf);
    }

    // Şerit açıkken istatistikler gizlenir; şerit kapanınca istatistikler geri gelir.
    private void ShowAvatarStrip(bool show)
    {
        if (show)
        {
            BuildStripOnce();
            HighlightSelected();
        }

        if (avatarStripRoot != null) avatarStripRoot.SetActive(show);
        if (statsRoot       != null) statsRoot.SetActive(!show);
    }

    private void BuildStripOnce()
    {
        if (stripBuilt) return;
        if (library == null || avatarOptionPrefab == null || avatarStripContainer == null) return;

        for (int i = 0; i < library.Count; i++)
        {
            var opt = Instantiate(avatarOptionPrefab, avatarStripContainer);
            opt.Setup(i, library.Get(i), OnAvatarPicked);
            options.Add(opt);
        }
        stripBuilt = true;
    }

    private void OnAvatarPicked(int id)
    {
        selectedAvatarId = id;
        ApplyPreview(id);
        HighlightSelected();
        ShowAvatarStrip(false);   // seçim yapıldı → şerit kapanır, istatistikler geri gelir
        PlayerProfile.AvatarId = id;   // anında kaydet → tüm AvatarView'lar yenilenir
    }

    private void ApplyPreview(int id)
    {
        if (currentAvatarImage == null || library == null) return;
        var s = library.Get(id);
        if (s != null) { currentAvatarImage.sprite = s; currentAvatarImage.enabled = true; }
    }

    private void HighlightSelected()
    {
        for (int i = 0; i < options.Count; i++)
            if (options[i] != null) options[i].SetSelected(i == selectedAvatarId);
    }

    private void CommitName(string value)
    {
        if (!PlayerProfile.CustomizationUnlocked) return;
        PlayerProfile.PlayerName = value;   // boş ise PlayerProfile default'a çevirir
        if (nameInput != null) nameInput.SetTextWithoutNotify(PlayerProfile.PlayerName);
    }
}
