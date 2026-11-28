using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Added to the player scene copy only. Source scenes keep their authored art.</summary>
public sealed class EventArtSceneBindings : MonoBehaviour
{
    public EventArtReference[] references = Array.Empty<EventArtReference>();
    internal readonly HashSet<string> appliedEvents = new();
    internal static readonly List<EventArtSceneBindings> Active = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active.Clear();
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDestroy() { Active.Remove(this); }
}
