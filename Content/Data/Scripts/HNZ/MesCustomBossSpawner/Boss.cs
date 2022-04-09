using System;
using System.Xml.Serialization;
using HNZ.Utils;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Boss
    {
        [XmlElement]
        public string Id;

        [XmlElement]
        public bool Enabled;

        [XmlElement]
        public string SpawnGroup;

        [XmlElement]
        public string FactionTag;

        [XmlElement]
        public ModStorageEntry ModStorageId;

        [XmlElement]
        public string CountdownGpsName;

        [XmlElement]
        public string CountdownGpsDescription;

        [XmlElement]
        public float SpawnRadius;

        [XmlElement]
        public double ClearanceRadius;

        [XmlArray]
        public Schedule[] Schedules;

        public void TryInitialize()
        {
            LangUtils.AssertNull(Id, nameof(Id));
            LangUtils.AssertNull(SpawnGroup, nameof(SpawnGroup));
            LangUtils.AssertNull(ModStorageId, nameof(ModStorageId));
            LangUtils.NullOrDefault(ref CountdownGpsName, "");
            LangUtils.NullOrDefault(ref CountdownGpsDescription, "");
            LangUtils.NullOrDefault(ref Schedules, Array.Empty<Schedule>());
        }

        public static Boss CreateDefault() => new Boss
        {
            Id = "Bababooey",
            Enabled = true,
            SpawnGroup = "Porks-SpawnGroup-Boss-BigMekKrooza",
            ModStorageId = new ModStorageEntry
            {
                Key = "b97e4f0d-6a55-4dcf-a471-448132e68e82",
                Value = "Bababooey",
            },
            SpawnRadius = 2000000,
            ClearanceRadius = 1000,
            CountdownGpsName = "Spawning in {0}",
            CountdownGpsDescription = "Spawning very soon",
            Schedules = new[]
            {
                new Schedule
                {
                    OffsetHours = 0,
                    IntervalHours = 0.01f,
                },
            },
        };
    }
}