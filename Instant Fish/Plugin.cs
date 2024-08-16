using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IAmFuture.Data;
using IAmFuture.Data.Character;
using IAmFuture.Data.Items;
using IAmFuture.Data.StorageItems;
using IAmFuture.Gameplay.Character;
using IAmFuture.Gameplay.Fishing;
using IAmFuture.Gameplay.LootSystem;
using IAmFuture.UserInterface.Gameplay.Fishing;
using UnityEngine;
using Random = System.Random;

namespace Instant_Fish;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static ManualLogSource _logger;

    private static T GetRandomOrDefault<T>(IList<T> list)
    {
        if (list == null || list.Count == 0)
            return default;

        int index = new Random().Next(list.Count);
        return list[index];
    }


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
                            .GetValue(fishingCharacterState)).LootStatistic.TotalPickedItems
                        .TryGetValue(baitCatchLootStack.ItemObject, out _)));
            else instance = baitCatch.PossibleCatch;

            if (instance.Count == 0) _logger.LogError("[FishingCharacterState]: No items in the possible catch list!");
            else
            {
                AccessTools.Method(typeof(FishingCharacterState), "RegisterCaughtEntity")
                    .Invoke(fishingCharacterState, new object[] { baitCatch });

                BaitCatchLootStack catchLootInfo = GetRandomOrDefault(instance);
                ItemStack currentlyCaughtStack = new ItemStack(catchLootInfo.ItemObject,
                    Mathf.RoundToInt(UnityEngine.Random.Range(catchLootInfo.MinItemsCount,
                        catchLootInfo.MaxItemsCount)));

                if (catchLootInfo.IsDroppedOnGround)
                    ((ILootDropService)AccessTools
                            .Field(typeof(FishingCharacterState), "lootDropService").GetValue(fishingCharacterState))
                        .DropItemStackTweened(currentlyCaughtStack,
                            (Vector3)AccessTools
                                .Field(typeof(FishingCharacterState), "positionToDropHugeItems")
                                .GetValue(fishingCharacterState));
                else
                {
                    ((CharacterInventory)AccessTools
                            .Field(typeof(FishingCharacterState), "inventory").GetValue(fishingCharacterState))
                        .TryToAdd(currentlyCaughtStack.Object, currentlyCaughtStack.Count, out var remainder);
                    if (remainder > 0)
                        ((LootFactory)AccessTools.Field(typeof(FishingCharacterState), "lootFactory")
                            .GetValue(fishingCharacterState)).Create(currentlyCaughtStack.Object, remainder,
                            ((Transform)AccessTools.Field(typeof(FishingCharacterState), "playerTransform")
                                .GetValue(fishingCharacterState)).position);
                }
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