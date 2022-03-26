using System;
using System.Xml.Serialization;
using HNZ.Utils;
using HNZ.Utils.Logging;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Schedule
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Schedule));

        float _intervalHours;

        [XmlAttribute]
        public float OffsetHours;

        [XmlAttribute]
        public float IntervalHours
        {
            get { return _intervalHours <= 0 ? 24 : _intervalHours; }
            set { _intervalHours = value; }
        }

        public bool TestFrameChanged(DateTime currentTime, DateTime lastTime)
        {
            return GetFrameIndex(currentTime) != GetFrameIndex(lastTime);
        }

        int GetFrameIndex(DateTime currentTime)
        {
            var t = (float)currentTime.TimeSpanSinceMidnight().TotalHours;
            return (int)Math.Floor((OffsetHours - t) / IntervalHours);
        }

        public float GetCountdown(DateTime currentTime)
        {
            var t = (float)currentTime.TimeSpanSinceMidnight().TotalHours;
            return MathUtils.PositiveMod(OffsetHours - t, IntervalHours);
        }
    }
}