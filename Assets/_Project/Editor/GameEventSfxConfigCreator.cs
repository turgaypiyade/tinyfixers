using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// GameEventSfxConfig asset'ini Resources/Audio altında tek tıkla oluşturur/günceller.
/// ChestOpen.wav + GameAudioMixer'ın SFX group'unu otomatik atar.
/// Menü: TinyFixers > Audio > Create Game Event Sfx Config.
/// </summary>
public static class GameEventSfxConfigCreator
{
    const string Dir = "Assets/_Project/Resources/Audio";
    const string AssetPath = Dir + "/GameEventSfxConfig.asset";
    const string ChestClipPath = "Assets/_Project/Audio/SFX/ChestOpen.wav";
    const string WeldClipPath = "Assets/_Project/Audio/SFX/WeldingSound.wav";
    const string MixerPath = "Assets/_Project/Audio/GameAudioMixer.mixer";

    [MenuItem("TinyFixers/Audio/Create Game Event Sfx Config")]
    public static void Create()
    {
        Directory.CreateDirectory(Dir);

        var cfg = AssetDatabase.LoadAssetAtPath<GameEventSfxConfig>(AssetPath);
        bool isNew = cfg == null;
        if (isNew) cfg = ScriptableObject.CreateInstance<GameEventSfxConfig>();

        if (cfg.chestOpen == null)
            cfg.chestOpen = AssetDatabase.LoadAssetAtPath<AudioClip>(ChestClipPath);
        if (cfg.weldingLoop == null)
            cfg.weldingLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(WeldClipPath);

        if (cfg.sfxGroup == null)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null)
            {
                var groups = mixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0) cfg.sfxGroup = groups[0];
            }
        }

        if (isNew) AssetDatabase.CreateAsset(cfg, AssetPath);
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);

        EditorUtility.DisplayDialog("Game Event Sfx",
            $"{(isNew ? "Oluşturuldu" : "Güncellendi")}: {AssetPath}\n\n" +
            $"• chestOpen: {(cfg.chestOpen != null ? cfg.chestOpen.name : "YOK — elle ata")}\n" +
            $"• sfxGroup: {(cfg.sfxGroup != null ? cfg.sfxGroup.name : "YOK — GameAudioMixer'dan SFX group ata")}\n\n" +
            "Sandık açılışı + oyun-sonu bu sesi çalar; global ses kontrolüne bağlı.", "Tamam");
    }
}
