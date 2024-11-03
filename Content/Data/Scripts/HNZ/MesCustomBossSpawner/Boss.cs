using System.Collections.Generic;
using HNZ.FlashGps.Interface;
using HNZ.Utils.MES;
using VRage.Game.ModAPI;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Boss
    {
        readonly MESApi _mesApi;
        readonly FlashGpsApi _gpsApi;
        readonly BossInfo _info;
        BossGrid _grid;

        public Boss(MESApi mesApi, FlashGpsApi gpsApi, BossInfo info)
        {
            _mesApi = mesApi;
            _gpsApi = gpsApi;
            _info = info;
        }

        public void Initialize()
        {
            _grid = new BossGrid(_mesApi, _gpsApi, _info);
            _grid.Initialize();
        }

        public void Close(string reason)
        {
            _grid.Close(reason);
        }

        public void OnFirstFrame(IEnumerable<IMyCubeGrid> grids)
        {
            _grid.TryInitializeWithSceneGrid(grids);
        }

        public void Update()
        {
            _grid.Update();
            if (_grid.Closed)
            {
                Initialize();
            }
        }

        public bool TrySpawn()
        {
            return _grid.TrySpawn();
        }

        public void ResetSpawningPosition()
        {
            _grid.ResetSpawningPosition();
        }
    }
}