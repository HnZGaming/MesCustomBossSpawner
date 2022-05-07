﻿using System;
using System.Collections.Generic;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.MES
{
    public sealed class MESGrid
    {
        static readonly Logger Log = LoggerManager.Create(nameof(MESGrid));

        readonly MESApi _mesApi;
        readonly ModStorageEntry _id;
        IMyCubeGrid _grid;
        string _spawnGroup;
        bool _cleanupIgnored;

        public MESGrid(MESApi mesApi, ModStorageEntry id)
        {
            _mesApi = mesApi;
            _id = id;
        }

        public bool Closed => _grid == null;
        public bool Compromised { get; private set; }

        public bool TryInitialize(string spawnGroup, string factionTag, MatrixD matrix, bool ignoreSafetyCheck)
        {
            Log.Info($"spawning: [{factionTag}] {spawnGroup} at {matrix}");

            _spawnGroup = spawnGroup;

            if (!_mesApi.CustomSpawnRequest(new List<string> { spawnGroup }, matrix, Vector3.Zero, ignoreSafetyCheck, factionTag, nameof(MesCustomBossSpawner)))
            {
                return false;
            }

            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
            return true;
        }

        public void Close()
        {
            if (Closed) return;

            if (_grid != null)
            {
                _grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
            }

            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);

            _grid.OrNull()?.Close();
            _grid = null;

            Log.Info($"despawned grid: {_spawnGroup}");
        }

        public void Update()
        {
            if (Closed) return;

            // deleted or whatever
            if (_grid != null && _grid.Closed)
            {
                Log.Info($"boss grid closed by someone else: {_grid.DisplayName}");
                Close();
                return;
            }

            if (!_cleanupIgnored)
            {
                _cleanupIgnored = _mesApi.SetSpawnerIgnoreForDespawn(_grid, true);
                if (_cleanupIgnored)
                {
                    Log.Info($"cleanup ignored: {_spawnGroup}");
                }
            }
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (_id.TestPresence(grid.Storage))
            {
                Log.Info($"spawn found: {grid.DisplayName} for spawn group: {_spawnGroup}; id: {_id}");

                _grid = grid;
                _cleanupIgnored = false;

                _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
                _mesApi.RegisterDespawnWatcher(_grid, OnBossDispawned);

                grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            }
        }

        void OnBlockOwnershipChanged(IMyCubeGrid _)
        {
            //Log.Info($"compromised: {_grid.DisplayName}");
            Compromised = true;
        }

        void OnBossDispawned(IMyCubeGrid grid, string type)
        {
            Log.Info($"dispawned: {grid.DisplayName}, cause: {type}");
            Close();
        }

        public void TryCharacterDistanceCleanup(float radius = 0f)
        {
            if (Closed) return;

            if (radius == 0 || (_grid.OrNull()?.HasCharactersInRadius(radius) ?? false))
            {
                Close();
            }
        }
    }
}