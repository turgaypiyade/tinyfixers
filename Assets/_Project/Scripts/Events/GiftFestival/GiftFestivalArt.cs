using System;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "GiftFestivalArt", menuName = "TinyFixers/Events/Gift Festival Art")]
public sealed class GiftFestivalArt : ScriptableObject
{
    private static GiftFestivalArt shared;
    public static GiftFestivalArt Shared => shared != null ? shared : (shared = Resources.Load<GiftFestivalArt>("Events/GiftFestivalArt"));
    public Sprite menuLogo, background, topSign, board, glass, giftSymbol;
    [Tooltip("Optional continuous tall panorama. The lower section should match the gameplay field.")]
    public Sprite introPanorama;
    public Sprite[] metalCorners = Array.Empty<Sprite>();
    public Sprite[] targetCorners = Array.Empty<Sprite>();
    public Sprite ball;
    public Sprite[] ballSpin = Array.Empty<Sprite>();
    public Sprite[] bearFrames = Array.Empty<Sprite>();
    public Sprite[] rabbitFrames = Array.Empty<Sprite>();
    public Sprite hitNormal, hitFrame, hitPressed, closeIcon;
    public TMP_FontAsset font;
    public Material fontMaterial;
    public Material disabledButtonMaterial;
}
