using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class BossActivationTracker
    {
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

                MyLog.Default.Info($"[CBS] BossActivationTracker.Load() existing data loaded; count: {activities.Count}");
            }
            else
            {
                MyLog.Default.Info("[CBS] BossActivationTracker.Load() new data");
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
                MyLog.Default.Info($"[CBS] BossActivationTracker.Update(); count: {_activations.Count}");
            }
        }

        public void OnActivate(string id, Vector3D position)
        {
            _activations[id] = position;
            _isDirty = true;
            MyLog.Default.Info($"[CBS] BossActivationTracker.OnActivate({id}, {position})");
        }

        public void OnInvalidate(string id)
        {
            _activations.Remove(id);
            _isDirty = true;
            MyLog.Default.Info($"[CBS] BossActivationTracker.OnInvalidate({id})");
        }

        public bool TryGetActivationPosition(string id, out Vector3D position)
        {
            return _activations.TryGetValue(id, out position);
        }
    }
}