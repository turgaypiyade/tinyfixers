#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Builds an auditable image inventory from the authored LevelV1 catalog entries.
/// It intentionally reads the levels themselves; the complete ObstacleLibrary is only a lookup table.</summary>
internal static class ProgressiveLevelArtCatalog
{
    private const string CatalogPath = "Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset";
    private const string OutputPath = "Docs/Backend/First-50-Level-Asset-Catalog.md";

    private sealed class RangeInfo
    {
        public int first, last;
        public readonly SortedSet<string> ids = new(StringComparer.Ordinal);
        public readonly SortedSet<string> images = new(StringComparer.Ordinal);
        public readonly SortedDictionary<int, string> levels = new();
    }

    [MenuItem("TinyFixers/Yayın/LevelV1 Görsel Kataloğunu Üret")]
    private static void Generate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("LevelCatalogProV1 bulunamadı.");
        var ranges = new SortedDictionary<int, RangeInfo>();
        var libraryCache = new Dictionary<ObstacleLibrary, Dictionary<int, SortedSet<string>>>();

        foreach (var entry in catalog.entries.OrderBy(e => e.level))
        {
            if (entry == null || entry.levelData == null)
                throw new InvalidOperationException("LevelV1 kaynağı eksik: " + (entry?.level ?? 0));
            int first = entry.level <= 50 ? 1 : ((entry.level - 1) / 50) * 50 + 1;
            int last = entry.level <= 50 ? 50 : Math.Min(first + 49, catalog.MaximumGlobalLevel);
            if (!ranges.TryGetValue(first, out var range))
                ranges[first] = range = new RangeInfo { first = first, last = last };

            var ids = ReadObstacleIds(entry.levelData);
            range.levels[entry.level] = string.Join(", ", ids.Select(id => Enum.GetName(typeof(ObstacleId), id) ?? id.ToString()));
            foreach (int id in ids)
            {
                range.ids.Add(Enum.GetName(typeof(ObstacleId), id) ?? id.ToString());
                var library = entry.levelData.obstacleLibrary;
                if (library == null) continue;
                if (!libraryCache.TryGetValue(library, out var definitions))
                    libraryCache[library] = definitions = ReadDefinitions(library);
                if (!definitions.TryGetValue(id, out var images)) continue;
                foreach (var image in images) range.images.Add(image);
            }

            // Authored goal icon overrides are level-specific images, not library art.
            foreach (var image in ReadSprites(entry.levelData)) range.images.Add(image);
        }

        var text = new StringBuilder();
        text.AppendLine("# LevelV1 Görsel Kataloğu");
        text.AppendLine();
        text.AppendLine("Kaynak: `LevelCatalogProV1.asset` içindeki gerçek `LevelV1_*` kayıtları. Her seviye için tahtada/hedefte tanımlı ObstacleId'ler okunur; görsel listesi yalnız bu ID'lerin ObstacleLibrary sprite alanlarından ve level'a özel Sprite referanslarından çıkarılır.");
        text.AppendLine();
        text.AppendLine("Bu dosya otomatik üretilebilir envanterdir. Paketlerin gerçekten bu listelere göre ayrıldığı anlamına gelmez; build çıktısı ayrıca kontrol edilmelidir.");
        text.AppendLine();
        foreach (var range in ranges.Values)
        {
            text.Append("## Levels ").Append(range.first).Append('–').Append(range.last).AppendLine();
            text.AppendLine();
            text.Append("ObstacleId seti (").Append(range.ids.Count).AppendLine("):");
            text.AppendLine();
            text.AppendLine(string.Join(", ", range.ids));
            text.AppendLine();
            text.Append("Benzersiz image asset'i: ").Append(range.images.Count).AppendLine();
            text.AppendLine();
            foreach (string image in range.images) text.Append("- `").Append(image).AppendLine("`");
            text.AppendLine();
            text.AppendLine("Seviye → kullanılan ObstacleId'ler:");
            text.AppendLine();
            foreach (var level in range.levels)
                text.Append("- ").Append(level.Key).Append(": ").AppendLine(string.IsNullOrEmpty(level.Value) ? "yok" : level.Value);
            text.AppendLine();
        }

