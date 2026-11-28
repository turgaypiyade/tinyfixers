using System;
using UnityEngine;

/// <summary>Read live configs each planning pass; no unlock snapshot is baked into a package.</summary>
public static class EventContentPolicy
{
    public const string Safari = "safari", Bridge = "bridge-repair", Harvest = "harvest",
        Gift = "gift-festival", Gear = "gear-cup", Claw = "claw-machine", MissionMap = "mission-map";
    private static SafariConfig safari;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => safari = null;

    public static string WindowKey(string id)
    {
        var now = DateTime.UtcNow;
        switch (id)
        {
            case MissionMap: return "mission-map"; // On-demand only; no level/schedule prefetch.
            case Safari:
                if (safari == null) safari = Resources.Load<SafariConfig>("Events/SafariConfig");
                return SafariSchedule.GetCycleKey(safari, now);
            case Bridge: return BridgeRepairSchedule.GetCycleKey(BridgeRepairConfig.Shared, now);
            case Harvest: return HarvestSchedule.GetCycleKey(HarvestConfig.Shared, now);
            case Gift: return GiftFestivalState.Window(GiftFestivalConfig.Shared, now).Start.Ticks.ToString();
            case Claw: return ClawMachineState.Window(ClawMachineConfig.Shared, now).Start.Ticks.ToString();
            case Gear:
                return GearCupState.HasPendingResult ? GearCupState.Data.season
                    : GearCupRules.SeasonKey(GearCupRules.SeasonStart(now, GearCupPreviewConfig.Shared.seasonHours));
            default: return "";
        }
    }

    public static bool Eligible(string id, int lookahead, bool pendingResult = false)
    {
        var now = DateTime.UtcNow;
        long level = (long)CurrentLevel.Global + Math.Max(0, lookahead);
        switch (id)
        {
            case Safari:
                if (safari == null) safari = Resources.Load<SafariConfig>("Events/SafariConfig");
                return safari != null && level >= safari.minLevelGate && SafariSchedule.IsActiveNow(safari, now);
            case Bridge:
                var bridge = BridgeRepairConfig.Shared;
                return bridge != null && level >= bridge.minLevelGate && BridgeRepairSchedule.IsActiveNow(bridge, now);
            case Harvest:
                var harvest = HarvestConfig.Shared;
                return harvest != null && level >= harvest.minLevelGate && HarvestSchedule.IsActiveNow(harvest, now);
            case Gift:
                var gift = GiftFestivalConfig.Shared;
                // Before first unlock no player cycle exists: cache art within the lookahead, without creating a cycle.
                return gift != null && level > gift.unlockAfterLevel
                    && (!GiftFestivalState.IsUnlocked(gift) || GiftFestivalState.Window(gift, now).Active);
            case Claw:
                var claw = ClawMachineConfig.Shared;
                // Gift kalıbı: ilk açılıştan önce döngü yok; ileriye bakışta paketi önceden indir, döngü yaratma.
                return claw != null && level >= claw.unlockLevel
                    && (!ClawMachineState.IsUnlocked(claw) || ClawMachineState.Window(claw, now).Active);
            case Gear:
                var gear = GearCupPreviewConfig.Shared;
                return gear != null && gear.eventEnabled && level >= gear.minLevelGate
                    && (gear.seasonHours > 0 || (pendingResult && GearCupState.HasPendingResult));
            default: return false;
        }
    }
}
