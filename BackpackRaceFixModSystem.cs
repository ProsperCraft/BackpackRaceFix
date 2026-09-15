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

    private static Exception? BagGetterFinalizer(Exception? __exception, ref ItemSlot? __result)
    {
        if (__exception is not ArgumentOutOfRangeException) return __exception;

        // The outer backpack getter (including XSkills' extra-slot mapping) stays
        // intact. Only suppress a stale index into the live inner bag list.
        __result = null;
        return null;
    }

    private static Exception? ThirstInventoryScanFinalizer(Exception? __exception)
    {
        // Hydrate-or-Diedrate scans the backpack periodically for liquid weight.
        // A synchronized bag resize can invalidate that scan; defer it one tick.
        return __exception is ArgumentOutOfRangeException ? null : __exception;
    }

    private static Exception? GuiComposerPostRenderFinalizer(Exception? __exception)
    {
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

        return setToolMode == null || Harmony.GetPatchInfo(setToolMode)?.Owners.Contains(harmonyId) != true;
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }
}