        string fullPath = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, text.ToString());
        AssetDatabase.Refresh();
        Debug.Log("LevelV1 görsel kataloğu üretildi: " + OutputPath);
    }

    internal static SortedSet<int> ReadObstacleIds(LevelData level)
    {
        var result = new SortedSet<int>();
        void Include(ObstacleId id)
        {
            if (id != ObstacleId.None) result.Add((int)id);
        }
        if (level.obstacles != null)
            foreach (int id in level.obstacles) if (id > 0) result.Add(id);
        if (level.stackedObstacles != null)
            foreach (var item in level.stackedObstacles) Include(item.obstacleId);
        if (level.dimerdals != null)
            foreach (var item in level.dimerdals) Include(item.obstacleId);
        if (level.goals != null)
            foreach (var goal in level.goals)
            {
                if (goal == null) continue;
                if (goal.targetType == LevelGoalTargetType.Obstacle) Include(goal.obstacleId);
                if (goal.targetType == LevelGoalTargetType.Collectible)
                    Include(CollectibleSources.ProducerOf(goal.collectibleId));
            }
        if (level.levelKind == LevelKind.BossDuel)
        {
            if (level.bossThrownObstacles?.Length > 0)
                foreach (var id in level.bossThrownObstacles) Include(id);
            else if (level.bossAttackOilCount > 0 || (level.bossWaves?.Any(w => w != null && w.oilCount > 0) ?? false))
                Include(ObstacleId.Oil);
            if (level.bossToolThreatEnabled)
            {
                if (level.bossToolThreatTypes?.Length > 0)
                    foreach (var id in level.bossToolThreatTypes) Include(id);
                else Include(ObstacleId.ToolThreat);
            }
        }
        if (level.fruits?.Length > 0) Include(ObstacleId.FlyFruit);
        if (level.waterChannels?.Length > 0) Include(ObstacleId.WaterChannel);
        if (level.tubes?.Length > 0) Include(ObstacleId.Tube);
        if (level.magnets?.Length > 0) Include(ObstacleId.Magnet);
        if (level.safes?.Length > 0) Include(ObstacleId.Safe);
        if (level.ancientSeals?.Length > 0) Include(ObstacleId.AncientSeal);
        if (level.tarpStatues?.Length > 0) Include(ObstacleId.TarpStatue);
        var pending = new Queue<ObstacleId>(result.Select(id => (ObstacleId)id));
        void Require(ObstacleId id)
        {
            if (result.Add((int)id)) pending.Enqueue(id);
        }
        while (pending.Count > 0)
        {
            var producer = pending.Dequeue();
            if (DimerdalRules.IsShutter(producer)) Require(DimerdalRules.DiamondFor(producer));
            switch (producer)
            {
                case ObstacleId.Windmill:
                    Require(ObstacleId.FlourSack);
                    Require(ObstacleId.FlourSackV);
                    break;
                case ObstacleId.FlourSack:
                case ObstacleId.FlourSackV: Require(ObstacleId.Flour); break;
                case ObstacleId.FlyEggBox: Require(ObstacleId.FlyEgg); break;
                case ObstacleId.GrassWaterLily:
                case ObstacleId.GrassFlower: Require(ObstacleId.Grass); break;
                case ObstacleId.Barrel:
                case ObstacleId.Barrell_v2: Require(ObstacleId.Mud); break;
                case ObstacleId.WaterTank:
                case ObstacleId.WaterTankSmall: Require(ObstacleId.WaterPuddle); break;
                case ObstacleId.GelLauncherUp:
                case ObstacleId.GelLauncherDown:
                case ObstacleId.GelLauncherLeft:
                case ObstacleId.GelLauncherRight: Require(ObstacleId.SpreadingGel); break;
            }
        }
        return result;
    }

    private static Dictionary<int, SortedSet<string>> ReadDefinitions(ObstacleLibrary library)
    {
        var result = new Dictionary<int, SortedSet<string>>();
        var serialized = new SerializedObject(library);
        var list = serialized.FindProperty("obstacles");
        if (list == null || !list.isArray) return result;
        for (int i = 0; i < list.arraySize; i++)
        {
            var item = list.GetArrayElementAtIndex(i);
            int id = item.FindPropertyRelative("id").intValue;
            if (result.ContainsKey(id)) continue;
            var images = new SortedSet<string>(StringComparer.Ordinal);
            var child = item.Copy();
            var end = child.GetEndProperty();
            bool enter = true;
            while (child.Next(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = child.hasVisibleChildren;
                if (child.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = child.objectReferenceValue;
                if (value is not Sprite && value is not Texture2D) continue;
                string path = AssetDatabase.GetAssetPath(value);
                if (!string.IsNullOrEmpty(path)) images.Add(path);
            }
            result.Add(id, images);
        }
        return result;
    }

    private static IEnumerable<string> ReadSprites(UnityEngine.Object source)
    {
        var serialized = new SerializedObject(source);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            var value = property.objectReferenceValue;
            if (value is not Sprite && value is not Texture2D) continue;
            string path = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(path)) yield return path;
        }
    }
}
#endif
