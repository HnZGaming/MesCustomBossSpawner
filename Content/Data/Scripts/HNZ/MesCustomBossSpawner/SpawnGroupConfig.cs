using System;
using System.Xml.Serialization;

namespace HNZ.MesCustomBossSpawner
{
    [Serializable]
    public sealed class SpawnGroupConfig
    {
        [XmlText]
        public string SpawnGroupName = "Porks-SpawnGroup-Boss-BigMekKrooza";

        [XmlAttribute]
        public float Weight = 1;

        [XmlAttribute]
        public string MainPrefab = "";
    }
}