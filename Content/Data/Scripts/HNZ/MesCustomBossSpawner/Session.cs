using System;
using System.Collections.Generic;
using System.Linq;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Communications;
using HNZ.Utils.Logging;
using HNZ.Utils.MES;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once UnusedType.Global
    public sealed class Session : MySessionComponentBase, ICommandListener
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Session));

        readonly Dictionary<string, Action<Command>> _serverCommands;
        readonly Dictionary<string, Boss> _bosses;
        readonly SceneEntityCachedCollection<IMyCubeGrid> _grids;

        ContentFile<Config> _configFile;
        ProtobufModule _protobufModule;
        CommandModule _commandModule;
        MESApi _mesApi;
        FlashGpsApi _gpsApi;
        bool _runOnce;

        public Session()
        {
            _serverCommands = new Dictionary<string, Action<Command>>
            {
                { "reload", Command_Reload },
                { "enabled", Command_Enabled },
                { "spawn", Command_Spawn },
                { "despawn", Command_Despawn },
                { "reset", Command_ResetPosition },
            };

            _bosses = new Dictionary<string, Boss>();
            _grids = new SceneEntityCachedCollection<IMyCubeGrid>();
        }

        public override void LoadData()
        {
            Log.Info("init");

            LoggerManager.SetPrefix(nameof(MesCustomBossSpawner));

            _protobufModule = new ProtobufModule((ushort)nameof(MesCustomBossSpawner).GetHashCode());
            _protobufModule.Initialize();

            _commandModule = new CommandModule(_protobufModule, 1, "cbs", this);
            _commandModule.Initialize();

            if (MyAPIGateway.Session.IsServer)
            {
                _mesApi = new MESApi();
                _gpsApi = new FlashGpsApi(nameof(MesCustomBossSpawner).GetHashCode());

                PlanetCollection.Initialize();
                _grids.Initialize();

                ReloadConfig();
            }
        }

        protected override void UnloadData()
        {
            _protobufModule.Close();
            _commandModule.Close();

            if (!MyAPIGateway.Session.IsServer) return;

            foreach (var boss in _bosses.Values)
            {
                boss.Close("UnloadData");
            }

            PlanetCollection.Close();
            _grids.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            _protobufModule.Update();
            _commandModule.Update();

            if (!MyAPIGateway.Session.IsServer) return;

            // load existing boss grids
            if (!_runOnce)
            {
                _runOnce = true;

                var grids = _grids.ApplyChanges();
                foreach (var boss in _bosses.Values)
                {
                    boss.OnFirstFrame(grids);
                }

                _grids.Close();
            }

            foreach (var kvp in _bosses)
            {
                kvp.Value.Update();
            }
        }

        void ReloadConfig()
        {
            Log.Info("Reloading config");

            _configFile = new ContentFile<Config>("CustomBossSpawner.cfg", Config.CreateDefault());
            _configFile.ReadOrCreateFile();
            Config.Instance = _configFile.Content;
            Config.Instance.TryInitialize();
            _configFile.WriteFile(); // fill missing fields
            LoggerManager.SetConfigs(Config.Instance.Logs);

            foreach (var bossGrid in _bosses.Values)
            {
                bossGrid.Close("ReloadConfig");
            }

            _bosses.Clear();

            foreach (var bossInfo in Config.Instance.Bosses)
            {
                var boss = new Boss(_mesApi, _gpsApi, bossInfo);
                boss.Initialize();
                _bosses.Add(bossInfo.Id, boss);
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
            if (!GameUtils.IsAdmin(command.SteamId))
            {
                command.Respond("CBS", Color.Red, "not admin");
                return;
            }

            ReloadConfig();
            command.Respond("CBS", Color.White, $"config reloaded; entries: {Config.Instance.Bosses.Select(b => b.Id).SeqToString()}");
        }

        void Command_Enabled(Command command)
        {
            if (!GameUtils.IsAdmin(command.SteamId))
            {
                command.Respond("CBS", Color.Red, "not admin");
                return;
            }

            string arg;
            if (!command.Arguments.TryGetFirstValue(out arg))
            {
                command.Respond("CBS", Color.Red, "no arg");
                return;
            }

            bool enabled;
            if (!bool.TryParse(arg, out enabled))
            {
                command.Respond("CBS", Color.Red, "invalid arg");
                return;
            }

            Config.Instance.Enabled = enabled;
            command.Respond("CBS", Color.White, $"spawning enabled: {enabled}");
        }

        void Command_Spawn(Command command)
        {
            if (!GameUtils.IsAdmin(command.SteamId))
            {
                command.Respond("CBS", Color.Red, "not admin");
                return;
            }

            string id;
            Boss boss;
            if (command.Arguments.TryGetFirstValue(out id) &&
                _bosses.TryGetValue(id, out boss))
            {
                var result = boss.TrySpawn();
                command.Respond("CBS", Color.White, $"spawn result: {result}");
            }
            else
            {
                command.Respond("CBS", Color.Red, $"invalid id: {id}");
            }
        }

        void Command_Despawn(Command command)
        {
            if (!GameUtils.IsAdmin(command.SteamId))
            {
                command.Respond("CBS", Color.Red, "not admin");
                return;
            }

            string id;
            Boss boss;
            if (command.Arguments.TryGetFirstValue(out id) &&
                _bosses.TryGetValue(id, out boss))
            {
                boss.Close("Command_Despawn");
                command.Respond("CBS", Color.White, "despawn command");
            }
            else
            {
                command.Respond("CBS", Color.Red, $"invalid id: {id}");
            }
        }

        void Command_ResetPosition(Command command)
        {
            if (!GameUtils.IsAdmin(command.SteamId))
            {
                command.Respond("CBS", Color.Red, "not admin");
                return;
            }

            foreach (var boss in _bosses.Values)
            {
                boss.ResetSpawningPosition();
            }

            command.Respond("CBS", Color.White, "position reset");
        }
    }
}