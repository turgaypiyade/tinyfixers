using UnityEngine;

[CreateAssetMenu(menuName = "TinyFixers/Audio/Board Sfx Profile")]
public class BoardSfxProfile : ScriptableObject
{
    [System.Serializable]
    public class VariantSet
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        public float minInterval = 0.05f;
    }

    [Header("Fall")]
    public VariantSet fallLight;
    public VariantSet fallMedium;
    public VariantSet fallHeavy;

    [Header("Special Create")]
    public VariantSet createLine;
    public VariantSet createPatchBot;
    public VariantSet createPulseCore;
    public VariantSet createSystemOverride;

    [Header("Special Activate")]
    public VariantSet activateLine;
    public VariantSet activatePatchBot;
    public VariantSet activatePulseCore;
    public VariantSet activateSystemOverride;

    [Header("Combo")]
    public VariantSet comboStart;
    public VariantSet comboImpact;

    [Header("Override Combo")]
    public VariantSet overrideNormalStart;
    public VariantSet overrideSpecialStart;

    [Header("Pulse Combo")]
    public VariantSet pulsePulseCharge;

    [Header("Combo - Line")]
    public VariantSet comboLineLine;
    public VariantSet comboPulseLine;
    public VariantSet comboPatchBotLine;

    [Header("Combo - PulseCore")]
    public VariantSet comboPulsePatchBot;

    [Header("Combo - PatchBot")]
    public VariantSet comboPatchBotPatchBot;

    [Header("Combo - Override")]
    public VariantSet comboOverrideOverride;
}