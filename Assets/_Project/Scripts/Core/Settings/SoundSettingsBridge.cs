using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-50)]
public class SoundSettingsBridge : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    private const string Param = "SFXVolume";

    private void OnEnable()
    {
        GameSettings.OnSoundChanged += ApplySound;
        ApplySound(GameSettings.SoundEnabled);
    }

    private void OnDisable()
    {
        GameSettings.OnSoundChanged -= ApplySound;
    }

    private void ApplySound(bool enabled)
    {
        if (mixer != null) mixer.SetFloat(Param, enabled ? 0f : -80f);
    }
}
