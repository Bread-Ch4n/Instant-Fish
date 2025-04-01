using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IAmFuture.Data;
using IAmFuture.Data.Items;
using IAmFuture.Data.StorageItems;
using IAmFuture.Gameplay.Character;
using IAmFuture.Gameplay.Fishing;
using IAmFuture.UserInterface.Gameplay.Fishing;
using UnityEngine;

namespace Instant_Fish;

public static class EnumerableExtensions
{
    public static T GetRandomOrDefault<T>(this IEnumerable<T> instance)
    {
        var enumerable = instance.ToList();
        var maxExclusive = enumerable.Count;
        return maxExclusive == 0 ? default : enumerable.ElementAt(Random.Range(0, maxExclusive));
    }
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static ManualLogSource _logger;

    private static Harmony _harmony;


    private void Awake()
    {
        _logger = Logger;
        _harmony = new Harmony("auto_fish");
        _harmony.PatchAll();
        _logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy() => _harmony.UnpatchSelf();

    [HarmonyPatch(typeof(GUI_Fishing))]
    private class AutoFishPatch
    {
        [HarmonyPatch(typeof(GUI_Fishing), "StartFishing")]
        private static bool Prefix(GUI_Fishing __instance)
        {
            // Used frequently below
            var fishingCharacterState =
                (FishingCharacterState)AccessTools.Field(typeof(GUI_Fishing), "fishingCharacterState")
                    .GetValue(__instance);

            var baitCatch = (BaitCatchPossibleRewards)AccessTools
                .Method(typeof(FishingCharacterState), "GetBaitCatch")
                .Invoke(fishingCharacterState, [FindObjectOfType<GUI_SelectedFishBait>().SelectedBait]);
            // ^

            // Register catch
            ((FishingService)AccessTools.Field(typeof(GUI_Fishing), "fishingService").GetValue(__instance))
                .RegisterCatch(baitCatch.ID);
            // ^

            // Drop item logic
            var instance = new List<BaitCatchLootStack>();

            if (baitCatch.UniqueSurpriseItemsRewards)
                instance.AddRange(baitCatch.PossibleCatch.Where(baitCatchLootStack =>
                    !((Statistics)AccessTools.Field(typeof(FishingCharacterState), "statistics")
                            .GetValue(fishingCharacterState)).LootStatistic.TotalPickedItems
                        .TryGetValue(baitCatchLootStack.ItemObject, out _)));
            else instance = baitCatch.PossibleCatch;

            if (instance.Count == 0)
            {
                _logger.LogError("[FishingCharacterState]: No items in the possible catch list!");
            }
            else
            {
                var catchLootInfo = instance.GetRandomOrDefault();
                var currentlyCaughtStack = new ItemStack(catchLootInfo.ItemObject,
                    Mathf.RoundToInt(Random.Range(catchLootInfo.MinItemsCount,
                        catchLootInfo.MaxItemsCount)));

                AccessTools.Field(typeof(FishingCharacterState), "currentlyCaughtStack")
                    .SetValue(fishingCharacterState, currentlyCaughtStack);
                AccessTools.Field(typeof(FishingCharacterState), "catchLootInfo")
                    .SetValue(fishingCharacterState, catchLootInfo);

                AccessTools.Method(typeof(FishingCharacterState), "RegisterCaughtEntity")
                    .Invoke(fishingCharacterState, [baitCatch]);

                AccessTools.Method(typeof(FishingCharacterState), "GetCaughtLoot").Invoke(fishingCharacterState, null);
            }
            // ^

            // Remove bait from inventory
            AccessTools.Method(typeof(GUI_SelectedFishBait), "RemoveBite")
                .Invoke(FindObjectOfType<GUI_SelectedFishBait>(), null);
            AccessTools.Method(typeof(GUI_SelectedFishBait), "UpdateBait")
                .Invoke(FindObjectOfType<GUI_SelectedFishBait>(), null);
            // ^

            return false;
        }
    }
}