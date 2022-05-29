using System;
using System.Xml.Serialization;
using HNZ.Utils;
using HNZ.Utils.Logging;
using VRage.Utils;
using VRageMath;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Config
    {
        public static Config Instance { get; set; }

        [XmlElement]
        public bool Enabled { get; set; }

        [XmlArray]
        public Boss[] Bosses;

        [XmlArray]
        public Sphere[] SpawnVoids;

        [XmlElement]
        public LogConfig[] Logs;

        public void TryInitialize()
        {
            Enabled = true;
            LangUtils.NullOrDefault(ref Bosses, Array.Empty<Boss>());
            LangUtils.NullOrDefault(ref Logs, Array.Empty<LogConfig>());
            LangUtils.NullOrDefault(ref SpawnVoids, Array.Empty<Sphere>());

            foreach (var boss in Bosses)
            {
                boss.TryInitialize();
            }
        }

        public static Config CreateDefault() => new Config
        {
            Enabled = true,
            Bosses = new[]
            {
                Boss.CreateDefault()
            },
            SpawnVoids = new[]
            {
                new Sphere(),
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

        public bool IntersectsAnySpawnVoids(BoundingSphereD sphere)
        {
            foreach (var voidSphere in SpawnVoids)
            {
                if (sphere.Contains(voidSphere) != ContainmentType.Disjoint)
                {
                    return true;
                }
            }

            return false;
        }
    }
}