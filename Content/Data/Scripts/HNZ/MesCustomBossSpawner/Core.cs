using System;
using HNZ.LocalGps.Interface;
using HNZ.MES;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Core
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Core));
        static readonly long GpsId = nameof(MesCustomBossSpawner).GetHashCode();

        readonly Scheduler _scheduler;
        readonly MESApi _mesApi;
        readonly LocalGpsApi _gpsApi;
        MESGrid _bossGrid;
        Vector3D? _gpsPosition;

        public Core(Scheduler scheduler)
        {
            _scheduler = scheduler;
            _mesApi = new MESApi();
            _gpsApi = new LocalGpsApi(nameof(MesCustomBossSpawner).GetHashCode());
        }

        public void Initialize()
        {
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
                    Log.Info($"scheduled spawn result: {result}");
                }
            }

            _bossGrid?.Update();

            if (_gpsPosition == null)
            {
                _gpsPosition = MakeRandomPosition();
            }

            if (GameUtils.EverySeconds(1))
            {
                if ((_bossGrid?.Closed ?? true) && _scheduler.Countdown != null)
                {
                    _gpsApi.AddOrUpdateLocalGps(new LocalGpsSource
                    {
                        Id = GpsId,
                        Name = string.Format(Config.Instance.CountdownGpsName, LangUtils.HoursToString(_scheduler.Countdown.Value)),
                        Description = Config.Instance.CountdownGpsDescription,
                        Position = _gpsPosition.Value,
                        Color = Color.Orange,
                    });
                }
                else
                {
                    _gpsApi.RemoveLocalGps(GpsId);
                }
            }
        }

        public bool TrySpawn()
        {
            if (!(_bossGrid?.Closed ?? true))
            {
                Log.Info("aborted spawning: already spawned");
                return false;
            }

            var position = _gpsPosition ?? MakeRandomPosition();
            if (!GameUtils.TryGetRandomPosition(position, 10000, 1000, out position))
            {
                Log.Warn("failed spawning: no space");
                return false;
            }

            _bossGrid?.Close(); // just in case
            _bossGrid = new MESGrid(_mesApi, Config.Instance.ModStorageId);
            return _bossGrid.TryInitialize(Config.Instance.SpawnGroup, position, true);
        }

        public void TryCleanup(float cleanupRange = 0f)
        {
            _bossGrid?.TryCharacterDistanceCleanup(cleanupRange);
        }

        static Vector3D MakeRandomPosition()
        {
            Vector3D position;
            GameUtils.TryGetRandomPosition(Vector3D.Zero, Config.Instance.SpawnRadius, 1000, out position);
            return position;
        }
    }
}