using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Bridge Repair test kısayolları (yalnız editör). Takvimsiz test için BridgeRepairConfig.debugForceAvailable açılmalı.
public static class BridgeRepairDebugMenu
{
    private const string Root = "TinyFixers/Debug/Bridge Repair/";

    [MenuItem(Root + "Kazanç Ekle (+1 level)")]
    private static void AddWin()
    {
        BridgeRepairState.DebugAddWin();
        Debug.Log($"[BridgeRepairDebug] Köprü: {BridgeRepairState.Wins} (sıra {BridgeRepairState.FinalRank})");
    }

    [MenuItem(Root + "Zamanı 3 Saat İlerlet (botlar)")]
    private static void ShiftTime()
    {
        BridgeRepairState.DebugShiftJoin(TimeSpan.FromHours(3));
        Debug.Log("[BridgeRepairDebug] Katılım 3 saat geri alındı → botlar ilerledi. Haritayı açınca oynatılır.");
    }

    [MenuItem(Root + "Yarış Ekranını Aç (Play)")]
    private static void OpenMap()
    {
        var ctrl = UnityEngine.Object.FindFirstObjectByType<BridgeRepairController>(FindObjectsInactive.Include);
        if (ctrl == null) { Debug.LogWarning("[BridgeRepairDebug] Sahnede BridgeRepairController yok."); return; }
        ctrl.OnIconClicked();
    }

    [MenuItem(Root + "Yarış Ekranını Aç (Play)", true)]
    private static bool OpenMapValidate() => Application.isPlaying;

    [MenuItem(Root + "Efekt Durumunu Kontrol Et (Play)")]
    private static void CheckVisuals()
    {
        var map = UnityEngine.Object.FindFirstObjectByType<BridgeRepairMapScreen>(FindObjectsInactive.Include);
        if (map == null || !map.IsOpen)
        {
            Debug.LogWarning("[BridgeRepairVisuals] Önce yarış ekranını aç.");
            return;
        }
        map.StartCoroutine(ProbeVisuals(map));
    }

    [MenuItem(Root + "Efekt Durumunu Kontrol Et (Play)", true)]
    private static bool CheckVisualsValidate() => Application.isPlaying;

    private static IEnumerator ProbeVisuals(BridgeRepairMapScreen map)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var image = typeof(BridgeRepairMapScreen).GetField("boardImage", flags)?.GetValue(map) as Image;
        var motion = typeof(BridgeRepairMapScreen).GetField("screenMotion", flags)?.GetValue(map);
        if (image == null) { Debug.LogError("[BridgeRepairVisuals] Board Image yok."); yield break; }
        var material = image.canvasRenderer.GetMaterial();
        bool river = material != null && material.HasProperty("_FlowClock");
        float before = river ? material.GetFloat("_FlowClock") : -1f;
        Debug.Log($"[BridgeRepairVisuals] Screen={map.gameObject.scene.name}/{map.name}; " +
            $"Image={image.name}; Sprite={image.overrideSprite?.name}; " +
            $"RenderedShader={material?.shader?.name}; Mask={ (river ? material.GetTexture("_WaterMask")?.name : "none") }; " +
            $"MotionCreated={motion != null}; EntranceComplete={(motion as BridgeRepairScreenMotion)?.EntranceComplete}; " +
            $"Clock={before:F3}", map);
        yield return new WaitForSecondsRealtime(1f);
        if (image == null || map == null || !map.IsOpen) yield break;
        material = image.canvasRenderer.GetMaterial();
        float after = material != null && material.HasProperty("_FlowClock") ? material.GetFloat("_FlowClock") : -1f;
        Debug.Log($"[BridgeRepairVisuals] Rendered clock {before:F3} → {after:F3}; " +
            $"Advancing={after != before}; BoardScale={image.rectTransform.localScale}", map);
    }

    [MenuItem(Root + "Nasıl Oynanır Göster (Play)")]
    private static void ShowHowTo()
    {
        var ctrl = UnityEngine.Object.FindFirstObjectByType<BridgeRepairController>(FindObjectsInactive.Include);
        if (ctrl != null) ctrl.ShowHowToPlay(null);
    }

    [MenuItem(Root + "Nasıl Oynanır Göster (Play)", true)]
    private static bool ShowHowToValidate() => Application.isPlaying;

    [MenuItem(Root + "Kazanma Ekranı: 1. (Play)")] private static void Win1() => PreviewVictory(1);
    [MenuItem(Root + "Kazanma Ekranı: 2. (Play)")] private static void Win2() => PreviewVictory(2);
    [MenuItem(Root + "Kazanma Ekranı: 3. (Play)")] private static void Win3() => PreviewVictory(3);
    [MenuItem(Root + "Kazanma Ekranı: 1. (Play)", true)] private static bool Win1V() => Application.isPlaying;
    [MenuItem(Root + "Kazanma Ekranı: 2. (Play)", true)] private static bool Win2V() => Application.isPlaying;
    [MenuItem(Root + "Kazanma Ekranı: 3. (Play)", true)] private static bool Win3V() => Application.isPlaying;

    // Ödül VERMEZ (yalnız gösterim) — tasarım önizlemesi.
    private static void PreviewVictory(int rank)
    {
        var ctrl = UnityEngine.Object.FindFirstObjectByType<BridgeRepairController>(FindObjectsInactive.Include);
        if (ctrl == null) { Debug.LogWarning("[BridgeRepairDebug] Sahnede BridgeRepairController yok."); return; }
        ctrl.ShowVictory(rank);
    }

    [MenuItem(Root + "Sıfırla (tüm durum)")]
    private static void Clear()
    {
        BridgeRepairState.DebugClearAll();
        Debug.Log("[BridgeRepairDebug] Bridge Repair durumu temizlendi.");
    }

    [MenuItem(Root + "Takvimi Yazdır (4 hafta)")]
    private static void PrintSchedule()
    {
        var config = BridgeRepairConfig.Shared;
        if (config == null) { Debug.LogWarning("[BridgeRepairDebug] Config yok."); return; }
        var sb = new System.Text.StringBuilder("[BridgeRepairDebug] Etkinlik günleri (UTC):\n");
        for (int w = 0; w < 4; w++)
        {
            foreach (var day in BridgeRepairSchedule.GetWeekDays(config, DateTime.UtcNow.AddDays(7 * w)))
                sb.AppendLine($"  {day:yyyy-MM-dd ddd}");
        }
        Debug.Log(sb.ToString());
    }
}
