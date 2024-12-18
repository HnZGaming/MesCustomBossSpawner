﻿using System;
using System.Collections.Generic;
using System.Linq;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Logging;
using HNZ.Utils.MES;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class BossGrid
    {
        static readonly Logger Log = LoggerManager.Create(nameof(BossGrid));
        readonly BossInfo _bossInfo;
        readonly Scheduler _scheduler;
        readonly BossGpsChannel _gpsApi;
        readonly long _gpsId;
        readonly MesSpawner _spawner;
        IMyRemoteControl _coreBlock;
        MatrixD? _spawnPosition;
        int _originalBlockCount;
        DateTime? _abandonStartTime;

        public BossGrid(MESApi mesApi, BossGpsChannel gpsApi, BossInfo bossInfo)
        {
            _bossInfo = bossInfo;
            _gpsApi = gpsApi;
            _scheduler = new Scheduler(bossInfo.Schedules);
            _gpsId = bossInfo.Id.GetHashCode();
            _spawner = new MesSpawner(mesApi, bossInfo.SpawnGroup, bossInfo.Id);
        }

        IMyCubeGrid Grid => _spawner.SpawnedGrid;
        public bool Closed { get; private set; }

        public void Initialize()
        {
            _scheduler.Initialize(DateTime.Now);
            _spawner.OnGridSet += OnGridSet;
            _abandonStartTime = null;
        }

        public void Close(string reason)
        {
            if (Closed) return;

            Log.Info($"closing boss: {_bossInfo.Id}; reason: {reason}");

            Closed = true;
            Grid.OrNull()?.Close();
            _spawner.Close();
            _gpsApi.Remove(_gpsId);
            _spawner.OnGridSet -= OnGridSet;
        }

        public void Update()
        {
            if (Closed) return;

            if (GameUtils.EverySeconds(1) && Config.Instance.Enabled)
            {
                if (_scheduler.Update(DateTime.Now))
                {
                    var result = TrySpawn();
                    Log.Info($"scheduled spawn result: {result}; {_bossInfo.SpawnGroup}");
                }

                var isAbandoned = Grid != null && IsAbandoned();
                if (!isAbandoned)
                {
                    _abandonStartTime = null;
                }
                else if (_abandonStartTime == null)
                {
                    Log.Info($"Boss abandoned: {_bossInfo.Id}");
                    _abandonStartTime = DateTime.UtcNow;
                }
                else if ((DateTime.UtcNow - _abandonStartTime)?.TotalSeconds > Config.Instance.AbandonCountdown)
                {
                    Log.Info($"Closing boss abandoned: {_bossInfo.Id}");
                    Close("Abandoned");
                }
            }

            if (_spawner.State == MesSpawner.SpawningState.Success && _spawner.SpawnedGrid.OrNull() == null)
            {
                Log.Warn("grid deleted by someone else");
                Close("Deleted externally");
                return;
            }

            _spawner.Update();

            // make the "would-be" matrix (before spawning the grid)
            // so we can broadcast it earlier
            if (_spawnPosition == null)
            {
                _spawnPosition = TryGetRandomSpawnPosition();
                return;
            }

            var spawningPosition = _spawnPosition.Value;

            if (GameUtils.EverySeconds(1))
            {
                var bossEnabled = _bossInfo.Enabled;
                var bossGridClosed = Grid?.Closed ?? false;
                var countdownStarted = _scheduler.Countdown != null;
                Log.Debug($"every second; {_bossInfo.Id}: {bossEnabled}, {bossGridClosed}, {countdownStarted}");
                if (!bossEnabled || bossGridClosed || !countdownStarted) return;

                if (Grid != null)
                {
                    _gpsApi.AddOrUpdate(new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = _bossInfo.GridGpsName,
                        Description = _bossInfo.GpsDescription,
                        Position = _coreBlock.OrNull()?.GetPosition() ?? Grid.GetPosition(),
                        DecaySeconds = 5,
                        Color = Color.Orange,
                        Radius = _bossInfo.GpsRadius,
                        SuppressSound = true,
                    });
                }
                else
                {
                    _gpsApi.AddOrUpdate(new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = string.Format(_bossInfo.CountdownGpsName, LangUtils.HoursToString(_scheduler.Countdown.Value)),
                        Description = _bossInfo.GpsDescription,
                        Position = spawningPosition.Translation,
                        DecaySeconds = 5,
                        Color = Color.Orange,
                        Radius = _bossInfo.GpsRadius,
                        SuppressSound = true,
                    });
                }
            }
        }

        public bool TrySpawn()
        {
            if (!_bossInfo.Enabled)
            {
                Log.Warn($"aborted spawning; not enabled: {_bossInfo.Id}");
                return false;
            }

            if (Grid != null)
            {
                Log.Warn($"aborted spawning; already spawned: {_bossInfo.Id}");
                return false;
            }

            // not a 100% proof but checks if the boss has already spawned there
            var searchSphere = (BoundingSphereD)_bossInfo.SpawnSphere;
            var entities = MyEntities.GetTopMostEntitiesInSphere(ref searchSphere).ToArray();
            if (TryInitializeWithSceneGrid(entities.OfType<IMyCubeGrid>()))
            {
                Log.Warn($"aborted spawning; already spawned: {_bossInfo.Id}");
                _spawnPosition = Grid?.WorldMatrix;
                return false;
            }

            if (_spawnPosition == null)
            {
                _spawnPosition = TryGetRandomSpawnPosition();
            }
            else
            {
                // re-calculating the spawn position here,
                // which causes the boss to spawn at a slightly offset position
                // but ensures that it won't clip into player grids.
                var sphere = new BoundingSphereD(_spawnPosition.Value.Translation, 10000);
                _spawnPosition = TryGetRandomSpawnPosition(sphere);
            }

            if (_spawnPosition == null)
            {
                Log.Warn($"failed spawning; no space: {_bossInfo.Id}");
                return false;
            }

            _spawner.RequestSpawn(_spawnPosition.Value, true);
            if (_spawner.State == MesSpawner.SpawningState.Failure)
            {
                Log.Warn("failed spawning; MES error");

                // reset so the next "spawn" attempt will generate a new matrix
                _spawnPosition = null;
                return false;
            }

            return true;
        }

        public bool TryInitializeWithSceneGrid(IEnumerable<IMyCubeGrid> grids)
        {
            foreach (var grid in grids)
            {
                if (MesSpawner.TestIdentity(grid, _bossInfo.SpawnGroup, _bossInfo.Id))
                {
                    Log.Info($"initializing with grid in scene: {_bossInfo.Id}");
                    _spawner.SetSpawnedGrid(grid);
                    return true;
                }
            }

            return false;
        }

        void OnGridSet()
        {
            _coreBlock = Grid.GetFatBlocks<IMyRemoteControl>().FirstOrDefault();
            Log.Debug($"{_bossInfo.Id} boss remote control found?: {_coreBlock != null}");

            Grid.DisplayName = $"[BOSS] {_bossInfo.Id}";

            _originalBlockCount = ((MyCubeGrid)Grid).BlocksCount;
            Log.Debug($"{_bossInfo.Id} original block count: {_originalBlockCount}");
        }

        bool IsAbandoned()
        {
            var blocksCount = ((MyCubeGrid)Grid).BlocksCount;
            Log.Debug($"{_bossInfo.Id} block count: {blocksCount}");
            if (blocksCount >= _originalBlockCount) return false;

            var hasPlayersNearby = HasPlayersNearby(Grid, Config.Instance.AbandonRange);
            Log.Debug($"{_bossInfo.Id} has players nearby: {hasPlayersNearby} in {Config.Instance.AbandonRange}");
            if (hasPlayersNearby) return false;

            return true;
        }

        public void ResetSpawningPosition()
        {
            _spawnPosition = TryGetRandomSpawnPosition();
        }

        MatrixD? TryGetRandomSpawnPosition(BoundingSphereD? sphere = null)
        {
            return _bossInfo.PlanetSpawn
                ? TryGetRandomPositionOnPlanet(sphere ?? _bossInfo.SpawnSphere, _bossInfo.ClearanceRadius)
                : TryGetRandomPositionInSpace(sphere ?? _bossInfo.SpawnSphere, _bossInfo.ClearanceRadius);
        }

        static MatrixD? TryGetRandomPositionInSpace(BoundingSphereD sphere, float clearance)
        {
            for (var i = 0; i < 100; i++)
            {
                Vector3D position;
                if (GameUtils.TryGetRandomPosition<IMyEntity>(sphere, clearance, 0, out position))
                {
                    var tested = new BoundingSphereD(position, 10000);
                    if (!Config.Instance.IntersectsAnySpawnVoids(tested))
                    {
                        return MatrixD.CreateWorld(position, Vector3D.Forward, Vector3D.Up);
                    }
                }
            }

            return null;
        }

        //todo optimize
        static MatrixD? TryGetRandomPositionOnPlanet(BoundingSphereD sphere, float clearance)
        {
            var position = MathUtils.GetRandomPosition(sphere);
            var planet = PlanetCollection.GetClosestPlanet(position);
            if (planet == null) return null; // no planet in the world

            var planetSphere = new BoundingSphereD(planet.PositionComp.GetPosition(), planet.AverageRadius);
            for (var i = 0; i < 100; i++)
            {
                var p = MathUtils.GetRandomPosition(planetSphere);
                var surfacePoint = planet.GetClosestSurfacePointGlobal(p);
                if (GameUtils.TestSurfaceFlat(planet, surfacePoint, 20f, 2f))
                {
                    var s = new BoundingSphereD(surfacePoint, clearance);
                    if (!GameUtils.HasAnyEntitiesInSphere<IMyCubeGrid>(s))
                    {
                        var surfaceNormal = (surfacePoint - planet.PositionComp.GetPosition()).Normalized();
                        var forward = Vector3D.Cross(Vector3D.Cross(surfaceNormal, Vector3D.Forward), surfaceNormal);
                        return MatrixD.CreateWorld(surfacePoint, forward, surfaceNormal);
                    }
                }
            }

            return null;
        }

        static bool HasPlayersNearby(IMyEntity target, double range)
        {
            var sphere = new BoundingSphereD(target.GetPosition(), range);
            var entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities, MyEntityQueryType.Both);
            foreach (var entity in entities)
            {
                // has "floating" characters nearby
                if (entity is IMyCharacter) return true;

                var g = entity as MyCubeGrid;
                if (g == null) continue;

                // has "seated" characters nearby
                if (g.OccupiedBlocks.Count > 0) return true;
            }

            return false;
        }
    }
}