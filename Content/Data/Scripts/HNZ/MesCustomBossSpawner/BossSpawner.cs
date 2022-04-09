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
        Vector3D? _gpsPosition;

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

            if (_gpsPosition == null)
            {
                _gpsPosition = MakeRandomPosition();
            }

            if (GameUtils.EverySeconds(1))
            {
                if (_bossInfo.Enabled && (_bossGrid?.Closed ?? true) && _scheduler.Countdown != null)
                {
                    _gpsApi.AddOrUpdate(new FlashGpsSource
                    {
                        Id = _gpsId,
                        Name = string.Format(_bossInfo.CountdownGpsName, LangUtils.HoursToString(_scheduler.Countdown.Value)),
                        Description = _bossInfo.CountdownGpsDescription,
                        Position = _gpsPosition.Value,
                        DecaySeconds = 2,
                        Color = Color.Orange,
                    });
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

            var position = _gpsPosition ?? MakeRandomPosition();
            if (!GameUtils.TryGetRandomPosition(position, 10000, 1000, out position))
            {
                Log.Warn($"failed spawning; no space: {_bossInfo.SpawnGroup}");
                return false;
            }

            _bossGrid = new MESGrid(_mesApi, _bossInfo.ModStorageId);
            return _bossGrid.TryInitialize(_bossInfo.SpawnGroup, _bossInfo.FactionTag, position, true);
        }

        public void TryCleanup(float cleanupRange = 0f)
        {
            _bossGrid?.TryCharacterDistanceCleanup(cleanupRange);
        }

        Vector3D MakeRandomPosition()
        {
            Vector3D position;
            GameUtils.TryGetRandomPosition(Vector3D.Zero, _bossInfo.SpawnRadius, 1000, out position);
            return position;
        }
    }
}