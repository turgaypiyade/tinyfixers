#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Source art/scenes remain authored. Activation creates stripped runtime config copies;
/// the scene processor rewrites only the player build's scene copy, never the saved scene.</summary>
internal static class ProgressiveEventBuild
{
    internal const string BindingPath = "Assets/_Project/Resources/EventArtBindings.asset";
    private const string Root = "Assets/_Project/RemoteContent/EventSources";
    private const string StatePath = Root + "/activation.json";
    private const string RiverPath = "Assets/_Project/Resources/Events/BridgeRepairRiver.mat";
    private const string LinkPath = Root + "/link.xml";
    internal static readonly string[] Ids = { "safari", "bridge-repair", "harvest", "gift-festival", "gear-cup", "claw-machine" };
    private static readonly string[] ConfigNames = { "SafariConfig", "BridgeRepairConfig", "HarvestConfig", "GiftFestivalArt", "GearCupConfig", "ClawMachineConfig" };
    private static readonly string[] Folders = {
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari", "Assets/_Project/Art/UI/MainScreenEvents/BridgeRepair",
        "Assets/_Project/Art/UI/MainScreenEvents/Harvest", "Assets/_Project/Art/UI/MainScreenEvents/GiftFestival",
        "Assets/_Project/Art/UI/Missions/GearCup", "Assets/_Project/Art/UI/MainScreenEvents/ClawMachine" };
    // These lightweight badges also appear in local loss/level reward UI, without opening an event screen.
    private static readonly HashSet<string> LocalNames = new(StringComparer.Ordinal) {
        "SafariEventBTNV2", "SafariLostIcon", "BridgeEventLogo", "Harvest_Logo", "Harvest_Trowel",
        "GiftFestivalLogo", "Gear Trophy Stadium Badge", "Glossy Golden Gear Icon", "ClawMachineIcon"
    };
    [Serializable] internal sealed class ConfigCopy { public string sourceGuid, originalPath, cloneGuid; }
    [Serializable] internal sealed class ArtMove { public string sourceGuid, originalPath; }
    [Serializable] internal sealed class State { public ConfigCopy[] missions; public ConfigCopy[] configs; public ArtMove[] artMoves; public string riverGuid; public string manifestHash; }
    [Serializable] internal sealed class Stamp { public string guid, hash, importerHash; }
    internal static bool Active => File.Exists(StatePath);
    private static State ReadState() => JsonUtility.FromJson<State>(File.ReadAllText(StatePath));
    internal static string ConfigPath(int index)
    {
        if (Active)
        {
            var copy = ReadState().configs.FirstOrDefault(c => Path.GetFileNameWithoutExtension(c.originalPath) == ConfigNames[index]);
            if (copy != null) return AssetDatabase.GUIDToAssetPath(copy.sourceGuid);
        }
        return "Assets/_Project/Resources/Events/" + ConfigNames[index] + ".asset";
    }
    private static bool Candidate(string path, out int index)
    {
        if (path == RiverPath || path == Root + "/BridgeRepairRiver.mat") { index = 1; return true; }
        if (path.StartsWith("Assets/_Project/Resources/RisingEvent/", StringComparison.Ordinal)) { index = 0; return true; }
        if (path.StartsWith(Root + "/Safari/", StringComparison.Ordinal)) { index = 0; return true; }
        index = Array.FindIndex(Folders, p => path.StartsWith(p + "/", StringComparison.Ordinal));
        if (index < 0 && path.StartsWith("Assets/_Project/Art/UI/MainScreenEvents/RisingEvent/", StringComparison.Ordinal)) index = 0;
        return index >= 0 && !LocalNames.Contains(Path.GetFileNameWithoutExtension(path));
    }
    private static bool Artwork(Object value) => value is Sprite || value is Material || value is Texture2D;
    internal static string Key(Object value)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id))
            return "";
        return guid + ":" + id;
    }
    internal static void Visit(Object target, Action<SerializedProperty, Object> visitor)
    {
        var so = new SerializedObject(target);
        var property = so.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                visitor(property, property.objectReferenceValue);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static string Owner(Component component)
    {
        if (ProgressiveMissionBuild.IsConsumer(component)) return ProgressiveMissionBuild.Id;
        for (var t = component.transform; t != null; t = t.parent)
        {
            string text = t.name + " " + (t == component.transform ? component.GetType().Name : "");
            if (text.IndexOf("Safari", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Rising", StringComparison.OrdinalIgnoreCase) >= 0) return Ids[0];
            if (text.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0) return Ids[1];
            if (text.IndexOf("Harvest", StringComparison.OrdinalIgnoreCase) >= 0) return Ids[2];
            if (text.IndexOf("GiftFestival", StringComparison.OrdinalIgnoreCase) >= 0) return Ids[3];
            if (text.IndexOf("GearCup", StringComparison.OrdinalIgnoreCase) >= 0) return Ids[4];
            if (text.IndexOf("ClawMachine", StringComparison.OrdinalIgnoreCase) >= 0) return Ids[5];
        }
        return null;
    }

    internal static ProgressiveContentPack[] Collect(List<AssetBundleBuild> builds)
    {
        var owners = new Dictionary<string, HashSet<string>>();
        var objects = new Dictionary<string, Object>();
        void Add(Object value, string owner)
        {
            string path = AssetDatabase.GetAssetPath(value);
            if (!Artwork(value) || !Candidate(path, out _)) return;
            if (owner == null) throw new BuildFailedException("Event art has an ungated scene consumer: " + path);
            if (!owners.TryGetValue(path, out var set)) owners[path] = set = new HashSet<string>();
            set.Add(owner); objects[Key(value)] = value;
        }
        for (int i = 0; i < Ids.Length; i++)
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath(i));
            if (config == null) throw new BuildFailedException("Event config missing: " + ConfigPath(i));
            Visit(config, (_, value) => Add(value, Ids[i]));
        }
        foreach (var setting in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            // Isolated preview scene: no scene switching, saving, or mutation of the user's open scene.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(setting.path);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        if (component != null) Visit(component, (_, value) => Add(value, Owner(component)));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
        string riverPath = Active ? AssetDatabase.GUIDToAssetPath(ReadState().riverGuid) : RiverPath;
        var river = AssetDatabase.LoadAssetAtPath<Material>(riverPath);
        if (river == null) throw new BuildFailedException("Bridge river material missing: " + riverPath);
        Add(river, Ids[1]);
        // Explicitly assign candidate dependencies (e.g. a material texture) to the same event(s).
        foreach (var pair in owners.ToArray())
            foreach (string dependency in AssetDatabase.GetDependencies(pair.Key, true))
                if (Candidate(dependency, out _) && dependency != pair.Key)
                {
                    if (!owners.TryGetValue(dependency, out var set)) owners[dependency] = set = new HashSet<string>();
                    set.UnionWith(pair.Value);
                }
        foreach (string path in owners.Keys)
        {
            if (path.Contains("/Resources/") && path != RiverPath
                && !path.StartsWith("Assets/_Project/Resources/RisingEvent/", StringComparison.Ordinal))
                throw new BuildFailedException("Remote art must be outside Resources: " + path);
            if (AssetImporter.GetAtPath(path) is TextureImporter texture && !string.IsNullOrEmpty(texture.spritePackingTag))
                throw new BuildFailedException("Remote event sprite has a legacy atlas tag: " + path);
        }
        foreach (string atlas in AssetDatabase.FindAssets("t:SpriteAtlas").Select(AssetDatabase.GUIDToAssetPath))
            if (AssetDatabase.GetDependencies(atlas, true).Any(owners.ContainsKey))
                throw new BuildFailedException("Remote event art belongs to a local SpriteAtlas: " + atlas);
        var assignments = owners.ToDictionary(p => p.Key, p => p.Value.Count > 1 ? "event-shared" : "event-" + p.Value.First());
        foreach (var group in assignments.GroupBy(p => p.Value))
            builds.Add(new AssetBundleBuild { assetBundleName = group.Key, assetNames = group.Select(p => p.Key).OrderBy(p => p).ToArray() });
        var assets = objects.Select(pair => new ProgressiveContentAsset {
            guid = pair.Key, sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Value)),
            path = AssetDatabase.GetAssetPath(pair.Value).ToLowerInvariant(), subAssetName = pair.Value.name,
            assetType = pair.Value.GetType().Name, bundle = assignments[AssetDatabase.GetAssetPath(pair.Value)],
            resourcePath = pair.Value == river ? "Events/BridgeRepairRiver" : null
        }).ToArray();
        // Runtime resolves subassets by (type,name); ambiguous names are rejected before building.
        if (assets.GroupBy(a => a.path + "|" + a.assetType + "|" + a.subAssetName).Any(g => g.Count() != 1))
            throw new BuildFailedException("Event sprite subasset names must be unique within a source file.");
        return Ids.Select((id, i) => new ProgressiveContentPack {
            eventId = id, configGuid = AssetDatabase.AssetPathToGUID(i == 3 ? "Assets/_Project/Resources/Events/GiftFestivalConfig.asset" : ConfigPath(i)),
            unlockField = i == 3 ? "unlockAfterLevel" : i == 5 ? "unlockLevel" : "minLevelGate",
            assets = assets.Where(a => owners[AssetDatabase.GUIDToAssetPath(a.sourceGuid)].Contains(id)).ToArray(),
            bundles = assignments.Where(p => owners[p.Key].Contains(id)).Select(p => p.Value).Distinct().OrderBy(p => p).ToArray()
        }).Where(p => p.assets.Length > 0).ToArray();
    }

    internal static Stamp[] Stamps(ProgressiveContentPack[] packs)
    {
        // Stamp only event bundle source files, their source config assets, and authored scenes.
        // Do not stamp transitive local dependencies such as fonts: they remain local and are
        // intentionally outside the remote bundle payload and its immutable manifest.
        var paths = packs.SelectMany(p => p.assets).Select(a => AssetDatabase.GUIDToAssetPath(a.sourceGuid))
            .Concat(packs.Where(p => p.eventId.StartsWith("mission-")).Select(p => AssetDatabase.GUIDToAssetPath(p.configGuid)))
            .Concat(Enumerable.Range(0, Ids.Length).Select(ConfigPath))
            .Concat(new[] { "Assets/_Project/Resources/Events/GiftFestivalConfig.asset", RiverPath })
            .Concat(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(p))
            .Distinct().OrderBy(p => p);
        return paths.Select(p => new Stamp { guid = AssetDatabase.AssetPathToGUID(p),
            hash = ProgressiveContentWindow.Sha256(p), importerHash = ProgressiveContentWindow.Sha256(p + ".meta") }).ToArray();
    }

    internal static void ValidateStamps(Stamp[] stamps)
    {
        if (stamps == null || stamps.Length == 0) throw new BuildFailedException("Event source stamps missing; rebuild content.");
        foreach (var stamp in stamps)
        {
            string path = AssetDatabase.GUIDToAssetPath(stamp.guid);
            if (!File.Exists(path) || ProgressiveContentWindow.Sha256(path) != stamp.hash
                || ProgressiveContentWindow.Sha256(path + ".meta") != stamp.importerHash)
                throw new BuildFailedException("Event source/import settings changed: " + path);
        }
    }
    private static Dictionary<string, (string id, ProgressiveContentAsset asset)> Lookup(ProgressiveContentManifest manifest)
    {
        var map = new Dictionary<string, (string, ProgressiveContentAsset)>();
        foreach (var pack in manifest.events)
            foreach (var asset in pack.assets) map.TryAdd(asset.guid, (pack.eventId, asset));
        return map;
    }
    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    internal static void Activate(ProgressiveContentManifest manifest)
    {
        if (manifest.events == null || manifest.events.Length == 0) { Restore(); return; }
        if (Active) Restore();
        EnsureFolder(Root);
        if (File.Exists(BindingPath) || File.Exists(LinkPath)) throw new BuildFailedException("Unowned event binding/link output exists; restore/recover it before activation.");
        var state = new State { configs = Array.Empty<ConfigCopy>(), artMoves = Array.Empty<ArtMove>(), manifestHash = ProgressiveContentWindow.Sha256(Path.Combine(ProgressiveContentWindow.Output, "manifest.json")) };
        var copies = new List<ConfigCopy>();
        var references = new List<EventArtReference>();
        var map = Lookup(manifest);
        try
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                string original = ConfigPath(i);
                var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(original);
                string guid = AssetDatabase.AssetPathToGUID(original);
                var clone = Object.Instantiate(source); clone.name = source.name;
                Visit(clone, (property, value) => {
                    if (!Artwork(value) || !map.TryGetValue(Key(value), out var entry)) return;
                    var binding = new EventArtReference { eventId = Ids[i], assetKey = entry.asset.guid,
                        propertyPath = property.propertyPath, target = clone };
                    binding.Set(value); // Validate runtime field/array binding while rollback is still possible.
                    references.Add(binding);
                    property.objectReferenceValue = null;
                });
                var copy = new ConfigCopy { sourceGuid = guid, originalPath = original };
                copies.Add(copy); state.configs = copies.ToArray();
                File.WriteAllText(StatePath, JsonUtility.ToJson(state, true)); // Journal before moving the original.
                string moved = Root + "/" + Path.GetFileName(original);
                string error = AssetDatabase.MoveAsset(original, moved);
                if (!string.IsNullOrEmpty(error)) { Object.DestroyImmediate(clone); throw new BuildFailedException(error); }
                AssetDatabase.CreateAsset(clone, original);
                copy.cloneGuid = AssetDatabase.AssetPathToGUID(original);
                File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
            }
            var wonderCatalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(ProgressiveMissionBuild.CatalogPath);
            var missions = new List<ConfigCopy>();
            foreach (var pack in manifest.events.Where(p => p.eventId.StartsWith("mission-") && p.eventId != ProgressiveMissionBuild.Id))
            {
                var source = AssetDatabase.LoadAssetAtPath<WonderDefinition>(AssetDatabase.GUIDToAssetPath(pack.configGuid));
                int slot = Array.IndexOf(wonderCatalog.wonders, source);
                if (source == null || slot < 0) throw new BuildFailedException("Mission source missing: " + pack.eventId);
                string path = Root + "/runtime-" + pack.configGuid + ".asset";
                if (File.Exists(path)) throw new BuildFailedException("Mission copy already exists: " + path);
                var clone = Object.Instantiate(source);
                clone.name = source.name;
                Visit(clone, (property, value) => {
                    var key = Key(value);
                    if (!pack.assets.Any(a => a.guid == key)) return;
                    references.Add(new EventArtReference { target = clone, propertyPath = property.propertyPath,
                        eventId = pack.eventId, assetKey = key });
                    property.objectReferenceValue = null;
                });
                AssetDatabase.CreateAsset(clone, path);
                missions.Add(new ConfigCopy { sourceGuid = pack.configGuid, originalPath = path, cloneGuid = AssetDatabase.AssetPathToGUID(path) });
                state.missions = missions.ToArray();
                File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
                wonderCatalog.wonders[slot] = clone;
                EditorUtility.SetDirty(wonderCatalog);
            }
            // Safari's legacy map art lives in Resources/RisingEvent. Move its source files out
            // of Resources after their bundle has been built, preserving the original GUIDs.
            // The player scene processor replaces serialized references with event bindings.
            var artMoves = manifest.events.SelectMany(p => p.assets).Select(a => a.sourceGuid).Distinct()
                .Select(guid => new ArtMove { sourceGuid = guid, originalPath = AssetDatabase.GUIDToAssetPath(guid) })
                .Where(m => m.originalPath.StartsWith("Assets/_Project/Resources/RisingEvent/", StringComparison.Ordinal))
                .ToArray();
            state.artMoves = artMoves;
            File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
            foreach (var move in artMoves)
            {
                string destination = Root + "/Safari/" + Path.GetFileName(move.originalPath);
                EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
                string moveError = AssetDatabase.MoveAsset(move.originalPath, destination);
                if (!string.IsNullOrEmpty(moveError)) throw new BuildFailedException(moveError);
            }
            state.riverGuid = AssetDatabase.AssetPathToGUID(RiverPath);
            File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
            string riverError = AssetDatabase.MoveAsset(RiverPath, Root + "/BridgeRepairRiver.mat");
            if (!string.IsNullOrEmpty(riverError)) throw new BuildFailedException(riverError);
            var catalog = ScriptableObject.CreateInstance<EventArtBindings>(); catalog.references = references.ToArray();
            AssetDatabase.CreateAsset(catalog, BindingPath);
            // Serialized binding paths survive IL2CPP. Preserve only fields of the involved project/UI types.
            var types = references.Select(r => r.target.GetType()).Concat(new[] { typeof(BridgeCharacterDef), typeof(HarvestCropDef) }).ToHashSet();
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scene.path);
                try { foreach (var root in preview.GetRootGameObjects()) foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) Visit(c, (_, v) => { if (Artwork(v) && map.ContainsKey(Key(v))) types.Add(c.GetType()); }); }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            }
            string XmlEscape(string value) => value.Replace("&", "&amp;").Replace("\"", "&quot;")
                .Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;");
            string xml = "<linker>\n" + string.Join("\n", types.GroupBy(t => t.Assembly.GetName().Name).Select(g =>
                "  <assembly fullname=\"" + XmlEscape(g.Key) + "\">\n" + string.Join("\n", g.Select(t =>
                    "    <type fullname=\"" + XmlEscape(t.FullName.Replace('+', '/')) + "\" preserve=\"all\"/>")) + "\n  </assembly>")) + "\n</linker>\n";
            File.WriteAllText(LinkPath, xml);
            AssetDatabase.ImportAsset(LinkPath); AssetDatabase.ImportAsset(StatePath);
            AssetDatabase.SaveAssets();
            ValidateInstalled(manifest);
        }
        catch { Restore(); throw; }
    }

    internal static void Restore()
    {
        if (!Active) return;
        var state = ReadState();
        if (state.missions != null)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(ProgressiveMissionBuild.CatalogPath);
            foreach (var copy in state.missions)
            {
                var clone = AssetDatabase.LoadAssetAtPath<WonderDefinition>(AssetDatabase.GUIDToAssetPath(copy.cloneGuid));
                if (clone == null) continue;
                int slot = Array.IndexOf(catalog.wonders, clone);
                var source = AssetDatabase.LoadAssetAtPath<WonderDefinition>(AssetDatabase.GUIDToAssetPath(copy.sourceGuid));
                if (source == null) throw new BuildFailedException("Missing authored mission " + copy.sourceGuid);
                if (slot >= 0) catalog.wonders[slot] = source;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(clone));
            }
        }
        if (state.artMoves != null)
            foreach (var move in state.artMoves.Reverse())
            {
                string source = AssetDatabase.GUIDToAssetPath(move.sourceGuid);
                if (source == move.originalPath) continue;
                if (File.Exists(move.originalPath))
                    throw new BuildFailedException("Refusing to replace an unrelated event art asset: " + move.originalPath);
                string moveError = AssetDatabase.MoveAsset(source, move.originalPath);
                if (!string.IsNullOrEmpty(moveError)) throw new BuildFailedException(moveError);
            }
        foreach (var copy in state.configs.Reverse())
        {
            string source = AssetDatabase.GUIDToAssetPath(copy.sourceGuid);
            if (source == copy.originalPath) continue;
            if (File.Exists(copy.originalPath))
            {
                if (string.IsNullOrEmpty(copy.cloneGuid) || AssetDatabase.AssetPathToGUID(copy.originalPath) != copy.cloneGuid)
                    throw new BuildFailedException("Refusing to replace an unrelated config: " + copy.originalPath);
                AssetDatabase.DeleteAsset(copy.originalPath);
            }
            string error = AssetDatabase.MoveAsset(source, copy.originalPath);
            if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
        }
        if (!string.IsNullOrEmpty(state.riverGuid))
        {
            string from = AssetDatabase.GUIDToAssetPath(state.riverGuid);
            if (from != RiverPath)
            {
                string error = AssetDatabase.MoveAsset(from, RiverPath);
                if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
            }
        }
        if (File.Exists(BindingPath)) AssetDatabase.DeleteAsset(BindingPath);
        if (File.Exists(LinkPath)) AssetDatabase.DeleteAsset(LinkPath);
        if (!AssetDatabase.DeleteAsset(StatePath) && File.Exists(StatePath)) File.Delete(StatePath);
    }

    internal static void ValidateInstalled(ProgressiveContentManifest manifest)
    {
        if (manifest.events == null || manifest.events.Length == 0)
        {
            if (Active) throw new BuildFailedException("Event config copies installed without event packs.");
            return;
        }
        var bindingCatalog = AssetDatabase.LoadAssetAtPath<EventArtBindings>(BindingPath);
        if (!Active || bindingCatalog == null || !File.Exists(LinkPath))
            throw new BuildFailedException("Event runtime bindings/linker metadata missing; activate the verified content.");
        int expectedBindings = bindingCatalog.references.Count(r => r.eventId.StartsWith("mission-"));
        var map = Lookup(manifest);
        foreach (var copy in ReadState().configs)
        {
            if (AssetDatabase.AssetPathToGUID(copy.originalPath) != copy.cloneGuid)
                throw new BuildFailedException("Runtime event config changed identity: " + copy.originalPath);
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(copy.sourceGuid));
            var expected = Object.Instantiate(source);
            Visit(expected, (p, v) => { if (Artwork(v) && map.ContainsKey(Key(v))) p.objectReferenceValue = null; });
            var installed = AssetDatabase.LoadAssetAtPath<ScriptableObject>(copy.originalPath);
            int configIndex = Array.IndexOf(ConfigNames, Path.GetFileNameWithoutExtension(copy.originalPath));
            Visit(source, (property, value) => {
                if (!Artwork(value) || !map.TryGetValue(Key(value), out var entry)) return;
                expectedBindings++;
                if (configIndex < 0 || bindingCatalog.references.Count(r => r.target == installed && r.eventId == Ids[configIndex]
                    && r.propertyPath == property.propertyPath && r.assetKey == entry.asset.guid) != 1)
                    throw new BuildFailedException("Event runtime binding missing/changed: " + copy.originalPath + "/" + property.propertyPath);
            });
            expected.name = installed.name;
            bool same = EditorJsonUtility.ToJson(expected) == EditorJsonUtility.ToJson(installed);
            Object.DestroyImmediate(expected);
            if (!same) throw new BuildFailedException("Runtime event config differs from source: " + copy.originalPath + ". Edit the source config, rebuild and reactivate.");
        }
        if (bindingCatalog.references.Length != expectedBindings)
            throw new BuildFailedException("Unexpected event runtime bindings; reactivate the verified content.");
        var resourceRoots = AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/")
            && !AssetDatabase.IsValidFolder(p) && !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
        var resourceDeps = AssetDatabase.GetDependencies(resourceRoots, true).ToHashSet();
        foreach (var copy in ReadState().configs)
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(copy.sourceGuid);
            foreach (string dependency in AssetDatabase.GetDependencies(sourcePath, true))
                if (Candidate(dependency, out _)) resourceDeps.Remove(dependency); // original lives outside Resources now
        }
        foreach (var pack in manifest.events)
            foreach (var asset in pack.assets)
            {
                string path = AssetDatabase.GUIDToAssetPath(asset.sourceGuid);
                if (resourceDeps.Contains(path))
                    throw new BuildFailedException("Remote event art still reachable from another Resources asset: " + path);
            }
    }

    internal static void ProcessScene(Scene scene)
    {
        if (!Active || !File.Exists(ProgressiveContentWindow.RuntimeManifestPath)) return;
        var manifest = JsonUtility.FromJson<ProgressiveContentManifest>(File.ReadAllText(ProgressiveContentWindow.RuntimeManifestPath));
        var map = Lookup(manifest);
        var copies = ReadState().configs.ToDictionary(c => c.sourceGuid, c => AssetDatabase.LoadAssetAtPath<Object>(c.originalPath));
        var refs = new List<EventArtReference>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null) Visit(component, (property, value) => {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                    if (copies.TryGetValue(guid, out var clone)) { property.objectReferenceValue = clone; return; }
                    if (!Artwork(value) || !map.TryGetValue(Key(value), out var entry)) return;
                    string id = Owner(component);
                    if (id == null || manifest.FindEvent(id) == null || !manifest.FindEvent(id).assets.Any(a => a.guid == entry.asset.guid))
                        throw new BuildFailedException("Ungated event consumer: " + component.name + "/" + property.propertyPath);
                    var binding = new EventArtReference { eventId = id, assetKey = entry.asset.guid, propertyPath = property.propertyPath, target = component };
                    // Validate path mapping against the actual type while the source value is still available.
                    binding.Set(value);
                    refs.Add(binding); property.objectReferenceValue = null;
                });
        var remoteSources = AssetDatabase.GetDependencies(manifest.events.SelectMany(p => p.assets).Select(a => AssetDatabase.GUIDToAssetPath(a.sourceGuid)).Distinct().ToArray(), true).Where(p => Candidate(p, out _)).Select(AssetDatabase.AssetPathToGUID).ToHashSet();
        var missionSources = manifest.events.Where(pack => pack.eventId.StartsWith("mission-"))
            .SelectMany(pack => pack.assets).Select(asset => AssetDatabase.GUIDToAssetPath(asset.sourceGuid))
            .Where(ProgressiveMissionBuild.IsCandidate).Select(AssetDatabase.AssetPathToGUID).ToHashSet();
        foreach (var dependency in EditorUtility.CollectDependencies(scene.GetRootGameObjects()))
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dependency));
            if (remoteSources.Contains(guid) || missionSources.Contains(guid))
                throw new BuildFailedException("Unstripped remote art dependency in player scene: " + scene.path + " -> " + AssetDatabase.GetAssetPath(dependency));
        }
        if (refs.Count == 0) return;
        var host = new GameObject("EventArtSceneBindings");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<EventArtSceneBindings>().references = refs.ToArray();
    }
}

public sealed class ProgressiveEventSceneProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 10000;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report != null) ProgressiveEventBuild.ProcessScene(scene); // Never modifies editor Play-mode scenes.
    }
}
#endif
