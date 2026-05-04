using UnityEngine;

[CreateAssetMenu(fileName = "OilSpreadVisualConfig", menuName = "TinyFixers/Obstacles/OilSpreadVisualConfig")]
public sealed class OilSpreadVisualConfig : ScriptableObject
{
    [Tooltip("Oil spread per-cell FX prefab (optional).")]
    public GameObject spreadFxPrefab;
    [Tooltip("Duration of the overlay fade-in per cell, in seconds.")]
    public float spreadDuration = 0.22f;
    [Tooltip("Stagger delay between cells, in seconds.")]
    public float staggerDelay = 0.04f;
    [Tooltip("Overlay tint color for Oil cells.")]
    public Color overlayColor = new Color(0f, 0f, 0f, 0.45f);
}
