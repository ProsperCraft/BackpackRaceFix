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
2. Download `backpackracefix_1.1.0.zip` from the latest release.
3. Place the ZIP, without extracting it, in the client `VintagestoryData/Mods` folder.
4. Remove older versions of Backpack Race Fix and restart the game.

This mod is client-side and does not need to be installed on the server. OptiTime
1.5.16 should remain disabled for the affected mod combination.

## Upstream fixes

- [Hydrate-or-Diedrate PR #190](https://github.com/Chronolegionnaire/HydrateOrDiedrate/pull/190)
- [Vintage Story API PR #95](https://github.com/anegostudios/vsapi/pull/95)

