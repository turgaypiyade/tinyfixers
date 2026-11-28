using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class ProgressiveContent
{
    private sealed class EventRequest
    {
        public MonoBehaviour owner;
        public string eventId;
        public bool cancelled, wasActive;
    }
    private readonly List<EventRequest> eventRequests = new();
    // A hidden, cached screen is still a consumer of its sprites. Keep one scene lease until its
    // binding host is destroyed, so reopening does not allocate duplicate textures every time.
    private readonly Dictionary<string, HashSet<GameObject>> eventConsumers = new();
    private readonly HashSet<string> dismissedEventPrompts = new();
    private readonly List<string> eventReleaseBuffer = new();
    private EventArtBindings eventBindings;
    private readonly HashSet<string> boundEvents = new();
    private readonly HashSet<string> retainedEventBundles = new();
    private readonly HashSet<string> retainedEventAssets = new();

    public static T EventSourceConfig<T>(string resourcePath) where T : Object
    {
#if UNITY_EDITOR
        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/RemoteContent/EventSources/"
            + System.IO.Path.GetFileName(resourcePath) + ".asset");
        if (source != null) return source;
#endif
        return Resources.Load<T>(resourcePath);
    }

    public static T EventResource<T>(string id, string path) where T : Object
    {
#if UNITY_EDITOR
        if (path == "Events/BridgeRepairRiver")
        {
            var moved = UnityEditor.AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/RemoteContent/EventSources/BridgeRepairRiver.mat");
            if (moved != null) return moved;
        }
#endif
        var self = Get();
        var pack = self?.manifest.FindEvent(id);
        if (pack == null) return Resources.Load<T>(path);
        var asset = Array.Find(pack.assets, a => a.resourcePath == path);
        return asset != null ? Resolve<T>(asset.guid) : null;
    }

    public static bool EventPromptDismissed(string id) => instance != null && instance.dismissedEventPrompts.Contains(id);
    public static bool EventReady(string id)
    {
        var self = Get();
        var pack = self?.manifest.FindEvent(id);
        return pack == null || self.readyPacks.Contains(pack.Key);
    }

    /// <summary>Returns true when the caller must defer. Callback re-enters the original method,
    /// so join/state/schedule checks run again before any mutation. Inactive authored screens are supported.</summary>
    public static bool GateEvent(MonoBehaviour owner, string id, Action onReady, Func<bool> stillAllowed = null)
    {
        var self = Get();
        var pack = self?.manifest.FindEvent(id);
        if (pack == null) return false; // Local/editor and previously published schema-2 content.
        if (owner == null || (stillAllowed != null && !stillAllowed())) return true;
        if (self.readyPacks.Contains(pack.Key))
        {
            self.ApplyEvent(id);
            self.RetainEvent(id, owner.gameObject);
            return false;
        }
        foreach (var pending in self.eventRequests)
            if (pending.owner == owner && pending.eventId == id) return true;
        var request = new EventRequest { owner = owner, eventId = id, wasActive = owner.isActiveAndEnabled };
        self.eventRequests.Add(request);
        self.StartCoroutine(self.EventGateRoutine(request, pack, onReady, stillAllowed));
        return true;
    }

    public static void CancelEvent(MonoBehaviour owner)
    {
        if (instance == null) return;
        foreach (var pending in instance.eventRequests)
            if (pending.owner == owner) pending.cancelled = true;
        // Release the view lease. An authored scene binding host remains a cache consumer until
        // scene teardown, because its inactive UI objects still own their sprite references.
        if (owner != null)
            foreach (var consumers in instance.eventConsumers.Values) consumers.Remove(owner.gameObject);
    }

    private IEnumerator EventGateRoutine(EventRequest pending, ProgressiveContentPack pack, Action ready, Func<bool> allowed)
    {
        string window = EventContentPolicy.WindowKey(pack.eventId);
        float nextPolicyCheck = 0;
        bool policyChanged = false;
        bool Cancelled()
        {
            if (pending.cancelled || pending.owner == null || (pending.wasActive && !pending.owner.isActiveAndEnabled)) return true;
            if (Time.realtimeSinceStartup >= nextPolicyCheck)
            {
                nextPolicyCheck = Time.realtimeSinceStartup + 1f;
                policyChanged = window != EventContentPolicy.WindowKey(pack.eventId) || (allowed != null && !allowed());
            }
            return policyChanged;
        }
        try
        {
            yield return RequirePack(pack, cancelled: Cancelled, cancel: () => {
                pending.cancelled = true;
                dismissedEventPrompts.Add(pack.eventId);
            });
            nextPolicyCheck = 0; // Always recheck the live policy after the final async load.
            if (!Cancelled() && readyPacks.Contains(pack.Key))
            {
                ApplyEvent(pack.eventId);
                RetainEvent(pack.eventId, pending.owner.gameObject);
                ready?.Invoke();
            }
        }
        finally { eventRequests.Remove(pending); }
    }

    private void RetainEvent(string id, GameObject owner)
    {
        if (!eventConsumers.TryGetValue(id, out var consumers)) eventConsumers[id] = consumers = new HashSet<GameObject>();
        consumers.Add(owner);
        foreach (var host in EventArtSceneBindings.Active)
            if (host != null && Array.Exists(host.references, r => r.eventId == id)) consumers.Add(host.gameObject);
    }

    private void ApplyEvent(string id)
    {
        if (eventBindings == null) eventBindings = Resources.Load<EventArtBindings>("EventArtBindings");
        if (eventBindings != null && boundEvents.Add(id)) Apply(eventBindings.references, id);
        foreach (var host in EventArtSceneBindings.Active)
            if (host != null && host.appliedEvents.Add(id)) Apply(host.references, id);
    }
    private void Apply(EventArtReference[] references, string id, bool clear = false)
    {
        foreach (var reference in references)
            if (reference.eventId == id)
            {
                loadedAssets.TryGetValue(reference.assetKey, out var value);
                reference.Set(clear ? null : value);
            }
    }

    private bool EventRequestCancelled(ProgressiveContentPack pack)
    {
        if (!foregroundActive || string.IsNullOrEmpty(pack.eventId)) return false;
        foreach (var pending in eventRequests)
            if (pending.eventId == pack.eventId && !pending.cancelled && pending.owner != null
                && (!pending.wasActive || pending.owner.isActiveAndEnabled)) return false;
        return true;
    }

    private IEnumerator PrefetchEvents()
    {
        foreach (var pack in manifest.events)
        {
            if (foreground.Count > 0 || paused) yield break;
            if (!dismissedEventPrompts.Contains(pack.eventId) && EventContentPolicy.Eligible(pack.eventId, manifest.prefetchLevels)) yield return DownloadPack(pack);
        }
    }

    private IEnumerator LoadEventAsset(ProgressiveContentPack pack, ProgressiveContentAsset asset)
    {
        var bundle = loadedBundles[asset.bundle];
        AssetBundleRequest op;
        if (asset.assetType == "Sprite") op = bundle.LoadAssetWithSubAssetsAsync<Sprite>(asset.path);
        else if (asset.assetType == "Material") op = bundle.LoadAssetWithSubAssetsAsync<Material>(asset.path);
        else op = bundle.LoadAssetWithSubAssetsAsync<Texture2D>(asset.path);
        yield return op;
        // Decode a sprite sheet once, then index all requested sub-sprites from that same response.
        foreach (var wanted in pack.assets)
        {
            if (wanted.bundle != asset.bundle || wanted.path != asset.path || wanted.assetType != asset.assetType) continue;
            Object found = null;
            foreach (var value in op.allAssets)
                if (value != null && value.name == wanted.subAssetName && value.GetType().Name == wanted.assetType)
                { found = value; break; }
            if (found == null) { LastError = "event_asset_missing"; yield break; }
            loadedAssets[wanted.guid] = found;
        }
    }

    private bool HasEventConsumer(string id)
    {
        if (eventConsumers.ContainsKey(id)) return true;
        foreach (var pending in eventRequests)
            if (pending.eventId == id && !pending.cancelled && pending.owner != null) return true;
        return false;
    }

    private void ReleaseUnusedEvents()
    {
        // Worker boundary only: no Unity asynchronous asset operation is in flight.
        eventReleaseBuffer.Clear();
        foreach (var pair in eventConsumers)
        {
            pair.Value.RemoveWhere(value => value == null);
            if (pair.Value.Count == 0) eventReleaseBuffer.Add(pair.Key);
        }
        foreach (string id in eventReleaseBuffer) eventConsumers.Remove(id);
        // The joined Safari run presents its result on the next menu scene. Keep its
        // already-loaded art across gameplay, even after the previous scene host is gone.
        var safari = manifest.FindEvent(EventContentPolicy.Safari);
        bool keepSafari = safari != null && readyPacks.Contains(safari.Key)
            && (SafariState.IsRunActive || SafariState.PendingOutcome != SafariRoundOutcome.None
                || SafariState.RunStatus == SafariRunStatus.AwaitingResult)
            && EventContentPolicy.Eligible(EventContentPolicy.Safari, 0);
        var retainedBundles = retainedEventBundles; retainedBundles.Clear();
        var retainedAssets = retainedEventAssets; retainedAssets.Clear();
        foreach (var pack in manifest.events)
        {
            if (!(keepSafari && pack == safari) && !HasEventConsumer(pack.eventId) && !queued.Contains(pack.Key)) continue;
            foreach (string id in pack.bundles) retainedBundles.Add(id);
            foreach (var asset in pack.assets) retainedAssets.Add(asset.guid);
        }
        foreach (var pack in manifest.events)
        {
            if ((keepSafari && pack == safari) || HasEventConsumer(pack.eventId) || queued.Contains(pack.Key)) continue;
            if (eventBindings != null && boundEvents.Remove(pack.eventId)) Apply(eventBindings.references, pack.eventId, true);
            foreach (var asset in pack.assets) if (!retainedAssets.Contains(asset.guid)) loadedAssets.Remove(asset.guid);
            foreach (string id in pack.bundles)
                if (!retainedBundles.Contains(id) && loadedBundles.TryGetValue(id, out var bundle))
                {
                    // A non-event reward overlay may still hold a sprite; never destroy live references.
                    bundle.Unload(false);
                    loadedBundles.Remove(id);
                }
            readyPacks.Remove(pack.Key);
        }
    }
}
