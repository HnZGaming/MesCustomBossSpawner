using System;
using System.Collections.Generic;
using HNZ.MES;
using HNZ.Utils;
using HNZ.Utils.Communications;
using HNZ.Utils.Logging;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public sealed class Session : MySessionComponentBase, ICommandListener
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Session));
        ContentFile<Config> _configFile;
        Dictionary<string, Action<Command>> _serverCommands;
        ProtobufModule _protobufModule;
        CommandModule _commandModule;
        MESApi _mesApi;
        BossTimer _bossTimer;
        IMyCubeGrid _bossGrid;
        bool _runOnce;
        bool _cleanupIgnored;

        public override void LoadData()
        {
            Log.Info("init");

            LoggerManager.SetPrefix(nameof(MesCustomBossSpawner));

            _protobufModule = new ProtobufModule((ushort)nameof(LocalGps).GetHashCode());
            _protobufModule.Initialize();

            _commandModule = new CommandModule(_protobufModule, 1, "cbs", this);
            _commandModule.Initialize();

            if (!MyAPIGateway.Session.IsServer) return;

            _serverCommands = new Dictionary<string, Action<Command>>
            {
                { "reload", Command_Reload },
                { "spawn", Command_Spawn },
            };

            _mesApi = new MESApi();

            _bossTimer = new BossTimer();
            _bossTimer.OnOpen += OnBossTimeOpen;
            _bossTimer.OnClose += OnBossTimeClose;

            ReloadConfig();
        }

        protected override void UnloadData()
        {
            _protobufModule.Close();
            _commandModule.Close();

            if (!MyAPIGateway.Session.IsServer) return;

            _bossTimer.OnOpen -= OnBossTimeOpen;
            _bossTimer.OnClose -= OnBossTimeClose;
            _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
        }

        public override void UpdateBeforeSimulation()
        {
            _protobufModule.Update();
            _commandModule.Update();

            if (!MyAPIGateway.Session.IsServer) return;

            if (LangUtils.RunOnce(ref _runOnce))
            {
                _mesApi.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
            }

            if (GameUtils.EverySeconds(1))
            {
                _bossTimer.Update();

                if (!_bossTimer.IsOpen && !_bossGrid.IsNullOrClosed())
                {
                    OnBossTimeClose();
                }
            }

            if (!_cleanupIgnored && !_bossGrid.IsNullOrClosed())
            {
                _cleanupIgnored = _mesApi.SetSpawnerIgnoreForDespawn(_bossGrid, true);
                if (_cleanupIgnored)
                {
                    Log.Info("cleanup ignored");
                }
            }
        }

        void ReloadConfig()
        {
            _configFile = new ContentFile<Config>("Config.cfg", Config.CreateDefault());
            _configFile.ReadOrCreateFile();
            Config.Instance = _configFile.Content;
            LoggerManager.SetConfigs(Config.Instance.Logs);
        }

        void ICommandListener.ProcessCommandOnServer(Command command)
        {
            _serverCommands.GetValueOrDefault(command.Header, null)?.Invoke(command);
        }

        void Command_Reload(Command command)
        {
            ReloadConfig();
            command.Respond("CBS", Color.White, "config reloaded");
        }

        void Command_Spawn(Command command)
        {
            var name = command.Arguments[0];

            IMyPlayer player;
            if (!GameUtils.TryGetPlayerBySteamId(command.SteamId, out player))
            {
                command.Respond("CBS", Color.Yellow, "player not found");
                return;
            }

            var pos = player.Character.GetPosition();
            var result = TryBossSpawn(name, pos, true);

            command.Respond("CBS", Color.White, $"spawn result: {result}");
        }

        void OnBossTimeOpen()
        {
            Vector3D position;
            if (!TryGetSpawnPosition(out position))
            {
                Log.Warn("spawn failed: no space");
                return;
            }

            if (TryBossSpawn(Config.Instance.SpawnGroup, position, true))
            {
                Log.Info("boss spawn success");
            }
            else
            {
                Log.Warn("boss spawn fail");
            }
        }

        void OnBossTimeClose()
        {
            if (!_bossGrid.IsNullOrClosed())
            {
                // don't delete the boss if players are around it
                var pos = _bossGrid.GetPosition();
                var sphere = new BoundingSphereD(pos, 10000);
                if (GameUtils.GetPlayerCharacterCountInSphere(sphere) > 0) return;

                CloseBossGrid();
            }
        }

        bool TryBossSpawn(string spawnGroup, Vector3D position, bool ignoreSafetyCheck)
        {
            CloseBossGrid();

            Log.Info($"spawning boss: {spawnGroup} at {position}");

            var matrix = MatrixD.CreateWorld(position, Vector3D.Forward, Vector3D.Up);
            return _mesApi.CustomSpawnRequest(new List<string> { spawnGroup }, matrix, Vector3.Zero, ignoreSafetyCheck, null, nameof(MesCustomBossSpawner));
        }

        void CloseBossGrid()
        {
            if (!_bossGrid.IsNullOrClosed())
            {
                _bossGrid.Close();
                Log.Info("closed the boss grid");
            }
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (Config.Instance.ModStorageId.Test(grid.Storage))
            {
                _bossGrid = grid;
                _cleanupIgnored = false;
                Log.Info($"Boss spawn found: {grid.DisplayName}");

                //todo need test
                _mesApi.RegisterDespawnWatcher(_bossGrid, OnBossDispawned);
            }
        }

        void OnBossDispawned(IMyCubeGrid grid, string type)
        {
            Log.Info($"Boss dispawned: {grid.DisplayName}, cause: {type}");
            _bossGrid = null;
        }

        bool TryGetSpawnPosition(out Vector3D position)
        {
            for (var i = 0; i < 100; i++)
            {
                // get a random position
                position = MathUtils.GetRandomUnitDirection() * Config.Instance.SpawnRadius;

                // check for gravity
                float gravityInterference;
                var gravity = MyAPIGateway.Physics.CalculateNaturalGravityAt(position, out gravityInterference);
                if (gravity != Vector3.Zero) continue;

                // check for space
                var sphere = new BoundingSphereD(position, Config.Instance.ClearanceRadius);
                if (GameUtils.GetEntityCountInSphere(sphere) > 0) continue;

                return true;
            }

            position = default(Vector3D);
            return false;
        }
    }
}