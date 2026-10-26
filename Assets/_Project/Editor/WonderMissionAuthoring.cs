#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Editor-only authoring helpers. Never use WonderCatalog.Get here: that reads player order.
internal static class WonderMissionAuthoring
{
    internal const string LocalizationPath = "Assets/_Project/Resources/Localization/tinyfixers_localization.json";

    internal static int[] Costs(WonderDefinition draft, int total)
    {
        int n = draft.TaskCount, before = 0;
        long wideSum = 0;
        var result = new int[n];
        for (int i = 0; i < n; i++) wideSum += Mathf.Max(1, draft.GetStarCost(i));
        if (wideSum < 1 || wideSum > int.MaxValue) return result;
        int sum = (int)wideSum;
        for (int i = 0; i < n; i++)
        {
            int own = Mathf.Max(1, draft.GetStarCost(i));
            result[i] = Mathf.RoundToInt(total * (float)(before + own) / sum)
                      - Mathf.RoundToInt(total * (float)before / sum);
            before += own;
        }
        return result;
    }

    internal static int Owner(WonderDefinition draft, Vector2 p)
    {
        int owner = -1;
        float area = float.MaxValue;
        for (int t = 0; t < draft.tasks.Length; t++)
        {
            var task = draft.tasks[t];
            if (task == null || task.action == WonderTaskAction.Shine || task.regions == null) continue;
            foreach (var r in task.regions)
                if (r.Contains(p) && r.width * r.height < area)
                {
                    area = r.width * r.height;
                    owner = t;
                }
        }
        return owner;
    }

