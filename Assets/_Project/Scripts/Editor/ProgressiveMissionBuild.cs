#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Packages only authored mission/world-map art and strips its scene references from the player.</summary>
internal static class ProgressiveMissionBuild
{
    internal const string Id = "mission-map";
    private static readonly string[] Roots = {
        "Assets/_Project/Art/UI/Missions/1/",
        "Assets/_Project/Art/UI/Missions/ND_M001/"
    };

    internal static bool IsCandidate(string path)
        => !string.IsNullOrEmpty(path) && Roots.Any(root => path.StartsWith(root, StringComparison.Ordinal));

    internal static bool IsConsumer(Component component)
    {
        if (component == null) return false;
        if (component is WorldMapController || component is WorldMapRegion || component is WorldMapIsland
            || component is RegionUnlockItem || component is RegionProgressBar || component is RegionUnlockListPanel
            || component is JourneyScreenController) return true;
        for (var transform = component.transform; transform != null; transform = transform.parent)
            if (transform.name.Equals("WorldMap", StringComparison.OrdinalIgnoreCase)
                || transform.name.Equals("MissionMap", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool Artwork(UnityEngine.Object value)
        => value is Sprite || value is Material || value is Texture2D;

    internal const string CatalogPath = "Assets/_Project/Settings/Wonders/WonderCatalog.asset";
    internal static ProgressiveContentPack[] Collect(List<AssetBundleBuild> builds)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(CatalogPath);
        var packs = new List<ProgressiveContentPack>();
        foreach (var wonder in catalog.wonders)
        {
            var objects = new Dictionary<string, UnityEngine.Object>();
            ProgressiveEventBuild.Visit(wonder, (property, value) => {
                if (Artwork(value) && AssetDatabase.GetAssetPath(value).Contains("/Missions/ND_M001/Build/"))
                    objects[ProgressiveEventBuild.Key(value)] = value;
            });
            if (objects.Count == 0) continue;
            string id = "mission-" + wonder.wonderId;
            if (packs.Any(p => p.eventId == id)) throw new BuildFailedException("Duplicate mission: " + id);
            packs.Add(MakePack(builds, objects, id, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(wonder))));
        }
        return packs.ToArray();
    }

    private static ProgressiveContentPack MakePack(List<AssetBundleBuild> builds,
        Dictionary<string, UnityEngine.Object> objects, string id, string guid)
    {
        string bundleId = "event-" + id;
        builds.Add(new AssetBundleBuild {
            assetBundleName = bundleId,
            assetNames = objects.Values.Select(value => AssetDatabase.GetAssetPath(value)).Where(path => !string.IsNullOrEmpty(path))
                .Distinct().OrderBy(path => path).ToArray()
        });
        var assets = objects.Select(pair => {
            string path = AssetDatabase.GetAssetPath(pair.Value);
            return new ProgressiveContentAsset {
                guid = pair.Key,
                sourceGuid = AssetDatabase.AssetPathToGUID(path),
                path = path.ToLowerInvariant(),
                subAssetName = pair.Value.name,
                assetType = pair.Value.GetType().Name,
                bundle = bundleId
            };
        }).ToArray();
        if (assets.GroupBy(asset => asset.path + "|" + asset.assetType + "|" + asset.subAssetName).Any(group => group.Count() != 1))
            throw new BuildFailedException("Mission map has duplicate sprite subasset names; separate the source textures.");
        return new ProgressiveContentPack {
            eventId = id,
            configGuid = guid,
            unlockField = "on-demand",
            assets = assets,
            bundles = new[] { bundleId }
        };
    }
}
#endif
