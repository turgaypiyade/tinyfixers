using UnityEngine;

public class MainMenuMusicStarter : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private void Start()
    {
        EnsureMusicManager();

        if (MusicManager.Instance == null)
        {
            Debug.LogWarning("[MainMenuMusicStarter] MusicManager bulunamadı.");
            return;
        }

        if (menuMusic == null)
        {
            Debug.LogWarning("[MainMenuMusicStarter] Menu music atanmadı.");
            return;
        }

        MusicManager.Instance.Play(menuMusic, volume);
    }

    private void EnsureMusicManager()
    {
        if (MusicManager.Instance != null)
            return;

        GameObject go = new GameObject("MusicManager");
        go.AddComponent<AudioSource>();
        go.AddComponent<MusicManager>();
    }
}