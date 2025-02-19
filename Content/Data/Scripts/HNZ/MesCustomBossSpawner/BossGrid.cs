using System;
using System.Collections.Generic;
using System.Linq;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Logging;
using HNZ.Utils.MES;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class BossGrid
    {
        static readonly Logger Log = LoggerManager.Create(nameof(BossGrid));
        readonly BossConfig _bossConfig;
        readonly Scheduler _scheduler;
        readonly BossGpsChannel _gpsApi;
        readonly long _gpsId;
        readonly MesSpawner _spawner;
        IMyRemoteControl _coreBlock;
        MatrixD? _activationPosition;
        int _originalBlockCount;
        bool _isActivated;

        public BossGrid(MESApi mesApi, BossGpsChannel gpsApi, BossConfig bossConfig)
        {
            _bossConfig = bossConfig;
            _gpsApi = gpsApi;
            _scheduler = new Scheduler(bossConfig.Schedules);
            _gpsId = bossConfig.Id.GetHashCode();

            var spawnGroupIndex = MathUtils.WeightedRandom(bossConfig.SpawnGroup.Select(c => c.Weight).ToArray());
            var spawnGroupName = bossConfig.SpawnGroup[spawnGroupIndex].SpawnGroupName;
            _spawner = new MesSpawner(mesApi, spawnGroupName, bossConfig.Id);
        }

        IMyCubeGrid Grid => _spawner.SpawnedGrid;
        public bool Closed { get; private set; }

        public void Initialize()
        {
            _scheduler.Initialize(DateTime.Now);
            _spawner.OnGridSet += OnGridSet;

            Vector3D position;
            if (BossActivationTracker.Instance.TryGetActivationPosition(_bossConfig.Id, out position))
            {
                _isActivated = true;
                _activationPosition = MatrixD.CreateTranslation(position);
            }
        }

        public void Close(string reason)
        {
            if (Closed) return;

            Log.Info($"closing boss: {_bossConfig.Id}; reason: {reason}");

            Closed = true;
            Grid.OrNull()?.Close();
            _spawner.Close();
            _gpsApi.Remove(_gpsId);
            _spawner.OnGridSet -= OnGridSet;

            BossActivationTracker.Instance.OnInvalidate(_bossConfig.Id);
        }

        public void Update()
        {
            if (Closed) return;
            if (!Config.Instance.Enabled) return;

            if (GameUtils.EverySeconds(1))
            {
                if (_scheduler.Update(DateTime.Now) && !_isActivated)
                {
                    var result = TryActivate();
                    Log.Info($"activation (scheduled) result: {result}; {_bossConfig.Id}");
                }

                IMyPlayer player;
                if (TryDetectPlayerEncounter(out player))
                {
                    var result = TrySpawn();
                    Log.Info($"spawn (scheduled) result: {result}; {_bossConfig.Id}, player: {player.DisplayName}");
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
            if (_activationPosition == null)
            {
                ResetActivationPosition();
                return;
            }

            if (GameUtils.EverySeconds(1))
            {
                var spawningPosition = _activationPosition.Value;
                var bossEnabled = _bossConfig.Enabled;
                var bossGridClosed = Grid?.Closed ?? false;
                var countdownStarted = _scheduler.Countdown != null;
                Log.Debug($"every second; {_bossConfig.Id}: {bossEnabled}, {bossGridClosed}, {countdownStarted}");
                if (!bossEnabled || bossGridClosed || !countdownStarted) return;

                if (Grid != null)
                {
                    _gpsApi.AddOrUpdate(new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = _bossConfig.GridGpsName,
                        Description = _bossConfig.GpsDescription,
                        Position = _coreBlock.OrNull()?.GetPosition() ?? Grid.GetPosition(),
                        DecaySeconds = 5,
                        Color = Color.Orange,
                        Radius = _bossConfig.GpsRadius,
                        SuppressSound = true,
                    });
                }
                else if (_isActivated)
                {
                    _gpsApi.AddOrUpdate(new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = _bossConfig.GridGpsName,
                        Description = _bossConfig.GpsDescription,
                        Position = spawningPosition.Translation,
                        DecaySeconds = 5,
                        Color = Color.Orange,
                        Radius = _bossConfig.GpsRadius,
                        SuppressSound = true,
                    });
                }
                else
                {
                    _gpsApi.AddOrUpdate(new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = string.Format(_bossConfig.CountdownGpsName, LangUtils.HoursToString(_scheduler.Countdown.Value)),
                        Description = _bossConfig.GpsDescription,
                        Position = spawningPosition.Translation,
                        DecaySeconds = 5,
                        Color = Color.Orange,
                        Radius = _bossConfig.GpsRadius,
                        SuppressSound = true,
                    });
                }
            }
        }

        public bool TryActivate()
        {
            if (_isActivated)
            {
                Log.Warn("already activated");
                return false;
            }

            if (!_bossConfig.Enabled)
            {
                Log.Warn($"aborted activation; not enabled: {_bossConfig.Id}");
                return false;
            }

            _isActivated = true;
            return true;
        }

        bool TryDetectPlayerEncounter(out IMyPlayer player)
        {
            player = null;
            if (!_isActivated) return false;
            if (Closed) return false;
            if (Grid.OrNull() != null) return false;
            if (_spawner.State == MesSpawner.SpawningState.Spawning) return false;
            if (_activationPosition == null) return false;
            return TryGetPlayerNearby(_activationPosition.Value.Translation, Config.Instance.EncounterRange, out player);
        }

        public bool TrySpawn()
        {
            if (!_bossConfig.Enabled)
            {
                Log.Warn($"aborted spawning; not enabled: {_bossConfig.Id}");
                return false;
            }

            if (Grid != null)
            {
                Log.Warn($"aborted spawning; already spawned: {_bossConfig.Id}");
                return false;
            }

            // not a 100% proof but checks if the boss has already spawned there
            var searchSphere = (BoundingSphereD)_bossConfig.SpawnSphere;
            var existingGrids = MyEntities
                .GetTopMostEntitiesInSphere(ref searchSphere)
                .OfType<IMyCubeGrid>();
            foreach (var existingGrid in existingGrids)
            {
                if (_spawner.IsMine(existingGrid))
                {
                    Log.Warn($"aborted spawning; already spawned: {_bossConfig.Id}");
                    return false;
                }
            }

            MatrixD? spawnPosition;
            if (_activationPosition == null)
            {
                spawnPosition = TryGetRandomPosition();
            }
            else
            {
                // re-calculating the spawn position here,
                // which causes the boss to spawn at a slightly offset position
                // but ensures that it won't clip into player grids.
                var sphere = new BoundingSphereD(_activationPosition.Value.Translation, 10000);
                spawnPosition = TryGetRandomPosition(sphere);
            }

            if (spawnPosition == null)
            {
                Log.Warn($"failed spawning; no space: {_bossConfig.Id}");
                return false;
            }

            _spawner.RequestSpawn(spawnPosition.Value, true);
            if (_spawner.State == MesSpawner.SpawningState.Failure)
            {
                Log.Warn("failed spawning; MES error");
                return false;
            }

            return true;
        }

        public void Initialize(IEnumerable<IMyCubeGrid> grids)
        {
            foreach (var grid in grids)
            {
                if (!_spawner.IsMine(grid))
                {
                    Log.Debug($"not mine: {grid.DisplayName}");
                    continue;
                }

                if (!CanRemoveMyGrid(grid))
                {
                    Log.Debug($"not removing: {grid}");
                    continue;
                }

                grid.Close();
                Log.Info($"Removed leftover grid: {_bossConfig.Id}");
            }
        }

        bool CanRemoveMyGrid(IMyCubeGrid grid)
        {
            var ownerId = grid.BigOwners.GetFirstOrElse(0);
            if (ownerId == 0) return false;
            Log.Info("4: owned by somebody");
            if (MyAPIGateway.Players.TryGetSteamId(ownerId) != 0) return false;
            Log.Info("5: owned by npc");

            if (!MyVisualScriptLogicProvider.HasPower($"{grid.EntityId}")) return false;
            MyLog.Default.Info("6: powered");
            if (!ContainsRivalAiBlock(grid)) return false;
            MyLog.Default.Info("7: has AI blocks");

            return true;
        }

        void OnGridSet()
        {
            _coreBlock = Grid.GetFatBlocks<IMyRemoteControl>().FirstOrDefault();
            Log.Debug($"{_bossConfig.Id} boss remote control found?: {_coreBlock != null}");

            Grid.DisplayName = $"[BOSS] {_bossConfig.Id}";

            _originalBlockCount = ((MyCubeGrid)Grid).BlocksCount;
            Log.Debug($"{_bossConfig.Id} original block count: {_originalBlockCount}");

            BossActivationTracker.Instance.OnInvalidate(_bossConfig.Id);
        }

        public void ResetActivationPosition()
        {
            _activationPosition = TryGetRandomPosition();
            if (_activationPosition != null)
            {
                BossActivationTracker.Instance.OnActivate(_bossConfig.Id, _activationPosition.Value.Translation);
            }
        }

        MatrixD? TryGetRandomPosition(BoundingSphereD? sphere = null)
        {
            return _bossConfig.PlanetSpawn
                ? TryGetRandomPositionOnPlanet(sphere ?? _bossConfig.SpawnSphere, _bossConfig.ClearanceRadius)
                : TryGetRandomPositionInSpace(sphere ?? _bossConfig.SpawnSphere, _bossConfig.ClearanceRadius);
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

        static bool TryGetPlayerNearby(Vector3D position, double range, out IMyPlayer player)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            foreach (var p in players)
            {
                if (p.Character == null) continue;
                var playerPosition = p.Character.GetPosition();
                var distance = Vector3D.Distance(playerPosition, position);
                if (distance < range)
                {
                    player = p;
                    return true;
                }
            }

            player = null;
            return false;
        }

        static bool ContainsRivalAiBlock(IMyCubeGrid grid)
        {
            foreach (var block in grid.GetFatBlocks<IMyRemoteControl>())
            {
                if (block.BlockDefinition.SubtypeId.StartsWith("RivalAIRemoteControl"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}