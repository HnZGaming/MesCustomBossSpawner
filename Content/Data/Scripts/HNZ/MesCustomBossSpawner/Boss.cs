﻿using System.Collections.Generic;
using HNZ.Utils.MES;
using VRage.Game.ModAPI;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Boss
    {
        readonly MESApi _mesApi;
        readonly BossGpsChannel _gpsApi;
        readonly BossConfig _config;
        BossGrid _grid;

        public Boss(MESApi mesApi, BossGpsChannel gpsApi, BossConfig config)
        {
            _mesApi = mesApi;
            _gpsApi = gpsApi;
            _config = config;
        }

        public void Initialize()
        {
            _grid = new BossGrid(_mesApi, _gpsApi, _config);
            _grid.Initialize();
        }

        public void Close(string reason)
        {
            _grid.Close(reason);
        }

        public void OnFirstFrame(IEnumerable<IMyCubeGrid> grids)
        {
            _grid.Initialize(grids);
        }

        public void Update()
        {
            _grid.Update();
            if (_grid.Closed)
            {
                Initialize();
            }
        }

        public bool TryActivate()
        {
            return _grid.TryActivate();
        }

        public bool TrySpawn()
        {
            return _grid.TrySpawn();
        }

        public void ResetSpawningPosition()
        {
            _grid.ResetActivationPosition();
        }
    }
}