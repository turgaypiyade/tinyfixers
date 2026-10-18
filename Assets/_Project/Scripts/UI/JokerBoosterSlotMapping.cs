using UnityEngine;

public class JokerBoosterSlotMapping : MonoBehaviour
{
    [SerializeField] private bool isBoosterSlot = true;
    [SerializeField] private int boosterIndex = -1;

    public bool IsBoosterSlot => isBoosterSlot;
    public int BoosterIndex => boosterIndex;

    /// Çalışma anında kopyalanan slot (5. joker) için.
    public void SetBoosterIndex(int index) { isBoosterSlot = true; boosterIndex = index; }
}
