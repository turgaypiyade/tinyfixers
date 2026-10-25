using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TilesTopClipShape ile stencil maske. Mask'ın alan testi (raycast filtresi) kapatılır: TilesRoot boyutsuz olduğundan
/// varsayılan test taşlara gelen tüm dokunuşları reddederdi. Yalnız görsel kırpar.
/// </summary>
[DisallowMultipleComponent]
public sealed class TilesTopClipMask : Mask
{
    public override bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera) => true;
}
