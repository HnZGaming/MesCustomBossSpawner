# MES Custom Boss Spawner

A mod that allows you to spawn MES spawn groups, irrelevant to player positions. 

## Screenshots

|Description|Screenshot|
|:--|:--|
|Spawning indicator GPS|![alt text](README/spawning.png)|
|Acivtation indicator GPS|![alt text](README/spawned.png)|
|Spawned boss|![alt text](README/grid.png)|

## Configuration Sample

```xml
<?xml version="1.0" encoding="utf-16"?>
<Config>
  <Bosses>
    <Boss>
      <Id>Common_Earth</Id>
      <Enabled>true</Enabled>
      <SpawnGroup>Orks-SpawnGroup-Station-ScrappaHut</SpawnGroup>
      <PlanetSpawn>true</PlanetSpawn>
      <GridGpsName>Something Common</GridGpsName>
      <CountdownGpsName>Something Common comes in {0}</CountdownGpsName>
      <GpsDescription />
      <SpawnSphere X="0" Y="0" Z="0" Radius="200000" />
      <ClearanceRadius>1000</ClearanceRadius>
      <GpsRadius>400000</GpsRadius>
      <Schedules>
        <Schedule OffsetHours="0.25" IntervalHours="1" />
      </Schedules>
    </Boss>
    <Boss>
        ...
    </Boss>
</Config>
```

Schedule should've been cron but I didn't think of that at the moment.

## Commands

|Example|Description|
|:--|:--|
|`/cbs activate Common_Earth`|Activartes a spawn group on player GPS HUDs.|
|`/cbs spawn Common_Earth`|Spawns a spawn group in the game world.|
|`/cbs despawn Common_Earth`|Despawns a spawn group.|
|`/cbs reset`|Despawns all bosses and reset their next spawning positions.|
|`/cbs reload`|Reloads the config from the disk, and despawns all bosses.|

Commands are available to admin players.

## Installation

- Add the MES mod in the world.

## Creating Bosses

The target MES spawn group definition must be modified as follows:

```diff
- [BossEncounterSpace:true]
+ [BossEncounterSpace:false]
[RandomizeWeapons:true]
[AddDefenseShieldBlocks:true]
+ [RivalAiSpawn: true]
[UseRivalAi:true]
[RivalAiReplaceRemoteControl:true]
```

## Other Interface Notes

- This mod uses grid mod storage ID `6BFEA3E4-7B06-460C-ADD1-C1A66EB7B5E9`.