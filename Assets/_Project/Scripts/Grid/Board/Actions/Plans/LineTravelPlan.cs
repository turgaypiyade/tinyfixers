using System.Collections.Generic;
using UnityEngine;

public class LineTravelPlan
{
    public Vector2Int OriginCell;
    public List<TileView> OriginViewsToHide = new();
    public List<(Vector2Int cell, Vector2 anchored)> HorizontalOrigins = new();
    public List<(Vector2Int cell, Vector2 anchored)> VerticalOrigins = new();
    public List<LineHitStep> HitSteps = new();
    public Dictionary<Vector2Int, TileView> HitVisuals = new();

    public bool IsBlocking = true;
    public string SourceTag = "OverrideLine";
}