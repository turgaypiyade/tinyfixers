using UnityEngine;

[System.Serializable]
public struct LineHitStep
{
    public Vector2Int Cell;
    public float Time;
    public string Lane; // "H" veya "V"

    public LineHitStep(Vector2Int cell, float time, string lane)
    {
        Cell = cell;
        Time = time;
        Lane = lane;
    }
}