using System;
using System.Collections.Generic;
using HNZ.Utils;
using HNZ.Utils.Logging;
using HNZ.Utils.MES;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class MesSpawner
    {
        public enum SpawningState
        {
            Idle,
            Spawning,
            Success,
            Failure,
        }

        static readonly Logger Log = LoggerManager.Create(nameof(MesSpawner));
        static readonly Guid ModStorageKey = Guid.Parse("6BFEA3E4-7B06-460C-ADD1-C1A66EB7B5E9");
        const float TimeoutSecs = 10;

        readonly MESApi _mesApi;
        readonly string _spawnGroup;
        readonly string _id;
        MatrixD _targetMatrix;
        DateTime _startTime;
        bool _setIgnoreCleanup;

        public MesSpawner(MESApi mesApi, string spawnGroup, string id)
        {
            _mesApi = mesApi;
            _spawnGroup = spawnGroup;
            _id = id;
            State = SpawningState.Idle;
        }

        public SpawningState State { get; private set; }

        public IMyCubeGrid SpawnedGrid { get; private set; }

        public event Action OnGridSet;

        public void Close()
        {
            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
            OnGridSet = null;
        }

        public void RequestSpawn(MatrixD targetMatrix, bool ignoreSafetyCheck)
        {
            Log.Info($"spawn request: {_spawnGroup} at {targetMatrix.Translation}");

            if (!_mesApi.CustomSpawnRequest(
                    new List<string> { _spawnGroup },
                    targetMatrix,
                    Vector3.Zero,
                    ignoreSafetyCheck,
                    null,
                    nameof(MesCustomBossSpawner)))
            {
                State = SpawningState.Failure;
                return;
            }

            _startTime = DateTime.UtcNow;
            _targetMatrix = targetMatrix;
            State = SpawningState.Spawning;

            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
        }

        public void Update()
        {
            var timeout = _startTime + TimeSpan.FromSeconds(TimeoutSecs) - DateTime.UtcNow;
            if (State == SpawningState.Spawning && timeout.TotalSeconds < 0)
            {
                Log.Warn($"timeout: {_spawnGroup}");
                State = SpawningState.Failure;
            }

            if (State == SpawningState.Success && !_setIgnoreCleanup)
            {
                _mesApi.SetSpawnerIgnoreForDespawn(SpawnedGrid, true);
                _setIgnoreCleanup = true;
            }
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            // not my spawn group
            if (!TestIdentity(grid, _spawnGroup)) return;

            Log.Info($"spawn found: {grid.DisplayName} for spawn group: {_spawnGroup}");

            // not mine
            var gridPos = grid.WorldMatrix.Translation;
            if (Vector3D.Distance(gridPos, _targetMatrix.Translation) > 500)
            {
                Log.Warn($"different position: {_spawnGroup}, {_id}");
                return;
            }

            grid.UpdateStorageValue(ModStorageKey, _id);
            SetSpawnedGrid(grid);
        }

        public void SetSpawnedGrid(IMyCubeGrid grid)
        {
            SpawnedGrid = grid;
            State = SpawningState.Success;
            OnGridSet?.Invoke();
        }

        public static bool TestIdentity(IMyCubeGrid grid, string spawnGroup, string id = null)
        {
            if (grid == null) return false;
            if (!NpcData.TestSpawnGroup(grid, spawnGroup)) return false;

            if (id != null)
            {
                string existingId;
                if (!grid.TryGetStorageValue(ModStorageKey, out existingId)) return false;
                if (existingId != id) return false;
            }

            return true;
        }
    }
}