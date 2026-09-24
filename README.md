# Backpack Race Fix

A client-side compatibility mod for Vintage Story 1.22.7 that prevents crashes
observed while backpack inventories are rebuilt during multiplayer synchronization.

It includes compatibility guards for:

- Hydrate-or-Diedrate liquid encumbrance scans
- XSkills extra backpack slots
- duplicate Vintage Rift Harmony initialization during reconnect
- a login-time GUI teardown race

## Installation

1. Close Vintage Story.
2. Download `backpackracefix_1.1.4.zip` from the GitHub release.
3. Place the ZIP, without extracting it, in the client `VintagestoryData/Mods` folder.
4. Remove older versions of Backpack Race Fix and restart the game.

This mod is client-side and does not need to be installed on the server.

Direct download: https://github.com/ProsperCraft/BackpackRaceFix/releases/download/v1.1.4/backpackracefix_1.1.4.zip

Public builds omit debug symbols and embedded local build paths. No game logs,
account settings, saves, or crash dumps are included.

## 1.1.4

Extends the two placement-search null-slot checks to all inventories using the base
GetBestSuitedSlot implementation. The September 22 torch-holder interaction crash
occurred in this search despite the backpack-only guard being active. Valid slots,
weights, exclusions and unrelated exceptions retain their original behavior. Logs
now include the inventory type when a null slot is skipped. No inventory contents,
slot counts or GUI layout are changed. This guards the observed null scan; the
source of the invalid slot remains unproven. Live multiplayer validation pending.

## 1.1.3 local test build

Protects the observed fruiting-bush harvest crash in GetBestSuitedSlot. Its two
placement searches skip null backpack slots while retaining vanilla suitability,
merge preference, exclusions, and actual slot references. Other inventories and
unrelated exceptions are unchanged. No fake slots, item mutations, or changes to
Count/indexer mapping are introduced. Reports skipped slots in diagnostics.

This does not repair the unresolved extra-slot mapping problem or restore slots
that are already unavailable. It does not fix the separate Linux crash reporter
icon-loading failure or establish a fix for the earlier login task exception.

## 1.1.2 local test build

Prevents the observed backpack GUI composition null-slot crash by excluding only
null entries from backpack exclusion-grid display dictionaries. Preserves real
slot IDs and XSkills extra slots. Retries missing entries on later frames and
recomposes the grid when its displayed slot IDs change, including when inventory
Count stays the same. No inventory contents are removed or replaced.

Fixes diagnostic logging of braces in reload records and exception messages.
Existing 1.1.1 guards are retained, including their known limitations: inner bag
errors can still become null slots for other callers, GUI PostRender suppression
is broad, and the original source of incomplete backpack state is unproven.
This is a targeted crash workaround, not a general inventory thread-safety fix.

## Upstream fixes

- [Hydrate-or-Diedrate PR #190](https://github.com/Chronolegionnaire/HydrateOrDiedrate/pull/190)
- [Vintage Story API PR #95](https://github.com/anegostudios/vsapi/pull/95)
