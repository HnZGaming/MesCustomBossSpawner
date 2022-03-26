using System;
using System.Xml.Serialization;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRage.Utils;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Config
    {
        public static Config Instance { get; set; }

        [XmlElement]
        public string SpawnGroup;

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

        [XmlElement]
        public Schedule[] Schedules;

        [XmlElement]
        public LogConfig[] Logs;

        public void TryInitialize()
        {
            LangUtils.AssertNull(SpawnGroup);
            LangUtils.AssertNull(ModStorageId);
            LangUtils.NullOrDefault(ref CountdownGpsName, "");
            LangUtils.NullOrDefault(ref CountdownGpsDescription, "");
            LangUtils.NullOrDefault(ref Schedules, Array.Empty<Schedule>());
            LangUtils.NullOrDefault(ref Logs, Array.Empty<LogConfig>());
        }

        public static Config CreateDefault() => new Config
        {
            SpawnGroup = "Porks-SpawnGroup-Boss-BigMekKrooza",
            ModStorageId = new ModStorageEntry
            {
                Key = "b97e4f0d-6a55-4dcf-a471-448132e68e82",
                Value = "Bababooey",
            },
            SpawnRadius = 2000000,
            ClearanceRadius = 1000,
            Schedules = new[]
            {
                new Schedule
                {
                    OffsetHours = 0,
                    IntervalHours = 0.01f,
                },
            },
            Logs = new[]
            {
                new LogConfig
                {
                    Severity = MyLogSeverity.Info,
                    Prefix = "",
                }
            }
        };
    }
}