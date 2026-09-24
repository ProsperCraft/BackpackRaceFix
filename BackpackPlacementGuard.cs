using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace BackpackRaceFix;

// Keep vanilla suitability, merge priorities, skipSlots and slot identities.
// Only the two enumerations inside placement selection filter unavailable slots.
internal static class BackpackPlacementGuard
{
    public static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(InventoryBase), nameof(InventoryBase.GetBestSuitedSlot),
            new[] { typeof(ItemSlot), typeof(ItemStackMoveOperation), typeof(List<ItemSlot>) }),
            transpiler: new HarmonyMethod(typeof(BackpackPlacementGuard), nameof(Transpile)));
    }

    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var enumerator = AccessTools.Method(typeof(InventoryBase), nameof(InventoryBase.GetEnumerator));
        var replacement = AccessTools.Method(typeof(BackpackPlacementGuard), nameof(PlacementSlots));
        var calls = code.Where(instruction => instruction.Calls(enumerator)).ToArray();
        if (calls.Length != 2)
            throw new InvalidOperationException("Unsupported GetBestSuitedSlot: expected two inventory enumerations.");
        foreach (var call in calls)
        {
            // Modify in place to retain branch labels and exception-block markers.
            call.opcode = OpCodes.Call;
            call.operand = replacement;
        }
        return code;
    }

    public static IEnumerator<ItemSlot> PlacementSlots(InventoryBase inventory)
    {
        var slots = inventory.GetEnumerator();
        return AvailableSlots(inventory, slots);
    }

    private static IEnumerator<ItemSlot> AvailableSlots(InventoryBase inventory, IEnumerator<ItemSlot> slots)
    {
        using (slots)
        {
            while (slots.MoveNext())
            {
                var slot = slots.Current;
                if (slot != null) yield return slot;
                else HotfixDiagnostics.Report("backpack-placement-null",
                    $"Skipped an unavailable slot while choosing item placement; inventory={inventory.InventoryID}; type={inventory.GetType().FullName}. Valid slot references and placement rules retained.", null);
            }
        }
    }
}
