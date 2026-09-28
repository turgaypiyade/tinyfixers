using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ortak event ses kütüphanesini (Resources/Audio/EventSfxLibrary.asset) kurar ve Audio/SFX/BridgeRepairEvents
/// altındaki klipleri DOSYA ADIYLA bağlar. Süre sınırları: dosyaların başında asıl ses var, kuyruk kesilir
/// (kullanıcı kararı). Asset yoksa editör açılınca bir kez otomatik çalışır; menüden tekrar çalıştırılabilir
/// (Inspector'da elle yapılan ses/süre ayarlarını varsayılana döndürür).
/// </summary>
[InitializeOnLoad]
public static class EventSfxLibrarySetup
{
    private const string AssetPath = "Assets/_Project/Resources/Audio/EventSfxLibrary.asset";
    private const string ClipDir   = "Assets/_Project/Audio/SFX/BridgeRepairEvents/";

    static EventSfxLibrarySetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<EventSfxLibrary>(AssetPath) == null
                && AssetDatabase.IsValidFolder(ClipDir.TrimEnd('/')))
                Setup();
        };
    }

    [MenuItem("TinyFixers/Audio/Event SFX Kütüphanesini Kur")]
    public static void Setup()
    {
        var lib = AssetDatabase.LoadAssetAtPath<EventSfxLibrary>(AssetPath);
        if (lib == null)
        {
            MockupUI.EnsureFolder("Assets/_Project/Resources/Audio");
            lib = ScriptableObject.CreateInstance<EventSfxLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
        }

        // (dosya, hedef, ses, süre sn)
        Bind("popupOpenSound",      c => lib.popupOpen = c,      0.8f, 0.6f);
        Bind("UITabSound",          c => lib.uiTap = c,          0.8f, 0.2f);
        Bind("StepPopSound",        c => lib.stepPop = c,        1f,   0.4f);   // kayıt kısık
        Bind("ArrowWhooshSound",    c => lib.arrowWhoosh = c,    0.8f, 0.4f);
        Bind("PlankPlaceSound",     c => lib.plankPlace = c,     1f,   0.5f);   // kayıt kısık
        Bind("hammerTabSound",      c => lib.hammerTap = c,      0.8f, 0.35f);  // ses 0.15'te başlıyor
        Bind("footStepSound",       c => lib.footstep = c,       1f,   1.4f);   // adım dizisi; yürüyüş süresiyle kesilir
        Bind("RankBadgeSound",      c => lib.rankBadge = c,      0.8f, 0.7f);
        Bind("podiumArriveSound",   c => lib.podiumArrive = c,   0.8f, 0.5f);
        Bind("eliminatedSound",     c => lib.eliminated = c,     0.8f, 1.5f);
        Bind("victoryFanwareSound", c => lib.victoryFanfare = c, 0.9f, 3f);
        Bind("ConfettiSound",       c => lib.confetti = c,       0.8f, 1.5f);

        var ambient = LoadClip("ambientLoopSound", loop: true);
        if (ambient != null) lib.ambientLoop = ambient;

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EventSfx] Kütüphane kuruldu: {AssetPath}");
    }

    private static void Bind(string file, Action<EventSfxCue> assign, float volume, float maxDuration)
    {
        var clip = LoadClip(file, loop: false);
        if (clip == null) { Debug.LogWarning($"[EventSfx] Klip yok: {ClipDir}{file}.wav"); return; }
        assign(new EventSfxCue
        {
            clips = new[] { clip },
            volume = volume,
            pitchJitter = 0.05f,
            maxDuration = maxDuration,
        });
    }

    private static AudioClip LoadClip(string file, bool loop)
    {
        string path = ClipDir + file + ".wav";
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return null;

        // Kısa SFX: belleğe açık + önyüklü → ilk çalışta takılma olmasın (bkz. ses yükleme ayarları).
        var settings = importer.defaultSampleSettings;
        bool dirty = false;
        if (settings.loadType != AudioClipLoadType.DecompressOnLoad) { settings.loadType = AudioClipLoadType.DecompressOnLoad; dirty = true; }
        if (!settings.preloadAudioData) { settings.preloadAudioData = true; dirty = true; }
        if (dirty)
        {
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
