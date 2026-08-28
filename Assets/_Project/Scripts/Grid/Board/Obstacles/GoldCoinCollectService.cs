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

    private void OnDisable()
    {
        if (board != null) board.ObstacleVisualChanged -= HandleVisualChanged;
        _subscribed = false;
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
        if (change.cleared && change.obstacleId == ObstacleId.GoldMoney)
        {
            int before = PlayerWallet.Coins;
            PlayerWallet.AddCoins(coinsPerPiece);
            int after = PlayerWallet.Coins;
            CoinFlyToWalletAnimator.QueuePendingReward(coinsPerPiece, before, after);
        }
    }
}
