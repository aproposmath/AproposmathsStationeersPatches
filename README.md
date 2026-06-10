# AproposMath's Stationeers Patches

Various small patches/enhancements for Stationeers

- Show detailed mood and hygiene stats in the tooltips
- Fix overshoot of pressure regulators if liquid is in input/output pipes (highly experimental, disabled by default)

Stable only (these issues are fixed in the latest game versions on beta, but not in the stable branch yet):
- Fix `StackSize` LogicType in IC10 to read the total number of a attached devices in a cable network
- Rotate content of logic displays if they are mounted upside down (disabled on newer game versions, where it's fixed already)
- Fix indirect register access in `l` and `s` IC10 instructions (e.g. `s rr2 Setting 22`)
- Fix access to defined numbers in `l` and `s` IC10 instructions (e.g. `s MYDEFINE Setting 42`)

All patches can be enabled/disabled in the StationeersLaunchPad configuration menu at startup.
This mod
  - Does not alter savegame files, you can add/remove it at any time without breaking something*
  - Is client-only, the server (or other players) do not need it in multiplayer games

* You will get errors again in IC10 scripts using `StackSize` of cable networks after removing the mod

## Installation
This is a **StationeersLaunchPad** Plugin Mod. It requires BepInEx to be installed.
See: https://github.com/StationeersLaunchPad/StationeersLaunchPad

### Steam Workshop

Subscribe to the mod on the Steam Workshop and it will be automatically downloaded and installed by the Steam client.
https://steamcommunity.com/sharedfiles/filedetails/?id=3601834995

### StationeersLaunchPad

Use this to install dev builds straight from the main branch.

- Stop game start by clicking on the black Launchpad window in the loading screen
- Type the following two commands one after another in the console and press enter after each line (you can paste them with Ctrl+V):
```
slp repos add github.com/aproposmath/AproposmathsStationeersPatches
slp repomods add branch=main aproposmaths-stationeers-patches
```
- Make sure the "Repo" version of the mod is selected and the "Workshop" version is not selected in the mod list (in case you subscribed the mod before).
