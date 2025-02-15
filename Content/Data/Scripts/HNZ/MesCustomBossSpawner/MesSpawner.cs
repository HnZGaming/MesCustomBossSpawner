using System;
using System.Collections.Generic;
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

        readonly MESApi _mesApi;
        readonly string _spawnGroup;
        readonly string _id;

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

            if (!_mesApi.CustomSpawnRequest(new MESApi.CustomSpawnRequestArgs
                {
                    SpawnProfileId = nameof(MesCustomBossSpawner),
                    SpawnGroups = new List<string> { _spawnGroup },
                    SpawningMatrix = targetMatrix,
                    IgnoreSafetyCheck = ignoreSafetyCheck,
                    Context = _id,
                }))
            {
                State = SpawningState.Failure;
                return;
            }

            State = SpawningState.Spawning;

            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
        }

        public void Update()
        {
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (IsMine(grid))
            {
                Log.Info($"[CBS] mes grid set: {grid.DisplayName} for spawn group: {_spawnGroup}, id: {_id}");
                SpawnedGrid = grid;
                State = SpawningState.Success;
                OnGridSet?.Invoke();
            }
        }

        public bool IsMine(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            NpcData npcData;
            if (!NpcData.TryGetNpcData(grid, out npcData)) return false; // shouldn't happen tho
            if (npcData.SpawnGroupName != _spawnGroup) return false;
            if (npcData.Context != _id) return false;
            return true;
        }
    }
}