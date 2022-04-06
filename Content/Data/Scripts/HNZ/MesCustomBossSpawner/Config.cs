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

        [XmlArray]
        public Boss[] Bosses;

        [XmlElement]
        public LogConfig[] Logs;

        public void TryInitialize()
        {
            LangUtils.NullOrDefault(ref Bosses, Array.Empty<Boss>());
            LangUtils.NullOrDefault(ref Logs, Array.Empty<LogConfig>());

            foreach (var boss in Bosses)
            {
                boss.TryInitialize();
            }
        }

        public static Config CreateDefault() => new Config
        {
            Bosses = new[]
            {
                Boss.CreateDefault()
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