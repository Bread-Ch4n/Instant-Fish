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
// using Random = System.Random;

namespace Instant_Fish;

public static class IEnumerableExtensions
{
    public static T GetRandomOrDefault<T>(this IEnumerable<T> instance)
    {
        int maxExclusive = instance.Count();
        if (maxExclusive == 0)
            return default(T);
        int index = Random.Range(0, maxExclusive); // Assumes Unity's Random.Range is used
        return instance.ElementAt(index);
    }
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static ManualLogSource _logger;

    // private static T GetRandomOrDefault<T>(IList<T> list)
    // {
    //     if (list == null || list.Count == 0)
    //         return default;
    //
    //     int index = new Random().Next(list.Count);
    //     return list[index];
    // }

    [HarmonyPatch(typeof(GUI_Fishing))]
    class AutoFishPatch
    {
        [HarmonyPatch(typeof(GUI_Fishing), "StartFishing")]
        static bool Prefix(GUI_Fishing __instance)
        {
            // Used frequently below
            FishingCharacterState fishingCharacterState =
                (FishingCharacterState)AccessTools.Field(typeof(GUI_Fishing), "fishingCharacterState")
                    .GetValue(__instance);

            BaitCatchPossibleRewards baitCatch = (BaitCatchPossibleRewards)AccessTools
                .Method(typeof(FishingCharacterState), "GetBaitCatch")
                .Invoke(fishingCharacterState, new object[] { FindObjectOfType<GUI_SelectedFishBait>().SelectedBait });
            // ^

            // Register catch
            ((FishingService)AccessTools.Field(typeof(GUI_Fishing), "fishingService").GetValue(__instance))
                .RegisterCatch(baitCatch.ID);
            // ^

            // Drop item logic
            List<BaitCatchLootStack> instance = new List<BaitCatchLootStack>();

            if (baitCatch.UniqueSurpriseItemsRewards)
                instance.AddRange(baitCatch.PossibleCatch.Where(baitCatchLootStack =>
                    !((Statistics)AccessTools.Field(typeof(FishingCharacterState), "statistics")
                        .GetValue(fishingCharacterState)).LootStatistic.TotalPickedItems.TryGetValue(baitCatchLootStack.ItemObject, out _)));
            else instance = baitCatch.PossibleCatch;

            if (instance.Count == 0) _logger.LogError("[FishingCharacterState]: No items in the possible catch list!");
            else
            {
                AccessTools.Method(typeof(FishingCharacterState), "RegisterCaughtEntity")
                    .Invoke(fishingCharacterState, new object[] { baitCatch });

                BaitCatchLootStack catchLootInfo = instance.GetRandomOrDefault();
                ItemStack currentlyCaughtStack = new ItemStack(catchLootInfo.ItemObject,
                    Mathf.RoundToInt(UnityEngine.Random.Range(catchLootInfo.MinItemsCount,
                        catchLootInfo.MaxItemsCount)));

                AccessTools.Field(typeof(FishingCharacterState), "currentlyCaughtStack").SetValue(fishingCharacterState, currentlyCaughtStack);
                AccessTools.Field(typeof(FishingCharacterState), "catchLootInfo").SetValue(fishingCharacterState, catchLootInfo);

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


    private void Awake()
    {
        _logger = Logger;
        _logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        var h = new Harmony("auto_fish");
        h.PatchAll();
    }
}