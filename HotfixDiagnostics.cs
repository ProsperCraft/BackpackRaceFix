using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace BackpackRaceFix;

// Observational diagnostics only. Inventory contents and player identities are
// never logged. Diagnostic failures must not replace the original game exception.
internal static class HotfixDiagnostics
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<BagInventory, ReloadHistory> Histories = new();
    private static readonly Dictionary<string, (long Last, long Total)> Reports = new();
    private static ILogger? logger;
    private static int startupThread;
    private static long sequence;

    internal sealed class ReloadHistory
    {
        public readonly Dictionary<long, ReloadCall> Active = new();
        public string LastCompleted = "none";
    }

    internal sealed record ReloadCall(long Id, int Thread, int Before, long Started);

    public static void Start(ILogger log)
    {
        lock (Gate)
        {
            logger = log;
            startupThread = Environment.CurrentManagedThreadId;
            Reports.Clear();
        }
        Report("startup", "Hotfix 1.1.3 active, including backpack GUI and item-placement null-slot guards. startupThread is a reference, not proof of main-thread identity. Rate limit: one report per category per 30 seconds; totals include suppressed reports.", null);
    }

    public static void ReloadPrefix(BagInventory __instance, out ReloadCall? __state)
    {
        __state = null;
        try
        {
            var call = new ReloadCall(Interlocked.Increment(ref sequence), Environment.CurrentManagedThreadId,
                __instance.Count, Environment.TickCount64);
            lock (Gate) Histories.GetOrCreateValue(__instance).Active.Add(call.Id, call);
            __state = call;
            Report(call.Thread == startupThread ? "reload-startup-thread" : "reload-other-thread",
                () => $"Reload begin bag={RuntimeHelpers.GetHashCode(__instance)} {call}\n{Describe(__instance)}\n{Environment.StackTrace}", null);
        }
        catch { /* Diagnostics must not change game behavior. */ }
    }

    public static Exception? ReloadFinalizer(BagInventory __instance, ReloadCall? __state, Exception? __exception)
    {
        try
        {
            if (__state != null)
            {
                lock (Gate)
                {
                    var history = Histories.GetOrCreateValue(__instance);
                    history.Active.Remove(__state.Id);
                    history.LastCompleted = $"{__state}; after={__instance.Count}; ended={Environment.TickCount64}; error={__exception?.GetType().Name ?? "none"}";
                }
            }
            if (__exception != null) Report("reload-error", Describe(__instance), __exception);
        }
        catch { }
        return __exception;
    }

    public static void BagFailure(BagInventory bag, int slotId, Exception exception)
    {
        try
        {
            Report("bag-index", $"Suppressed invalid index={slotId}; countNow={bag.Count}; bag={RuntimeHelpers.GetHashCode(bag)}\n{Describe(bag)}\n" +
                $"Patch owners: inner={Owners(typeof(BagInventory), "Item")}; outerItem={Owners(typeof(InventoryPlayerBackpacks), "Item")}; outerCount={Owners(typeof(InventoryPlayerBackpacks), "Count")}", exception);
        }
        catch { }
    }

    private static string Owners(Type type, string property)
    {
        var getter = AccessTools.PropertyGetter(type, property);
        return getter == null ? "missing" : string.Join(",", Harmony.GetPatchInfo(getter)?.Owners ?? Enumerable.Empty<string>());
    }

    private static string Describe(BagInventory bag)
    {
        lock (Gate)
        {
            var history = Histories.GetOrCreateValue(bag);
            return $"Active reloads=[{string.Join("; ", history.Active.Values)}]; lastCompleted=[{history.LastCompleted}]";
        }
    }

    public static void Report(string category, string detail, Exception? exception)
        => Report(category, () => detail, exception);

    private static void Report(string category, Func<string> detail, Exception? exception)
    {
        try
        {
            ILogger? target;
            long total;
            long now = Environment.TickCount64;
            lock (Gate)
            {
                target = logger;
                if (target == null) return;
                bool exists = Reports.TryGetValue(category, out var previous);
                total = previous.Total + 1;
                if (exists && now - previous.Last < 30_000)
                {
                    Reports[category] = (previous.Last, total);
                    return;
                }
                Reports[category] = (now, total);
            }
            target.Warning("{0}", "[BackpackRaceFix diagnostics] " +
                $"category={category} total={total} thread={Environment.CurrentManagedThreadId} startupThread={startupThread} tickMs={now}\n{detail()}\n{exception}");
        }
        catch { }
    }
}
