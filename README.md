# AproposMath's Stationeers Patches

Various small patches/enhancements for Stationeers

- Show detailed mood and hygiene stats in the tooltips
- Fix `StackSize` LogicType in IC10 to read the total number of a attached devices in a cable network
- Rotate content of logic displays if they are mounted upside down (disabled on newer game versions, where it's fixed already)

These patches/fixes can be enabled/disabled in the StationeersLaunchPad configuration menu at startup.
This mod
  - Does not alter safegame files, you can add/remove it at any time without breaking something*
  - Is client-only, the server (or other players) do not need it in multiplayer games


* You will get errors again in IC10 scripts using `StackSize` of cable networks after removing the mod

## Installation
This is a **StationeersLaunchPad** Plugin Mod. It requires BepInEx to be installed.
See: https://github.com/StationeersLaunchPad/StationeersLaunchPad
