using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avatar ikonuna tıklanınca açılan profil sayfası.
/// View: mevcut avatar + isim. "Edit" → avatar grid + isim girişi (10. level sonrası açık).
/// Kaydedince PlayerProfile güncellenir (AvatarView'lar otomatik yenilenir).
///
/// Açmak için: avatar dairesine bir Button koy, onClick → ProfilePageController.Open().
/// </summary>
public sealed class ProfilePageController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private GameObject profileRoot;     // tüm profil paneli
    [SerializeField] private AvatarLibrary library;

    [Header("View")]
    [SerializeField] private Image currentAvatarImage;
    [SerializeField] private TMP_Text currentNameText;
    [SerializeField] private Button editButton;
    [Tooltip("Kilitliyken gösterilecek ipucu (örn. '10. levelde açılır'). Opsiyonel.")]
    [SerializeField] private GameObject editLockedHint;
    [SerializeField] private Button closeButton;

    [Header("Edit")]
    [SerializeField] private GameObject editRoot;
    [SerializeField] private Transform avatarGridContainer;
    [SerializeField] private AvatarOptionButton avatarOptionPrefab;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField, Min(1)] private int nameMaxLength = 12;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;

    private int selectedAvatarId;
    private readonly List<AvatarOptionButton> options = new();

    private void Awake()
    {
        if (editButton   != null) editButton.onClick.AddListener(EnterEdit);
        if (closeButton  != null) closeButton.onClick.AddListener(Close);
        if (saveButton   != null) saveButton.onClick.AddListener(SaveEdit);
        if (cancelButton != null) cancelButton.onClick.AddListener(ExitEdit);
        if (nameInput    != null) nameInput.characterLimit = nameMaxLength;

        if (profileRoot != null) profileRoot.SetActive(false);
    }

    // Avatar dairesinin Button'ı bunu çağırır.
    public void Open()
    {
        if (profileRoot != null) profileRoot.SetActive(true);
        ShowView();
    }

    public void Close()
    {
        if (profileRoot != null) profileRoot.SetActive(false);
    }

    private void ShowView()
    {
        if (editRoot != null) editRoot.SetActive(false);

        if (currentAvatarImage != null && library != null)
        {
            var s = library.Get(PlayerProfile.AvatarId);
            if (s != null) { currentAvatarImage.sprite = s; currentAvatarImage.enabled = true; }
        }
        if (currentNameText != null) currentNameText.text = PlayerProfile.PlayerName;

        bool unlocked = PlayerProfile.CustomizationUnlocked;
        if (editButton != null) editButton.interactable = unlocked;
        if (editLockedHint != null) editLockedHint.SetActive(!unlocked);
    }

    private void EnterEdit()
    {
        if (!PlayerProfile.CustomizationUnlocked)
            return;

        if (editRoot != null) editRoot.SetActive(true);

        selectedAvatarId = PlayerProfile.AvatarId;
        if (nameInput != null) nameInput.text = PlayerProfile.PlayerName;

        BuildGrid();
    }

    private void ExitEdit()
    {
        if (editRoot != null) editRoot.SetActive(false);
        ShowView();
    }

    private void SaveEdit()
    {
        string name = nameInput != null ? nameInput.text : PlayerProfile.PlayerName;
        PlayerProfile.SetProfile(selectedAvatarId, name);   // AvatarView'lar OnChanged ile yenilenir
        ExitEdit();
    }

    private void BuildGrid()
    {
        for (int i = 0; i < options.Count; i++)
            if (options[i] != null) Destroy(options[i].gameObject);
        options.Clear();

        if (library == null || avatarOptionPrefab == null || avatarGridContainer == null)
            return;

        for (int i = 0; i < library.Count; i++)
        {
            var opt = Instantiate(avatarOptionPrefab, avatarGridContainer);
            opt.Setup(i, library.Get(i), OnAvatarPicked);
            opt.SetSelected(i == selectedAvatarId);
            options.Add(opt);
        }
    }

    private void OnAvatarPicked(int id)
    {
        selectedAvatarId = id;
        for (int i = 0; i < options.Count; i++)
            if (options[i] != null) options[i].SetSelected(i == id);
    }
}
