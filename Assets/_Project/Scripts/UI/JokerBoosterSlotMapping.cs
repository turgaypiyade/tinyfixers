using UnityEngine;

public class JokerBoosterSlotMapping : MonoBehaviour
{
    [SerializeField] private bool isBoosterSlot = true;
    [SerializeField] private int boosterIndex = -1;

    public bool IsBoosterSlot => isBoosterSlot;
    public int BoosterIndex => boosterIndex;
}
