using System;
using System.Collections.Generic;
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
        ContentFile<Config> _configFile;
        Dictionary<string, Action<Command>> _serverCommands;
        ProtobufModule _protobufModule;
        CommandModule _commandModule;
        Scheduler _scheduler;
        Core _core;

        public override void LoadData()
        {
            Log.Info("init");

            LoggerManager.SetPrefix(nameof(MesCustomBossSpawner));

            _protobufModule = new ProtobufModule((ushort)nameof(MesCustomBossSpawner).GetHashCode());
            _protobufModule.Initialize();

            _commandModule = new CommandModule(_protobufModule, 1, "cbs", this);
            _commandModule.Initialize();

            if (!MyAPIGateway.Session.IsServer) return;

            _serverCommands = new Dictionary<string, Action<Command>>
            {
                { "reload", Command_Reload },
                { "spawn", Command_Spawn },
                { "despawn", Command_Despawn },
            };

            _scheduler = new Scheduler();
            _core = new Core(_scheduler);
            _core.Initialize();

            ReloadConfig();
        }

        protected override void UnloadData()
        {
            _protobufModule.Close();
            _commandModule.Close();

            if (!MyAPIGateway.Session.IsServer) return;

            _core.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            _protobufModule.Update();
            _commandModule.Update();

            if (!MyAPIGateway.Session.IsServer) return;

            _core.Update();
        }

        void ReloadConfig()
        {
            _configFile = new ContentFile<Config>("Config.cfg", Config.CreateDefault());
            _configFile.ReadOrCreateFile();
            Config.Instance = _configFile.Content;
            Config.Instance.TryInitialize();
            LoggerManager.SetConfigs(Config.Instance.Logs);
        }

        bool ICommandListener.ProcessCommandOnClient(Command command)
        {
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
            var result = _core.TrySpawn();
            command.Respond("CBS", Color.White, $"spawn result: {result}");
        }

        void Command_Despawn(Command command)
        {
            _core.TryCleanup();
            command.Respond("CBS", Color.White, "despawn command");
        }
    }
}