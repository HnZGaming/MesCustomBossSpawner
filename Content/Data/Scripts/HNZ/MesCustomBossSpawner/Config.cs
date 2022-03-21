using System.Xml.Serialization;
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
        public ModStorageId ModStorageId;

        [XmlElement]
        public float SpawnRadius;

        [XmlElement]
        public double ClearanceRadius;

        [XmlElement]
        public ScheduleConfig[] Schedules;

        [XmlElement]
        public LogConfig[] Logs;

        public static Config CreateDefault() => new Config
        {
            SpawnGroup = "Porks-SpawnGroup-Boss-BigMekKrooza",
            ModStorageId = new ModStorageId
            {
                Key = "b97e4f0d-6a55-4dcf-a471-448132e68e82",
                Value = "Bababooey",
            },
            SpawnRadius = 2000000,
            ClearanceRadius = 1000,
            Schedules = new[]
            {
                new ScheduleConfig
                {
                    OffsetHours = 0,
                    IntervalHours = 0.01f,
                    SpanHours = 0.01f,
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