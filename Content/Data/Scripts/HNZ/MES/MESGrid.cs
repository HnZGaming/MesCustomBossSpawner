using System;
using System.Collections.Generic;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.MES
{
    public sealed class MESGrid
    {
        public interface IIdentity
        {
            ModStorageEntry PrefabId { get; }
            string InstanceId { get; }
            string SpawnGroup { get; }
            string FactionTag { get; }
        }

        static readonly Logger Log = LoggerManager.Create(nameof(MESGrid));
        const float TimeoutSecs = 10;

        readonly MESApi _mesApi;
        readonly IIdentity _identity;
        IMyCubeGrid _grid;
        bool _cleanupIgnored;
        DateTime? _spawnedTime;
        bool _spawning;
        MatrixD _spawningMatrix;

        public MESGrid(MESApi mesApi, IIdentity identity)
        {
            _mesApi = mesApi;
            _identity = identity;
        }

        public bool Closed => _grid == null;

        public bool TryInitialize(MatrixD matrix, bool ignoreSafetyCheck)
        {
            Log.Info($"spawning: {_identity} at {matrix}");

            if (!_mesApi.CustomSpawnRequest(
                    new List<string> { _identity.SpawnGroup },
                    matrix,
                    Vector3.Zero,
                    ignoreSafetyCheck,
                    _identity.FactionTag,
                    nameof(MesCustomBossSpawner)))
            {
                return false;
            }

            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
            _spawnedTime = DateTime.UtcNow;
            _spawning = true;
            _spawningMatrix = matrix;
            return true;
        }

        public void Close()
        {
            if (Closed) return;

            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);

            _grid.OrNull()?.Close();
            _grid = null;

            _spawning = false;
            _spawnedTime = null;

            Log.Info($"closed grid: {_identity.SpawnGroup}");
        }

        public void Update()
        {
            var timeout = _spawnedTime + TimeSpan.FromSeconds(TimeoutSecs) - DateTime.UtcNow;
            if (_spawning && _grid == null && timeout.HasValue && timeout.Value.TotalSeconds < 0)
            {
                Log.Warn($"timeout: {_identity.SpawnGroup}, {_identity.PrefabId}");
                Close();
                return;
            }

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
                    Log.Info($"cleanup ignored: {_identity.SpawnGroup}");
                }
            }
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (_identity.PrefabId.TestPresence(grid.Storage))
            {
                Log.Info($"spawn found: {grid.DisplayName} for spawn group: {_identity.SpawnGroup}; id: {_identity.PrefabId}");

                var gridPos = grid.WorldMatrix.Translation;
                if (Vector3D.Distance(gridPos, _spawningMatrix.Translation) > 500)
                {
                    Log.Warn("same id but different position");
                    return;
                }

                _grid = grid;
                _cleanupIgnored = false;

                _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
                _mesApi.RegisterDespawnWatcher(_grid, OnBossDispawned);
            }
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