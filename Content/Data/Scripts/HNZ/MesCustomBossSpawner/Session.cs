using System;
using System.Collections.Generic;
using HNZ.FlashGps.Interface;
using HNZ.MES;
using HNZ.Utils;
using HNZ.Utils.Communications;
using HNZ.Utils.Logging;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public sealed class Session : MySessionComponentBase, ICommandListener
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Session));

        readonly Dictionary<string, Action<Command>> _serverCommands;
        readonly Dictionary<string, BossSpawner> _bossSpawners;

        ContentFile<Config> _configFile;
        ProtobufModule _protobufModule;
        CommandModule _commandModule;
        MESApi _mesApi;
        FlashGpsApi _localGpsApi;

        public Session()
        {
            _serverCommands = new Dictionary<string, Action<Command>>
            {
                { "reload", Command_Reload },
                { "spawn", Command_Spawn },
                { "despawn", Command_Despawn },
            };

            _bossSpawners = new Dictionary<string, BossSpawner>();
        }

        public override void LoadData()
        {
            Log.Info("init");

            LoggerManager.SetPrefix(nameof(MesCustomBossSpawner));

            _protobufModule = new ProtobufModule((ushort)nameof(MesCustomBossSpawner).GetHashCode());
            _protobufModule.Initialize();

            _commandModule = new CommandModule(_protobufModule, 1, "cbs", this);
            _commandModule.Initialize();

            if (!MyAPIGateway.Session.IsServer) return;

            _mesApi = new MESApi();
            _localGpsApi = new FlashGpsApi(nameof(MesCustomBossSpawner).GetHashCode());

            ReloadConfig();
        }

        protected override void UnloadData()
        {
            _protobufModule.Close();
            _commandModule.Close();

            if (!MyAPIGateway.Session.IsServer) return;

            foreach (var bossSpawner in _bossSpawners)
            {
                bossSpawner.Value.Close();
            }
        }

        public override void UpdateBeforeSimulation()
        {
            _protobufModule.Update();
            _commandModule.Update();

            if (!MyAPIGateway.Session.IsServer) return;

            foreach (var bossSpawner in _bossSpawners)
            {
                bossSpawner.Value.Update();
            }
        }

        void ReloadConfig()
        {
            _configFile = new ContentFile<Config>("CustomBossSpawner.cfg", Config.CreateDefault());
            _configFile.ReadOrCreateFile();
            Config.Instance = _configFile.Content;
            Config.Instance.TryInitialize();
            _configFile.WriteFile(); // fills missing fields
            LoggerManager.SetConfigs(Config.Instance.Logs);

            foreach (var bossSpawner in _bossSpawners)
            {
                bossSpawner.Value.TryCleanup();
                bossSpawner.Value.Close();
            }

            _bossSpawners.Clear();

            foreach (var boss in Config.Instance.Bosses)
            {
                var bossSpawner = new BossSpawner(_mesApi, _localGpsApi, boss);
                bossSpawner.Initialize();
                _bossSpawners.Add(boss.Id, bossSpawner);
            }
        }

        bool ICommandListener.ProcessCommandOnClient(Command command)
        {
            if (!MyAPIGateway.Session.IsUserAdmin(command.SteamId))
            {
                command.Respond("CBS", Color.Red, "admin only");
                return true;
            }

            return false;
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
            string id;
            BossSpawner bossSpawner;
            if (command.Arguments.TryGetFirstValue(out id) &&
                _bossSpawners.TryGetValue(id, out bossSpawner))
            {
                var result = bossSpawner.TrySpawn();
                command.Respond("CBS", Color.White, $"spawn result: {result}");
            }
            else
            {
                command.Respond("CBS", Color.Red, $"invalid id: {id}");
            }
        }

        void Command_Despawn(Command command)
        {
            string id;
            BossSpawner bossSpawner;
            if (command.Arguments.TryGetFirstValue(out id) &&
                _bossSpawners.TryGetValue(id, out bossSpawner))
            {
                bossSpawner.TryCleanup();
                command.Respond("CBS", Color.White, "despawn command");
            }
            else
            {
                command.Respond("CBS", Color.Red, $"invalid id: {id}");
            }
        }
    }
}