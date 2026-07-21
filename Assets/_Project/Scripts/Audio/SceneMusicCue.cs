using UnityEngine;

public class SceneMusicCue : MonoBehaviour
{
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool restartIfSame = false;

    private void Start()
    {
        if (MusicManager.Instance == null)
            return;

        if (MusicState.TryGetSelectedTrack(out var selectedClip, out var selectedVolume))
        {
            MusicManager.Instance.Play(selectedClip, selectedVolume, restartIfSame);
            return;
        }

        MusicManager.Instance.Play(music, volume, restartIfSame);
    }
}
