using UnityEngine;

/// <summary>
/// Taş yerleştirme / işaretleme "nota dizisi": UFO'nun (ve prelevel booster'ın) special yerleştirmesi ile
/// Override'ın taşları işaretlemesi her yeni taşta Do majör pentatonikte BİR SONRAKİ notayı çalar → yükselen
/// arpej (pentatonik: hangi notalar üst üste binerse binsin uyumlu). 10 notayı aşan dizi aşağı-yukarı
/// dalgalanır (9→0 sert düşüş yok). Notalar Resources/Audio/PlacementNotes/PlacementNote_00..09 (sentez çelesta).
/// Klipler bir kez yüklenir; çalarken bellek ayırmaz. Çok yoğun dizide (Override, onlarca hedef) aynı karede
/// üst üste binmeyi MinInterval sınırlar.
/// </summary>
public static class PlacementChime
{
    private const string ResourcePath = "Audio/PlacementNotes";
    private const float MinInterval = 0.025f;

    private static AudioClip[] notes;
    private static float lastPlayTime = -1f;

    public static void Play(AudioSource source, int index, float volume = 0.7f)
    {
        if (source == null || !GameSettings.SoundEnabled) return;
        EnsureLoaded();
        int n = notes.Length;
        if (n == 0) return;

        float now = Time.unscaledTime;
        if (now - lastPlayTime < MinInterval) return;
        lastPlayTime = now;

        int note = n == 1 ? 0 : PingPong(Mathf.Max(0, index), n);
        var clip = notes[note];
        if (clip != null) source.PlayOneShot(clip, volume);
    }

    // 0,1..n-1,n-2..1,0,1.. (periyot 2n-2)
    private static int PingPong(int index, int n)
    {
        int period = 2 * n - 2;
        int k = index % period;
        return k < n ? k : period - k;
    }

    private static void EnsureLoaded()
    {
        if (notes != null) return;
        notes = Resources.LoadAll<AudioClip>(ResourcePath);
        System.Array.Sort(notes, (a, b) => string.CompareOrdinal(a.name, b.name));
    }
}
