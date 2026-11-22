using UnityEngine;

/// <summary>
/// GoldMoney (bonus coin) toplanınca cüzdana coin ekler. GoldMoney, single-hit movable bir
/// obstacle'dır (düşer, plastic_orange/HelmetPorcelain gibi); yanındaki match onu temizleyince
/// "toplanmış" sayılır ve burada PlayerWallet'a coin yazılır. Level hedefi DEĞİL, saf bonus.
///
/// Sahneye bir kez koy (board ile aynı sahnede); board referansını ata ya da otomatik bulur.
/// </summary>
public sealed class GoldCoinCollectService : MonoBehaviour
{
    [SerializeField] private BoardController board;
    [Tooltip("Toplanan her GoldMoney parçası kaç coin ekler.")]
    [SerializeField, Min(1)] private int coinsPerPiece = 1;

    private bool _subscribed;

    private void OnEnable()  => TrySubscribe();
    private void Start()     => TrySubscribe();   // board sahnede geç hazırlanırsa yakala

    // Aynı karede temizlenen altınlar tek kazanca toplanır: 50 altınlık zincir = 1 AddCoins + 1 PlayerPrefs.Save
    // (her parça için ayrı kayıt değil). Kare sonunda yazılır; kapanış/arka plana alınışta bekleyen hemen yazılır.
    private int _pendingCoins;

    private void OnDisable()
    {
        FlushPendingCoins();
        if (board != null) board.ObstacleVisualChanged -= HandleVisualChanged;
        _subscribed = false;
    }

    private void LateUpdate()
    {
        if (_pendingCoins > 0) FlushPendingCoins();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) FlushPendingCoins();
    }

    private void FlushPendingCoins()
    {
        int amount = _pendingCoins;
        if (amount <= 0) return;
        _pendingCoins = 0;
        int before = PlayerWallet.Coins;
        PlayerWallet.AddCoins(amount);
        int after = PlayerWallet.Coins;
        CoinFlyToWalletAnimator.QueuePendingReward(amount, before, after);
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (board == null) board = FindFirstObjectByType<BoardController>();
        if (board == null) return;

        board.ObstacleVisualChanged -= HandleVisualChanged;
        board.ObstacleVisualChanged += HandleVisualChanged;
        _subscribed = true;
    }

    private void HandleVisualChanged(ObstacleVisualChange change)
    {
        // GoldMoney (bonus) ve Altın (hedef, ödül leveli): toplanan her parça cüzdana.
        if (change.cleared && (change.obstacleId == ObstacleId.GoldMoney || change.obstacleId == ObstacleId.GoldCoin))
            _pendingCoins += coinsPerPiece;
    }
}
