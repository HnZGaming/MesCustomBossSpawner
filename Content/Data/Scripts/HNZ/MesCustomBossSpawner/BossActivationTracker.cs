using System;
using System.Collections.Generic;
using HNZ.Utils.Logging;
using Sandbox.ModAPI;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class BossActivationTracker
    {
        static readonly Logger Log = LoggerManager.Create(nameof(BossActivationTracker));
        public static readonly BossActivationTracker Instance = new BossActivationTracker();
        const string Key = "BossActivationTracker";

        readonly Dictionary<string, Vector3D> _activations;
        bool _isDirty;

        public BossActivationTracker()
        {
            _activations = new Dictionary<string, Vector3D>();
        }

        public void Load()
        {
            string dataStr;
            if (MyAPIGateway.Utilities.GetVariable(Key, out dataStr))
            {
                var data = Convert.FromBase64String(dataStr);
                var activities = MyAPIGateway.Utilities.SerializeFromBinary<Dictionary<string, Vector3D>>(data);
                foreach (var kvp in activities)
                {
                    _activations[kvp.Key] = kvp.Value;
                }

                Log.Info($"BossActivationTracker.Load() existing data loaded; count: {activities.Count}");
            }
            else
            {
                Log.Info("BossActivationTracker.Load() new data");
            }
        }

        public void Update()
        {
            if (_isDirty)
            {
                var data = MyAPIGateway.Utilities.SerializeToBinary(_activations);
                var dataStr = Convert.ToBase64String(data);
                MyAPIGateway.Utilities.SetVariable(Key, dataStr);
                _isDirty = false;
                Log.Info($"BossActivationTracker.Update(); count: {_activations.Count}");
            }
        }

        public void OnActivate(string id, Vector3D position)
        {
            _activations[id] = position;
            _isDirty = true;
            Log.Info($"BossActivationTracker.OnActivate({id}, {position})");
        }

        public void OnInvalidate(string id)
        {
            _activations.Remove(id);
            _isDirty = true;
            Log.Info($"BossActivationTracker.OnInvalidate({id})");
        }

        public bool TryGetActivationPosition(string id, out Vector3D position)
        {
            return _activations.TryGetValue(id, out position);
        }
    }
}