    // Geometric estimate only; the production shader adds slight noisy edges.
    internal static float[] Coverage(WonderDefinition draft, int resolution = 96)
    {
        var result = new float[draft.TaskCount + 1]; // last entry = unassigned environment
        float unit = 1f / (resolution * resolution);
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                int owner = Owner(draft, new Vector2((x + .5f) / resolution, (y + .5f) / resolution));
                result[owner < 0 ? draft.TaskCount : owner] += unit;
            }
        return result;
    }

    internal static List<string> Errors(WonderDefinition draft, string englishTitle, string[] englishTasks, WonderCatalog catalog)
    {
        var errors = new List<string>();
        if (draft == null) { errors.Add("Bir harika taslağı seçin."); return errors; }
        if (string.IsNullOrWhiteSpace(draft.wonderId) || draft.wonderId.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            errors.Add("Kimlik yalnız harf, rakam ve alt çizgi içermeli.");
        if (draft.backgroundSprite == null) errors.Add("Bir arka plan sprite'ı seçin.");
        if (string.IsNullOrWhiteSpace(draft.displayName) || string.IsNullOrWhiteSpace(englishTitle))
            errors.Add("Harika adını TR ve EN olarak girin.");
        if (draft.tasks == null || draft.tasks.Length == 0) errors.Add("En az bir görev ekleyin.");
        if (draft.tasks != null && Enumerable.Range(0, draft.tasks.Length).Sum(i => (long)Mathf.Max(1, draft.GetStarCost(i))) > int.MaxValue)
            errors.Add("Görev ağırlıklarının toplamı çok büyük.");
        int count = 0;
        bool anyRegion = false;
        if (draft.tasks != null)
            for (int t = 0; t < draft.tasks.Length; t++)
            {
                var task = draft.tasks[t];
                if (task == null) { errors.Add($"{t + 1}. görev boş."); continue; }
                if (string.IsNullOrWhiteSpace(task.name) || englishTasks == null || t >= englishTasks.Length || string.IsNullOrWhiteSpace(englishTasks[t]))
                    errors.Add($"{t + 1}. görev için TR ve EN adını girin.");
                if (draft.GetStarCost(t) < 1) errors.Add($"{t + 1}. görev ağırlığı en az 1 olmalı.");
                if (task.regions == null) continue;
                foreach (var r in task.regions)
                {
                    if (!Finite(r.x) || !Finite(r.y) || !Finite(r.width) || !Finite(r.height)
                        || r.width <= 0 || r.height <= 0 || r.xMin < 0 || r.yMin < 0 || r.xMax > 1.00001f || r.yMax > 1.00001f)
                        errors.Add($"{t + 1}. görevde bölge geçersiz; 0–1 aralığında ve pozitif boyutta olmalı.");
                    anyRegion = true;
                    if (task.action != WonderTaskAction.Shine) count++;
                }
            }
        if (!anyRegion) errors.Add("En az bir resim bölgesi çizin; aksi halde oyun eski otomatik parçalamaya döner.");
        if (count > WonderTaskRegions.MaxRects) errors.Add($"En fazla {WonderTaskRegions.MaxRects} fiziksel bölge kullanılabilir (şimdi {count}).");
        if (catalog == null) errors.Add("Gerçek maliyet kontrolü için bir WonderCatalog seçin.");
        else
        {
            if (catalog.firstWonderTotalStars < 1 || catalog.secondWonderTotalStars < 1 || catalog.lastWonderTotalStars < 1)
                errors.Add("Katalog toplamları pozitif olmalı.");
            if (draft.tasks != null && draft.tasks.Length > 0)
                for (int slot = 0; slot < Mathf.Max(1, catalog.Count); slot++)
                    if (Costs(draft, catalog.TotalStarsFor(slot)).Any(c => c < 1))
                    {
                        errors.Add($"{slot + 1}. sırada sıfır yıldızlı görev oluşuyor. Ağırlıkları veya toplamı düzenleyin.");
                        break;
                    }
            if (catalog.wonders != null)
                foreach (var other in catalog.wonders)
                {
                    if (other == null || other.wonderId == draft.wonderId || other.TaskCount < 1) continue;
                    for (int slot = 0; slot < Mathf.Max(1, catalog.Count); slot++)
                        if (Costs(other, catalog.TotalStarsFor(slot)).Any(c => c < 1))
                        {
                            errors.Add($"Katalog toplamları {other.wonderId} için {slot + 1}. sırada sıfır maliyet üretiyor.");
                            break;
                        }
                }
        }
        return errors;
    }

    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    internal static void WriteLocalization(string text)
    {
        // Hidden sibling keeps File.Replace atomic on both Windows and macOS, outside Unity import.
        string temp = Path.Combine(Path.GetDirectoryName(LocalizationPath), ".wonder-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Replace(temp, LocalizationPath, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static string ReadText(JObject json, string language, string key, string fallback)
    {
        var entries = Entries(json, language, false);
        var entry = entries?.OfType<JObject>().FirstOrDefault(e => (string)e["key"] == key);
        return (string)entry?["value"] ?? fallback;
    }

    static JArray Entries(JObject json, string language, bool create)
    {
        var languages = json["languages"] as JArray;
        if (languages == null) throw new InvalidDataException("Yerelleştirme dosyasında languages dizisi yok.");
        var item = languages.OfType<JObject>().FirstOrDefault(l => (string)l["code"] == language);
        if (item == null && create)
        {
            item = new JObject { ["code"] = language, ["entries"] = new JArray() };
            languages.Add(item);
        }
        if (item == null) return null;
        if (!(item["entries"] is JArray)) throw new InvalidDataException(language + " entries dizisi geçersiz.");
        return (JArray)item["entries"];
    }

    internal static string MergeLocalization(string current, WonderDefinition draft, string englishTitle, string[] englishTasks)
    {
        var json = JObject.Parse(current);
        foreach (string language in new[] { "tr", "en" })
        {
            var entries = Entries(json, language, true);
            SetText(entries, $"wonder_{draft.wonderId}_name", language == "tr" ? draft.displayName : englishTitle);
            for (int i = 0; i < draft.tasks.Length; i++)
                SetText(entries, $"wonder_{draft.wonderId}_task_{i + 1}", language == "tr" ? draft.tasks[i].name : englishTasks[i]);
        }
        // Preserve unknown fields, other languages and unrelated entries, including their order.
        string result = json.ToString(Formatting.Indented).Replace("\r\n", "\n");
        return current.EndsWith("\n", StringComparison.Ordinal) ? result + "\n" : result;
    }

    static void SetText(JArray entries, string key, string value)
    {
        var matches = entries.OfType<JObject>().Where(e => (string)e["key"] == key).ToArray();
        if (matches.Length > 1) throw new InvalidDataException("Tekrarlanan yerelleştirme anahtarı: " + key);
        if (matches.Length == 1) matches[0]["value"] = value;
        else entries.Add(new JObject { ["key"] = key, ["value"] = value });
    }
}
#endif
