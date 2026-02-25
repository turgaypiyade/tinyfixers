using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelEndSimplePopupController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;

    [Header("Popup Roots")]
    [SerializeField] private GameObject failPopupRoot;
    [SerializeField] private GameObject successPopupRoot;
    [SerializeField] private GameObject blockerRoot;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text failDescriptionText;
    [SerializeField] private TMP_Text successDescriptionText;

    [Header("Buttons")]
    [SerializeField] private Button buyMovesButton;
    [SerializeField] private Button failCloseButton;
    [SerializeField] private Button successCloseButton;
    [SerializeField] private Button successContinueButton; // BtnsContinue

    [Header("Fail Offer")]
    [SerializeField] private int extraMovesAmount = 5;
    [SerializeField] private int extraMovesCost = 900;

    [Header("Success Bonus (placeholder)")]
    [SerializeField] private bool autoStartBonus = true;

    [Header("Progression")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string prefsLevelKey = "current_level";

    private bool failPopupShown;
    private bool successPopupShown;

    // We may receive moves/goal events while the board is still resolving cascades.
    // This gate ensures we only evaluate & show end popups after the board becomes idle.
    private bool endCheckQueued;

    private void OnEnable()
    {
        failPopupShown = false;
        successPopupShown = false;
        HideAllPopups();

        StartCoroutine(InitializeWhenReady());

        if (buyMovesButton != null)
            buyMovesButton.onClick.AddListener(HandleBuyMovesClicked);
        if (failCloseButton != null)
            failCloseButton.onClick.AddListener(HideAllPopups);
        if (successCloseButton != null)
            successCloseButton.onClick.AddListener(HandleSuccessCloseClicked);

        if (successContinueButton != null)
            successContinueButton.onClick.AddListener(HandleSuccessCloseClicked);

    }

    private void OnDisable()
    {
        Unsubscribe();

        if (buyMovesButton != null)
            buyMovesButton.onClick.RemoveListener(HandleBuyMovesClicked);
        if (failCloseButton != null)
            failCloseButton.onClick.RemoveListener(HideAllPopups);
        if (successCloseButton != null)
            successCloseButton.onClick.RemoveListener(HandleSuccessCloseClicked);
        if (successContinueButton != null)
            successContinueButton.onClick.RemoveListener(HandleSuccessCloseClicked);
    }

    private IEnumerator InitializeWhenReady()
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>();

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        while (board == null || topHud == null || board.ActiveLevelData == null)
            yield return null;

        Subscribe();
        RefreshPopupCopy();
        ResolveBlockerReference();
        SetBlockerVisible(false);
        RequestEvaluateLevelEndState();
    }
    private void HandleSuccessCloseClicked()
    {
        HideAllPopups();

        // Level++
        int level = PlayerPrefs.GetInt(prefsLevelKey, 1);
        PlayerPrefs.SetInt(prefsLevelKey, level + 1);
        PlayerPrefs.Save();

        // Ana Menü'ye dön
        SceneManager.LoadScene(mainMenuSceneName);
    }
    private void ResolveBlockerReference()
    {
        if (blockerRoot != null)
            return;

        Transform blockerTransform = transform.Find("Blocker");
        if (blockerTransform == null)
            blockerTransform = transform.Find("blocker");

        if (blockerTransform != null)
            blockerRoot = blockerTransform.gameObject;
    }

    private void SetBlockerVisible(bool isVisible)
    {
        if (blockerRoot != null)
            blockerRoot.SetActive(isVisible);
    }

    private void Subscribe()
    {
        Unsubscribe();

        board.OnMovesChanged += HandleMovesChanged;
        topHud.OnGoalsCompletionChanged += HandleGoalsCompletionChanged;
    }

    private void Unsubscribe()
    {
        if (board != null)
            board.OnMovesChanged -= HandleMovesChanged;

        if (topHud != null)
            topHud.OnGoalsCompletionChanged -= HandleGoalsCompletionChanged;
    }

    private void HandleMovesChanged(int _)
    {
        RequestEvaluateLevelEndState();
    }

    private void HandleGoalsCompletionChanged(bool _)
    {
        RequestEvaluateLevelEndState();
    }

    private void RequestEvaluateLevelEndState()
    {
        if (board == null || topHud == null)
            return;

        if (endCheckQueued)
            return;

        endCheckQueued = true;

        // If board is still resolving cascades (fall/spawn/matches/specials), wait.
        // When it becomes idle, we re-check the conditions and only then show the popup.
        board.RunAfterIdle(() =>
        {
            endCheckQueued = false;
            EvaluateAndShowIfEnded();
        });
    }

    private void EvaluateAndShowIfEnded()
    {
        if (board == null || topHud == null)
            return;

                // If a popup is already shown, don't try to show another.
        if (failPopupShown || successPopupShown)
            return;

        if (topHud.AreAllGoalsCompleted)
        {
            ShowSuccessPopup();
            return;
        }

        if (board.RemainingMoves <= 0)
            ShowFailPopup();

    }

    private void ShowFailPopup()
    {
        if (failPopupShown)
            return;

        failPopupShown = true;
        successPopupShown = false;

        if (failPopupRoot != null)
            failPopupRoot.SetActive(true);
        if (successPopupRoot != null)
            successPopupRoot.SetActive(false);

        SetBlockerVisible(true);
    }

    private void ShowSuccessPopup()
    {
        if (successPopupShown)
            return;

        successPopupShown = true;
        failPopupShown = false;

        if (successPopupRoot != null)
            successPopupRoot.SetActive(true);
        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        SetBlockerVisible(true);

        if (autoStartBonus)
            Debug.Log("[LevelEndSimplePopupController] Success popup shown. Hook bonus flow here.");
    }

    private void HandleBuyMovesClicked()
    {
        if (board == null)
            return;

        // Ekonomi entegrasyonu henüz yok. Şimdilik direkt +hamle veriyoruz.
        board.AddMoves(extraMovesAmount);

        failPopupShown = false;
        HideAllPopups();
    }

    private void RefreshPopupCopy()
    {
        if (failDescriptionText != null)
            failDescriptionText.text = $"Hedefe ulaşamadın. {extraMovesCost} coin ile +{extraMovesAmount} hamle alıp devam edebilirsin.";

        if (successDescriptionText != null)
            successDescriptionText.text = "Hedefler tamamlandı! Kalan hamleler bonusa dönüşecek.";
    }

    public void HideAllPopups()
    {
        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        if (successPopupRoot != null)
            successPopupRoot.SetActive(false);

        SetBlockerVisible(false);
    }
}
