using UnityEngine;

public class PulsePulseExplosionVfx : MonoBehaviour
{
    [SerializeField] private ParticleSystem streaks;

    public void PlayStreaks()
    {
        if (streaks != null)
            streaks.Play();
    }
}
