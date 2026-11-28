#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>City sources stay local until verified activation. Moves preserve .meta GUIDs.</summary>
internal static class ProgressiveCityBuild
{
    internal const string CatalogPath = "Assets/_Project/Settings/Wonders/WonderCatalog.asset";
    internal const string LocalRoot = "Assets/_Project/Resources/WorldTour";
    internal const string RemoteRoot = "Assets/_Project/RemoteContent/WorldTour";

    [Serializable] internal sealed class Stamp
    {
        public string guid, fileHash, importerHash;
    }

    internal static ProgressiveContentPack[] Collect()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(CatalogPath);
        if (catalog == null || catalog.Count == 0) throw new BuildFailedException("Şehir kataloğu eksik.");
        var ids = new HashSet<string>();
        var packs = new List<ProgressiveContentPack>();
        // Raw order only. Never call catalog.Get() here: editor must not write player WonderOrder.
        foreach (var wonder in catalog.wonders)
        {
            if (wonder == null || !Regex.IsMatch(wonder.wonderId ?? "", "^[a-z0-9_-]+$") || !ids.Add(wonder.wonderId))
                throw new BuildFailedException("Eksik/tekrarlı/geçersiz şehir kimliği.");
            string bundle = "city-" + wonder.wonderId;
            var assets = new List<ProgressiveContentAsset>();
            foreach (string suffix in new[] { "", "_Site" })
            {
                string filename = "City_" + wonder.wonderId + suffix + ".png";
                string local = LocalRoot + "/" + filename, remote = RemoteRoot + "/" + filename;
                if (File.Exists(local) && File.Exists(remote)) throw new BuildFailedException("İki şehir kaynağı var: " + filename);
                string path = File.Exists(local) ? local : remote;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null || importer == null
                    || importer.spriteImportMode != SpriteImportMode.Single || !string.IsNullOrEmpty(importer.spritePackingTag))
                    throw new BuildFailedException("Şehir görseli tek, atlasa paketlenmemiş sprite olmalı: " + path);
                if (AssetDatabase.GetDependencies(path, true).Any(p => p != path))
                    throw new BuildFailedException("Şehir görselinin harici bağımlılığı var: " + path);
                assets.Add(new ProgressiveContentAsset { guid = AssetDatabase.AssetPathToGUID(path),
                    path = path.ToLowerInvariant(), resourcePath = "WorldTour/" + Path.GetFileNameWithoutExtension(filename), bundle = bundle });
            }
            packs.Add(new ProgressiveContentPack { cityId = wonder.wonderId, bundles = new[] { bundle }, assets = assets.ToArray() });
        }
        return packs.ToArray();
    }

    internal static Stamp[] Stamps(ProgressiveContentPack[] cities)
        => cities.SelectMany(c => c.assets).Select(a => {
            string path = AssetDatabase.GUIDToAssetPath(a.guid);
            return new Stamp { guid = a.guid, fileHash = ProgressiveContentWindow.Sha256(path),
                importerHash = ProgressiveContentWindow.Sha256(path + ".meta") };
        }).ToArray();

    internal static void ValidateStamps(Stamp[] stamps, ProgressiveContentPack[] cities)
    {
        if (stamps == null || cities == null || stamps.Length != cities.Sum(c => c.assets.Length)
            || !new HashSet<string>(stamps.Select(s => s.guid)).SetEquals(cities.SelectMany(c => c.assets).Select(a => a.guid)))
            throw new BuildFailedException("Şehir kaynak kaydı eksik; içerik paketlerini yeniden hazırla.");
        foreach (var stamp in stamps)
        {
            string path = AssetDatabase.GUIDToAssetPath(stamp.guid);
            if (!File.Exists(path) || ProgressiveContentWindow.Sha256(path) != stamp.fileHash
                || ProgressiveContentWindow.Sha256(path + ".meta") != stamp.importerHash)
                throw new BuildFailedException("Şehir kaynağı/import ayarı değişmiş: " + path);
        }
    }

    internal static void AssertExclusive(ProgressiveContentPack[] cities)
    {
        var paths = new HashSet<string>(cities.SelectMany(c => c.assets).Select(a => AssetDatabase.GUIDToAssetPath(a.guid)));
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
            .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p) && !paths.Contains(p)))
            .Concat(AssetDatabase.FindAssets("t:SpriteAtlas").Select(AssetDatabase.GUIDToAssetPath)).Distinct().ToArray();
        foreach (string dependency in AssetDatabase.GetDependencies(roots, true))
            if (paths.Contains(dependency))
                throw new BuildFailedException("Şehir görseline yerel sahne/Resources/atlas referansı var; önce ayır: " + dependency);
    }

    internal static void Move(ProgressiveContentPack[] cities, bool remote, List<(string from, string to)> journal)
    {
        string root = remote ? RemoteRoot : LocalRoot;
        EnsureFolder(root);
        foreach (var asset in cities.SelectMany(c => c.assets))
        {
            string from = AssetDatabase.GUIDToAssetPath(asset.guid);
            string to = root + "/" + Path.GetFileName(asset.resourcePath) + ".png";
            if (from == to) continue;
            if (!File.Exists(from) || File.Exists(to)) throw new BuildFailedException("Şehir taşıma yolu uygun değil: " + to);
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
            journal.Add((from, to));
        }
    }

    internal static void Rollback(List<(string from, string to)> journal)
    {
        var errors = new List<string>();
        for (int i = journal.Count - 1; i >= 0; i--)
        {
            string error = AssetDatabase.MoveAsset(journal[i].to, journal[i].from);
            if (!string.IsNullOrEmpty(error)) errors.Add(error);
        }
        if (errors.Count > 0) throw new BuildFailedException("Şehir taşıma geri alınamadı: " + string.Join("; ", errors));
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    internal static bool HasMovedSources => Directory.Exists(RemoteRoot) && Directory.EnumerateFiles(RemoteRoot, "City_*.png").Any();

    internal static void ValidateInstalled(ProgressiveContentManifest manifest)
    {
        foreach (var asset in manifest.cities.SelectMany(c => c.assets))
            if (AssetDatabase.GUIDToAssetPath(asset.guid) != RemoteRoot + "/" + Path.GetFileName(asset.resourcePath) + ".png"
                || File.Exists(LocalRoot + "/" + Path.GetFileName(asset.resourcePath) + ".png"))
                throw new BuildFailedException("Şehir hâlâ Resources içinde veya kaynak yanlış: " + asset.resourcePath);
        AssertExclusive(manifest.cities);
    }
}
#endif
