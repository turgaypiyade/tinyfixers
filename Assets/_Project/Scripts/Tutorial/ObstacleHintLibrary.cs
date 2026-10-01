using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleHintLibrary",
    menuName = "CoreCollapse/Tutorial/Obstacle Hint Library")]
public class ObstacleHintLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ObstacleId id;
        [Tooltip("Boş bırakılırsa ObstacleLibrary'den otomatik çekilir.")]
        public Sprite iconOverride;
        [Tooltip("Obstacle adının lokalizasyon key'i. TR/EN isimler lokalizasyon dosyasından düzenlenir.")]
        public string titleLocKey;
        [Tooltip("İsim lokalizasyonu yoksa kullanılacak ad.")]
        public string fallbackTitle;
        [Tooltip("Lokalizasyon key. Boşsa fallbackText kullanılır.")]
        public string locKey;
        [Tooltip("Lokalizasyon yoksa kullanılacak açıklama metni.")]
        [TextArea(2, 4)] public string fallbackText;

        public string GetTitle() => LocalizeOrFallback(titleLocKey,
            string.IsNullOrEmpty(fallbackTitle) ? id.ToString() : fallbackTitle);

        public string GetDescription() => LocalizeOrFallback(locKey, fallbackText);

        private static string LocalizeOrFallback(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            string localized = GameLocalization.Get(key);
            return string.IsNullOrEmpty(localized) || localized == key ? fallback : localized;
        }
    }

    [SerializeField] private Entry[] entries;

    public bool TryGet(ObstacleId id, out Entry entry)
    {
        if (entries != null)
            foreach (var e in entries)
                if (e != null && e.id == id) { entry = e; return true; }

        entry = null;
        return false;
    }
}
