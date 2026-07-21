using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Arkadaş Bul" popup'ı (referans RM): ID arama kutusu + kendi ID'n (kopyala) + Davet Et.
/// Arama sonucu bulunan oyuncu satır olarak gösterilir ve Ekle ile arkadaş listesine girer.
/// v1: arama FriendDirectory'nin deterministik mock dizinine gider; backend gelince
/// aynı akış Firestore sorgusuna bağlanır.
/// </summary>
public sealed class FindFriendPopup : MonoBehaviour
{
    [Header("Genel")]
    [SerializeField] private Button closeButton;

    [Header("Arama")]
    [SerializeField] private TMP_InputField searchInput;     // "Arkadaşının ID'si"
    [SerializeField] private Button searchButton;

    [Header("Sonuç")]
    [SerializeField] private GameObject resultRoot;          // başta kapalı
    [SerializeField] private Image resultAvatar;
    [SerializeField] private TMP_Text resultNameText;
    [SerializeField] private TMP_Text resultSubText;         // "Bölüm N"
    [SerializeField] private Button resultAddButton;
    [SerializeField] private TMP_Text resultAddLabel;        // "Ekle" → "Eklendi"
    [SerializeField] private TMP_Text notFoundText;          // "Oyuncu bulunamadı"

    [Header("Kendi ID'n")]
    [SerializeField] private TMP_Text myIdText;              // "ID'm: YX7115676"
    [SerializeField] private Button copyButton;

    [Header("Davet")]
    [SerializeField] private Button inviteButton;
    [SerializeField] private TMP_Text inviteLabel;           // "Davet Et" → "Kopyalandı!"

    [Header("Görsel")]
    [SerializeField] private Sprite[] avatarPool;

    private FriendProfile found;
    private bool wired;

    public void Open()
    {
        gameObject.SetActive(true);
        Wire();

        if (searchInput != null) searchInput.text = "";
        if (myIdText != null) myIdText.text = $"ID'm: {FriendState.MyCode}";
        if (inviteLabel != null) inviteLabel.text = "Davet Et";
        ShowResult(null);
    }

    public void Close() => gameObject.SetActive(false);

    private void Wire()
    {
        if (wired) return;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (searchButton != null) searchButton.onClick.AddListener(OnSearch);
        if (searchInput != null) searchInput.onSubmit.AddListener(_ => OnSearch());
        if (copyButton != null) copyButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = FriendState.MyCode);
        if (inviteButton != null) inviteButton.onClick.AddListener(OnInvite);
        if (resultAddButton != null) resultAddButton.onClick.AddListener(OnAddFound);
        wired = true;
    }

    private void OnSearch()
    {
        string code = searchInput != null ? searchInput.text : "";

        // Arama artık GERÇEK (players dizini, async) — sorgu dönene dek butonu kilitle.
        if (searchButton != null) searchButton.interactable = false;
        if (resultRoot != null) resultRoot.SetActive(false);
        if (notFoundText != null) notFoundText.gameObject.SetActive(false);

        FriendDirectory.SearchByCode(code, profile =>
        {
            if (this == null) return;   // popup arama dönmeden yok edildiyse
            if (searchButton != null) searchButton.interactable = true;
            ShowResult(profile);
        });
    }

    private void ShowResult(FriendProfile profile)
    {
        found = profile;
        bool has = profile != null;

        if (resultRoot != null) resultRoot.SetActive(has);
        if (notFoundText != null)
            notFoundText.gameObject.SetActive(!has && searchInput != null && !string.IsNullOrWhiteSpace(searchInput.text));

        if (!has) return;

        if (resultNameText != null) resultNameText.text = profile.name;
        if (resultSubText != null) resultSubText.text = $"Bölüm {profile.chapter}";
        if (resultAvatar != null)
        {
            var sprite = PickAvatar(profile.name);
            resultAvatar.sprite = sprite;
            resultAvatar.enabled = sprite != null;
            resultAvatar.preserveAspect = true;
        }

        bool already = !string.IsNullOrEmpty(profile.uid)
            ? FriendState.IsRealFriend(profile.uid)
            : FriendState.IsFriend(profile.name);
        if (resultAddButton != null) resultAddButton.interactable = !already;
        if (resultAddLabel != null) resultAddLabel.text = already ? "Eklendi" : "Ekle";
    }

    private void OnAddFound()
    {
        if (found == null) return;

        // ID aramasından gelen GERÇEK oyuncu uid'iyle, bot önerisi isimle eklenir.
        if (!string.IsNullOrEmpty(found.uid))
            FriendState.AddRealFriend(found.uid, found.name, found.chapter);
        else
            FriendState.AddFriend(found.name);

        if (resultAddButton != null) resultAddButton.interactable = false;
        if (resultAddLabel != null) resultAddLabel.text = "Eklendi";
    }

    // Native paylaşım (mobil share sheet) ayrı plugin ister; v1'de davet metni panoya
    // kopyalanır ve buton geri bildirim verir.
    private void OnInvite()
    {
        GUIUtility.systemCopyBuffer = FriendDirectory.InviteMessage();
        if (inviteLabel != null) inviteLabel.text = "Kopyalandı!";
    }

    private Sprite PickAvatar(string name)
    {
        Sprite profileAvatar = PlayerAvatarProvider.PickForSeed(name);
        if (profileAvatar != null)
            return profileAvatar;

        if (avatarPool == null || avatarPool.Length == 0) return null;
        int hash = StableHash(name);
        return avatarPool[hash % avatarPool.Length];
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
            }
            return hash & int.MaxValue;
        }
    }
}
