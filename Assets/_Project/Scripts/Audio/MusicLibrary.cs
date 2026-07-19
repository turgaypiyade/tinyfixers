using System;
using UnityEngine;

/// <summary>
/// Müzik kataloğu: id → parça (isim + clip + ses). MusicState.SelectedTrack buna index'ler.
/// Avatar sistemi (AvatarLibrary) ile aynı desen: tek asset, her yerde paylaşılır.
/// Parça 0 varsayılan/ücretsiz; diğerleri profil sayfasından 100 altınla açılır.
/// </summary>
[CreateAssetMenu(fileName = "MusicLibrary", menuName = "TinyFixers/Music Library")]
public sealed class MusicLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Track
    {
        public string displayName = "Parça";
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private Track[] tracks;

    public int Count => tracks != null ? tracks.Length : 0;

    public Track Get(int id)
    {
        if (tracks == null || tracks.Length == 0) return null;
        return tracks[Mathf.Clamp(id, 0, tracks.Length - 1)];
    }
}
