using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace BackpackRaceFix;

public sealed class BackpackRaceFixModSystem : ModSystem
{
    private const string HarmonyId = "com.prospercraft.backpackracefix";
    private Harmony? harmony;

    public override void StartPre(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Client || Harmony.HasAnyPatches(HarmonyId)) return;

        harmony = new Harmony(HarmonyId);
        HotfixDiagnostics.Start(api.Logger);
        BackpackGridGuard.Install(harmony);
        BackpackPlacementGuard.Install(harmony);
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(BagInventory), nameof(BagInventory.ReloadBagInventory)),
            prefix: new HarmonyMethod(typeof(HotfixDiagnostics), nameof(HotfixDiagnostics.ReloadPrefix)),
            finalizer: new HarmonyMethod(typeof(HotfixDiagnostics), nameof(HotfixDiagnostics.ReloadFinalizer))
        );
        var bagGetterFinalizer = new HarmonyMethod(
            typeof(BackpackRaceFixModSystem),
            nameof(BagGetterFinalizer)
        )
        {
            priority = Priority.Last
        };
        harmony.Patch(
            AccessTools.DeclaredPropertyGetter(typeof(BagInventory), "Item"),
            finalizer: bagGetterFinalizer
        );

        var liquidEncumbranceType = AccessTools.TypeByName(
            "HydrateOrDiedrate.encumbrance.EntityBehaviorLiquidEncumbrance"
        );
        var checkInventoryForEncumbrance = AccessTools.DeclaredMethod(
            liquidEncumbranceType,
            "CheckInventoryForEncumbrance"
        );
        if (checkInventoryForEncumbrance != null)
        {
            var thirstScanFinalizer = new HarmonyMethod(
                typeof(BackpackRaceFixModSystem),
                nameof(ThirstInventoryScanFinalizer)
            )
            {
                priority = Priority.Last
            };
            harmony.Patch(checkInventoryForEncumbrance, finalizer: thirstScanFinalizer);
        }

        var guiPostRenderFinalizer = new HarmonyMethod(
            typeof(BackpackRaceFixModSystem),
            nameof(GuiComposerPostRenderFinalizer)
        )
        {
            priority = Priority.Last
        };
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(Vintagestory.API.Client.GuiComposer), "PostRender"),
            finalizer: guiPostRenderFinalizer
        );

        var vintageRiftHarmonySystem = AccessTools.TypeByName("VintageRift.Modules.HarmonyModSystem");
        var vintageRiftStart = AccessTools.DeclaredMethod(vintageRiftHarmonySystem, "Start");
        if (vintageRiftStart != null)
        {
            harmony.Patch(
                vintageRiftStart,
                prefix: new HarmonyMethod(typeof(BackpackRaceFixModSystem), nameof(VintageRiftStartPrefix))
            );
        }
    }

    private static Exception? BagGetterFinalizer(BagInventory __instance, int slotId, Exception? __exception, ref ItemSlot? __result)
    {
        if (__exception is not ArgumentOutOfRangeException) return __exception;
        HotfixDiagnostics.BagFailure(__instance, slotId, __exception);

        // The outer backpack getter (including XSkills' extra-slot mapping) stays
        // intact. Only suppress a stale index into the live inner bag list.
        __result = null;
        return null;
    }

    private static Exception? ThirstInventoryScanFinalizer(Exception? __exception)
    {
        if (__exception is ArgumentOutOfRangeException)
            HotfixDiagnostics.Report("thirst-scan", "Suppressed encumbrance scan failure", __exception);
        // Hydrate-or-Diedrate scans the backpack periodically for liquid weight.
        // A synchronized bag resize can invalidate that scan; defer it one tick.
        return __exception is ArgumentOutOfRangeException ? null : __exception;
    }

    private static Exception? GuiComposerPostRenderFinalizer(Exception? __exception)
    {
        if (__exception is NullReferenceException)
            HotfixDiagnostics.Report("gui-postrender", "Suppressed GUI PostRender failure; cause unconfirmed", __exception);
        // A composer or one of its interactive elements can be torn down while
        // the login/reconnect frame is still finalizing. Dropping that one frame
        // is safer than terminating the client; unrelated exceptions still flow.
        return __exception is NullReferenceException ? null : __exception;
    }

    private static bool VintageRiftStartPrefix(object __instance)
    {
        var harmonyId = AccessTools.Property(__instance.GetType(), "HarmonyId")?.GetValue(__instance) as string;
        if (harmonyId != "vintagerift_miscpatches") return true;

        MethodBase? setToolMode = AccessTools.Method(
            "TabletopGames.CollectibleBehaviorChiseledPieceToolModes:SetToolMode"
        );

        bool runStart = setToolMode == null || Harmony.GetPatchInfo(setToolMode)?.Owners.Contains(harmonyId) != true;
        if (!runStart) HotfixDiagnostics.Report("rift-start", "Skipped duplicate Vintage Rift Start", null);
        return runStart;
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }
}
