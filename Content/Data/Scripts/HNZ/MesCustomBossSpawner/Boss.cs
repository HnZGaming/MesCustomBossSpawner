﻿using System;
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
        public bool PlanetSpawn;

        [XmlElement]
        public string CountdownGpsName;

        [XmlElement]
        public string CountdownGpsDescription;

        [XmlElement]
        public Sphere SpawnSphere;

        [XmlElement]
        public float ClearanceRadius;

        [XmlElement]
        public float GpsRadius;

        [XmlArray]
        public Schedule[] Schedules;

        public void TryInitialize()
        {
            LangUtils.AssertNull(Id, nameof(Id));
            LangUtils.AssertNull(SpawnGroup, nameof(SpawnGroup));
            LangUtils.NullOrDefault(ref SpawnSphere, new Sphere());
            LangUtils.NullOrDefault(ref CountdownGpsName, "");
            LangUtils.NullOrDefault(ref CountdownGpsDescription, "");
            LangUtils.NullOrDefault(ref Schedules, Array.Empty<Schedule>());
        }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(SpawnGroup)}: {SpawnGroup}";
        }

        public static Boss CreateDefault() => new Boss
        {
            Id = "Bababooey",
            Enabled = true,
            SpawnGroup = "Porks-SpawnGroup-Boss-BigMekKrooza",
            PlanetSpawn = false,
            SpawnSphere = new Sphere
            {
                X = 0,
                Y = 0,
                Z = 0,
                Radius = 2000000,
            },
            ClearanceRadius = 1000,
            GpsRadius = 500000,
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