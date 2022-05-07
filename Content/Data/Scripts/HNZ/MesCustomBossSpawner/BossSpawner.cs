using System;
using HNZ.FlashGps.Interface;
using HNZ.MES;
using HNZ.Utils;
using HNZ.Utils.Logging;
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
        Vector3D _spawningPosition;

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
            if (GameUtils.EverySeconds(1))
            {
                if (_scheduler.Update(DateTime.Now))
                {
                    var result = TrySpawn();
                    Log.Info($"scheduled spawn result: {result}; {_bossInfo.SpawnGroup}");
                }
            }

            _bossGrid?.Update();

            if (_spawningPosition == Vector3.Zero)
            {
                TryGetRandomPosition(_bossInfo.SpawnSphere, _bossInfo.ClearanceRadius, out _spawningPosition);
                return;
            }

            if (GameUtils.EverySeconds(1))
            {
                var bossEnabled = _bossInfo.Enabled;
                var bossGridClosed = _bossGrid?.Closed ?? true;
                var countdownStarted = _scheduler.Countdown != null;
                Log.Debug($"every second; {_bossInfo.Id}: {bossEnabled}, {bossGridClosed}, {countdownStarted}");
                Log.Debug($"boss grid state; {_bossInfo.Id}: {_bossGrid != null}, {_bossGrid?.Closed}");

                if (bossEnabled && bossGridClosed && countdownStarted)
                {
                    var gps = new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = string.Format(_bossInfo.CountdownGpsName, LangUtils.HoursToString(_scheduler.Countdown.Value)),
                        Description = _bossInfo.CountdownGpsDescription,
                        Position = _spawningPosition,
                        DecaySeconds = 2,
                        Color = Color.Orange,
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
                Log.Info($"aborted spawning; already spawned: {_bossInfo.SpawnGroup}");
                return false;
            }

            if (_spawningPosition == Vector3.Zero)
            {
                var sphere = new BoundingSphereD(_spawningPosition, 10000);
                if (!TryGetRandomPosition(sphere, 5000, out _spawningPosition))
                {
                    Log.Warn($"failed spawning; no space: {_bossInfo.SpawnGroup}");
                    return false;
                }
            }

            _bossGrid = new MESGrid(_mesApi, _bossInfo.ModStorageId);
            if (!_bossGrid.TryInitialize(_bossInfo.SpawnGroup, _bossInfo.FactionTag, _spawningPosition, true))
            {
                return false;
            }

            _spawningPosition = Vector3D.Zero;
            return true;
        }

        public void TryCleanup(float cleanupRange = 0f)
        {
            _bossGrid?.TryCharacterDistanceCleanup(cleanupRange);
        }

        public void ResetSpawningPosition()
        {
            _spawningPosition = Vector3D.Zero;
        }

        static bool TryGetRandomPosition(BoundingSphereD sphere, float clearance, out Vector3D position)
        {
            for (var i = 0; i < 100; i++)
            {
                if (GameUtils.TryGetRandomPosition(sphere, clearance, 0, out position))
                {
                    var tested = new BoundingSphereD(position, 10000);
                    if (!Config.Instance.IntersectsAnySpawnVoids(tested))
                    {
                        return true;
                    }
                }
            }

            position = default(Vector3D);
            return false;
        }

        static bool TryGetRandomPositionOnPlanetSurface(BoundingSphereD sphere, float clearance, out Vector3D position, out Vector3D upward)
        {
            position = Vector3D.Zero;
            upward = Vector3D.Zero;
            return false;
        }
    }
}