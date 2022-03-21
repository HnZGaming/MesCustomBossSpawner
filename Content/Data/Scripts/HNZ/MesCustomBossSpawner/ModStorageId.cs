using System;
using System.Xml.Serialization;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRage.Game.Components;
using VRage.Utils;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class ModStorageId
    {
        static readonly Logger Log = LoggerManager.Create(nameof(ModStorageId));

        Guid _keyGuid;

        [XmlAttribute]
        public string Key
        {
            get { return _keyGuid.ToString(); }
            set { _keyGuid = Guid.Parse(value); }
        }

        [XmlAttribute]
        public string Value;

        public bool Test(MyModStorageComponentBase storage)
        {
            if (storage == null) return false;

            if (Log.Severity <= MyLogSeverity.Debug)
            {
                Log.Debug($"mes spawn: {storage.Keys.SeqToString()}, {storage.Values.SeqToString()}");
            }

            string value;
            return storage.TryGetValue(_keyGuid, out value) && value == Value;
        }
    }
}