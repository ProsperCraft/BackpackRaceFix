using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace BackpackRaceFix;

// Only changes the display dictionaries. Never changes inventory Count, slot IDs,
// item stacks, or the XSkills indexer mapping.
internal static class BackpackGridGuard
{
    private sealed class State
    {
        public bool Missing;
        public int[]? ComposedKeys;
    }
    private static readonly ConditionalWeakTable<GuiElementItemSlotGridBase, State> States = new();
    private static readonly System.Reflection.MethodInfo Init = AccessTools.Method(typeof(GuiElementItemSlotGridExcl), "InitDicts");

    public static void Install(Harmony harmony)
    {
        harmony.Patch(Init, postfix: new HarmonyMethod(typeof(BackpackGridGuard), nameof(FilterMissingSlots)));
        harmony.Patch(AccessTools.Method(typeof(GuiElementItemSlotGridBase), "ComposeInteractiveElements"),
            postfix: new HarmonyMethod(typeof(BackpackGridGuard), nameof(Composed)));
        harmony.Patch(AccessTools.Method(typeof(GuiElementItemSlotGridBase), "PostRenderInteractiveElements"),
            prefix: new HarmonyMethod(typeof(BackpackGridGuard), nameof(BeforePostRender)));
        harmony.Patch(AccessTools.Method(typeof(GuiElementItemSlotGridBase), "ComposeSlotOverlays"),
            prefix: new HarmonyMethod(typeof(BackpackGridGuard), nameof(BeforeOverlay)));
    }

    public static void FilterMissingSlots(GuiElementItemSlotGridExcl __instance, IInventory ___inventory)
    {
        if (___inventory is not InventoryPlayerBackpacks) return;
        var missing = __instance.availableSlots.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray();
        States.GetOrCreateValue(__instance).Missing = missing.Length != 0;
        foreach (int id in missing)
        {
            __instance.availableSlots.Remove(id);
            __instance.renderedSlots.Remove(id);
        }
        if (missing.Length != 0)
            HotfixDiagnostics.Report("backpack-grid-null", $"Excluded unavailable GUI slot IDs [{string.Join(",", missing)}]; inventory count={___inventory.Count}. Real slot IDs preserved; retrying on later frames.", null);
    }

    public static void Composed(GuiElementItemSlotGridBase __instance, IInventory ___inventory)
    {
        if (___inventory is InventoryPlayerBackpacks && __instance is GuiElementItemSlotGridExcl)
            States.GetOrCreateValue(__instance).ComposedKeys = __instance.availableSlots.Keys.ToArray();
    }

    public static bool BeforePostRender(GuiElementItemSlotGridBase __instance, IInventory ___inventory)
    {
        if (___inventory is not InventoryPlayerBackpacks || __instance is not GuiElementItemSlotGridExcl) return true;
        var state = States.GetOrCreateValue(__instance);
        // Retry missing slots even if the server did not send a second dirty-slot notification.
        if (state.Missing) Init.Invoke(__instance, null);
        if (state.ComposedKeys == null || !state.ComposedKeys.SequenceEqual(__instance.availableSlots.Keys))
        {
            // Adding/removing even one GUI slot shifts positional texture/bounds arrays.
            // Recompose before overlays; do not merely insert a recovered slot into the dict.
            __instance.ComposeElements(null, null);
            return false;
        }
        return true;
    }

    public static bool BeforeOverlay(IInventory ___inventory, ItemSlot? slot, ref bool __result)
    {
        if (___inventory is not InventoryPlayerBackpacks || slot != null) return true;
        // A later indexer read may again be unavailable. Leave it dirty for a retry.
        __result = false;
        return false;
    }
}
