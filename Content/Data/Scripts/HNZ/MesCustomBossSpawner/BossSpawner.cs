using System;
using HNZ.FlashGps.Interface;
using HNZ.MES;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class BossSpawner
    {
        static readonly Logger Log = LoggerManager.Create(nameof(BossSpawner));
        readonly Boss _bossInfo;
        readonly Scheduler _scheduler;
        readonly MESApi _mesApi;
        readonly FlashGpsApi _gpsApi;
        readonly long _gpsId;
        MESGrid _bossGrid;
        MatrixD _spawningMatrix;

        public BossSpawner(MESApi mesApi, FlashGpsApi localGpsApi, Boss bossInfo)
        {
            _bossInfo = bossInfo;
            _mesApi = mesApi;
            _gpsApi = localGpsApi;
            _scheduler = new Scheduler(bossInfo.Schedules);
            _gpsId = bossInfo.Id.GetHashCode();
        }

        public void Initialize()
        {
            _scheduler.Initialize(DateTime.Now);
        }

        public void Close()
        {
            _bossGrid?.Close();
        }

        public void Update()
        {
            if (GameUtils.EverySeconds(1) && Config.Instance.Enabled)
            {
                if (_scheduler.Update(DateTime.Now))
                {
                    var result = TrySpawn();
                    Log.Info($"scheduled spawn result: {result}; {_bossInfo.SpawnGroup}");
                }
            }

            _bossGrid?.Update();

            // make the "would-be" matrix (before spawning the grid)
            // so we can broadcast it earlier
            if (_spawningMatrix == default(MatrixD))
            {
                TrySetRandomPosition();
                return;
            }

            if (GameUtils.EverySeconds(1))
            {
                var bossEnabled = _bossInfo.Enabled;
                var bossGridClosed = _bossGrid?.Closed ?? true;
                var countdownStarted = _scheduler.Countdown != null;
                Log.Debug($"every second; {_bossInfo.Id}: {bossEnabled}, {bossGridClosed}, {countdownStarted}");

                if (bossEnabled && bossGridClosed && countdownStarted)
                {
                    var gps = new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = string.Format(_bossInfo.CountdownGpsName, LangUtils.HoursToString(_scheduler.Countdown.Value)),
                        Description = _bossInfo.CountdownGpsDescription,
                        Position = _spawningMatrix.Translation,
                        DecaySeconds = 2,
                        Color = Color.Orange,
                        Radius = _bossInfo.GpsRadius,
                    };

                    _gpsApi.AddOrUpdate(gps);

                    Log.Debug($"countdown gps sending: {gps.Name}");
                }
                else
                {
                    _gpsApi.Remove(_gpsId);
                }
            }
        }

        public bool TrySpawn()
        {
            if (!_bossInfo.Enabled)
            {
                return false;
            }

            if (!(_bossGrid?.Closed ?? true))
            {
                Log.Info($"aborted spawning; already spawned: {_bossInfo.Id}");
                return false;
            }

            // position would be zero (not set)
            // if Update() hasn't been called
            // or ResetSpawningPosition() has been called earlier
            if (_spawningMatrix == default(MatrixD))
            {
                if (!TrySetRandomPosition())
                {
                    Log.Warn($"failed spawning; no space: {_bossInfo.Id}");
                    return false;
                }
            }

            IMyCubeGrid existingBossGrid;
            if (MESGrid.TrySearchExistingGrid(_bossInfo.Id, _bossInfo.SpawnSphere, out existingBossGrid))
            {
                Log.Info($"aborted spawning; already spawned. tracking: {_bossInfo.Id}");
                _bossGrid = new MESGrid(_mesApi, _bossInfo, existingBossGrid);
            }
            else
            {
                _bossGrid = new MESGrid(_mesApi, _bossInfo);
                if (!_bossGrid.TryInitialize(_spawningMatrix, true))
                {
                    return false;
                }
            }

            // reset so the next "spawn" attempt will generate a new matrix
            _spawningMatrix = default(MatrixD);
            return true;
        }

        bool TrySetRandomPosition()
        {
            return _bossInfo.PlanetSpawn
                ? TryGetRandomPositionOnPlanet(_bossInfo.SpawnSphere, _bossInfo.ClearanceRadius, out _spawningMatrix)
                : TryGetRandomPosition(_bossInfo.SpawnSphere, _bossInfo.ClearanceRadius, out _spawningMatrix);
        }

        public void TryCleanup(float cleanupRange = 0f)
        {
            _bossGrid?.CleanUpIfFarFromCharacters(cleanupRange);
        }

        public void ResetSpawningPosition()
        {
            _spawningMatrix = default(MatrixD);
        }

        static bool TryGetRandomPosition(BoundingSphereD sphere, float clearance, out MatrixD matrix)
        {
            for (var i = 0; i < 100; i++)
            {
                Vector3D position;
                if (GameUtils.TryGetRandomPosition(sphere, clearance, 0, out position))
                {
                    var tested = new BoundingSphereD(position, 10000);
                    if (!Config.Instance.IntersectsAnySpawnVoids(tested))
                    {
                        matrix = MatrixD.CreateWorld(position, Vector3D.Forward, Vector3D.Up);
                        return true;
                    }
                }
            }

            matrix = default(MatrixD);
            return false;
        }

        //todo optimize
        static bool TryGetRandomPositionOnPlanet(BoundingSphereD sphere, float clearance, out MatrixD matrix)
        {
            var position = MathUtils.GetRandomPosition(sphere);
            var planet = PlanetCollection.GetClosestPlanet(position);
            if (planet == null) // no planet in the world
            {
                matrix = default(MatrixD);
                return false;
            }

            var planetSphere = new BoundingSphereD(planet.PositionComp.GetPosition(), planet.AverageRadius);
            for (var i = 0; i < 100; i++)
            {
                var p = MathUtils.GetRandomPosition(planetSphere);
                var surfacePoint = planet.GetClosestSurfacePointGlobal(p);
                if (GameUtils.TestSurfaceFlat(planet, surfacePoint, 20f, 2f))
                {
                    var s = new BoundingSphereD(surfacePoint, clearance);
                    if (!GameUtils.HasAnyGridsInSphere(s))
                    {
                        var surfaceNormal = (surfacePoint - planet.PositionComp.GetPosition()).Normalized();
                        var forward = Vector3D.Cross(Vector3D.Cross(surfaceNormal, Vector3D.Forward), surfaceNormal);
                        matrix = MatrixD.CreateWorld(surfacePoint, forward, surfaceNormal);
                        return true;
                    }
                }
            }

            matrix = default(MatrixD);
            return false;
        }
    }
}