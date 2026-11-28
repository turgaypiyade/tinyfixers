#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Creates generated LevelData/library copies so each level range only references
/// the obstacle definitions authored in that range. Authored LevelV1 assets are never edited.</summary>
internal static class ProgressiveLevelPackSources
{
    private const string LibraryPath = "Assets/_Project/Settings/ObstacleLibrary.asset";
    private const string GeneratedRoot = "Assets/_Project/RemoteContent/GeneratedLevelPacks";
    private const string LocalRoot = GeneratedRoot + "/local-1-50";

    internal sealed class Result
    {
        public readonly Dictionary<string, string> generatedPathToSourceGuid = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> generatedPathToSourcePath = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> sourceGuidToGeneratedPath = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> sourceGuidToLocalGeneratedPath = new(StringComparer.Ordinal);
        public readonly List<string> generatedLocalPaths = new();
        public readonly List<string> localSourcePaths = new();
    }

    internal static Result Prepare(LevelCatalog catalog)
    {
        var sourceLibrary = AssetDatabase.LoadAssetAtPath<ObstacleLibrary>(LibraryPath);
        if (sourceLibrary == null) throw new BuildFailedException("ObstacleLibrary kaynağı bulunamadı.");
        if (catalog.entries.Any(e => e == null || e.levelData == null || !string.IsNullOrEmpty(e.remoteGuid)))
            throw new BuildFailedException("Önce Yerel içerik moduna dön; kaynak level kayıtları eksik/uzak durumda.");
        var mismatched = catalog.entries.FirstOrDefault(e => e.levelData.obstacleLibrary != sourceLibrary);
        if (mismatched != null)
            throw new BuildFailedException("Level " + mismatched.level + " ortak ObstacleLibrary kaynağını kullanmıyor; otomatik filtreleme güvenli değil.");

        var groups = catalog.entries.GroupBy(e => e.level <= 50 ? 1 : ((e.level - 1) / 50) * 50 + 1)
            .OrderBy(g => g.Key).ToArray();
        var result = new Result();
        foreach (var group in groups)
        {
            var ids = new SortedSet<int>(group.SelectMany(e => ProgressiveLevelArtCatalog.ReadObstacleIds(e.levelData)));
            if (ids.Count == 0) throw new BuildFailedException("Seviye aralığında kullanılan obstacle bulunamadı: " + group.Key);
            string groupName = group.Key == 1 ? "local-1-50" : "levels-" + group.Key + "-" + Math.Min(group.Key + 49, catalog.MaximumGlobalLevel);
            string folder = group.Key == 1 ? LocalRoot : GeneratedRoot + "/" + groupName;
            EnsureFolder(folder);

            var libraryCopy = UnityEngine.Object.Instantiate(sourceLibrary);
            libraryCopy.name = "ObstacleLibrary_" + groupName;
            FilterLibrary(libraryCopy, ids);
            string libraryOut = SaveOwnedAsset(libraryCopy, folder + "/ObstacleLibrary_" + groupName + ".asset");
            if (group.Key == 1) result.generatedLocalPaths.Add(libraryOut);
            var filteredLibrary = AssetDatabase.LoadAssetAtPath<ObstacleLibrary>(libraryOut);
            if (filteredLibrary == null) throw new BuildFailedException("Filtrelenmiş ObstacleLibrary kaydedilemedi: " + libraryOut);
            foreach (int id in ids)
            {
                var original = sourceLibrary.obstacles.FirstOrDefault(def => def != null && (int)def.id == id);
                var retained = filteredLibrary.obstacles.FirstOrDefault(def => def != null && (int)def.id == id);
                if (original == null || retained == null || JsonUtility.ToJson(original) != JsonUtility.ToJson(retained))
                    throw new BuildFailedException(groupName + ": engel tanımı veya aşama referansları eksik/değişmiş: " + (ObstacleId)id);
            }
            if (ids.Contains((int)ObstacleId.FlyEggBox))
            {
                var egg = filteredLibrary.obstacles.FirstOrDefault(def => def != null && def.id == ObstacleId.FlyEgg);
                if (egg == null || egg.GetPreviewSprite() == null || egg.auxiliarySprites == null
                    || egg.auxiliarySprites.Count < 6 || egg.auxiliarySprites.Take(6).Any(sprite => sprite == null))
                    throw new BuildFailedException(groupName + ": FlyEggBox için yumurta veya sineğin altı animasyon parçası eksik.");
            }

            foreach (var entry in group.OrderBy(e => e.level))
            {
                string sourcePath = AssetDatabase.GetAssetPath(entry.levelData);
                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                var levelCopy = UnityEngine.Object.Instantiate(entry.levelData);
                levelCopy.name = entry.levelData.name;
                var serialized = new SerializedObject(levelCopy);
                var libraryProperty = serialized.FindProperty("obstacleLibrary");
                if (libraryProperty == null) throw new BuildFailedException("LevelData ObstacleLibrary alanı bulunamadı: " + entry.level);
                libraryProperty.objectReferenceValue = filteredLibrary;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                string levelOut = SaveOwnedAsset(levelCopy, folder + "/" + entry.levelData.name + ".asset");
                var generated = AssetDatabase.LoadAssetAtPath<LevelData>(levelOut);
                if (generated == null) throw new BuildFailedException("Filtrelenmiş level kopyası kaydedilemedi: " + levelOut);
                if (group.Key == 1)
                {
                    result.generatedLocalPaths.Add(levelOut.Replace('\\', '/'));
                    result.localSourcePaths.Add(sourcePath);
                    result.sourceGuidToLocalGeneratedPath[sourceGuid] = levelOut.Replace('\\', '/');
                }
                else
                {
                    levelOut = levelOut.Replace('\\', '/');
                    result.generatedPathToSourceGuid[levelOut] = sourceGuid;
                    result.generatedPathToSourcePath[levelOut] = sourcePath;
                    result.sourceGuidToGeneratedPath[sourceGuid] = levelOut;
                }
            }
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static void FilterLibrary(ObstacleLibrary copy, ISet<int> ids)
    {
        var serialized = new SerializedObject(copy);
        var list = serialized.FindProperty("obstacles");
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (!ids.Contains(list.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue))
                list.DeleteArrayElementAtIndex(i);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string SaveOwnedAsset<T>(T asset, string path) where T : UnityEngine.Object
    {
        path = path.Replace('\\', '/');
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            if (File.Exists(path)) throw new BuildFailedException("Generated asset path başka bir asset tarafından kullanılıyor: " + path);
            AssetDatabase.CreateAsset(asset, path);
            return path;
        }
        EditorUtility.CopySerialized(asset, existing);
        existing.name = asset.name;
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(asset);
        return path;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
