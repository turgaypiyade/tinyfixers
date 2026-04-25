using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BorderSpriteLibrary", menuName = "TinyFixers/Grid/Border Sprite Library")]
public class BorderSpriteLibrary : ScriptableObject
{
    [SerializeField] private List<BorderSpriteSet> sets = new List<BorderSpriteSet>();

    public IReadOnlyList<BorderSpriteSet> Sets => sets;

    public BorderSpriteSet Get(BorderColorId colorId)
    {
        if (sets == null)
            return null;

        for (int i = 0; i < sets.Count; i++)
        {
            BorderSpriteSet set = sets[i];
            if (set != null && set.colorId == colorId)
                return set;
        }

        return null;
    }

    public bool TryGet(BorderColorId colorId, out BorderSpriteSet set)
    {
        set = Get(colorId);
        return set != null;
    }
}
