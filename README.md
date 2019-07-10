
# Discord Rich Presence

## About

Risk of Rain 2 does not currently expose game information to Discord, and so this mod was created in order to integrate Risk of Rain 2 with Discord's rich presence system. Show your Discord friends your current run state, invite channels to play, or ask to join a friend, all via Discord!

## Current Features
Join your friends through Discord, either by inviting them or having them ask to join your game!

Rich presence is provided for Classic Runs. The current stage count and stage name are exposed, and icons give an at-a-glance view of the current stage. Hovering over the icon reveals the stage subtitle. The current elapsed run time is shown on stages.

## Command
The mod provides a privacy setting via console commands, `discord_privacy_level`, which takes a single integer as an argument:

Disabled = 0 - no rich presence is broadcast
Presence = 1 - run and lobby information is broadcast, but invites and join requests are disabled
Join = 2 - run and lobby information is broadcast, invites and join requests are allowed

## Build
Requires R2API, BepInEx and [Discord-RPC-Csharp](https://github.com/Lachee/discord-rpc-csharp) (barebones Unity3D dlls, see readme)

## Installation

Requires R2API. Extract contents of zip (from Thunderstore) to Risk of Rain 2/Bepinex/Plugins/

## Versions

Latest 2.3.0 fixes elapsed time on pause, adds additional presence for menus

Version 2.2.X introduces proper elapsed run time

Version 2.1.1 fixes small issues - STABLE

Version 2.1.X introduces elapsed run time

Version 2.X.X introduces lobby features

Versions 1.X.X include run cards only

## Known Issues