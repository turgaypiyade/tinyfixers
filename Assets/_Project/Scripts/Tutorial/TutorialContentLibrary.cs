using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialContentLibrary", menuName = "CoreCollapse/Tutorial Content Library")]
public class TutorialContentLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public TutorialId id;
        [Tooltip("tinyfixers_localization.json key. Metin buradan çekilir.")]
        public string locKey;
        public Sprite illustrationSprite;
    }

    [SerializeField] private Entry[] entries;

    public bool TryGet(TutorialId id, out Entry entry)
    {
        if (entries != null)
            foreach (var e in entries)
                if (e != null && e.id == id) { entry = e; return true; }

        entry = null;
        return false;
    }
}
