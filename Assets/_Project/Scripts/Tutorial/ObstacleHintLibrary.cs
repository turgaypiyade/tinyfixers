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
        [Tooltip("Lokalizasyon key. Boşsa fallbackText kullanılır.")]
        public string locKey;
        [Tooltip("Lokalizasyon yoksa kullanılacak açıklama metni.")]
        [TextArea(2, 4)] public string fallbackText;
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
