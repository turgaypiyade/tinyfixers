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
}