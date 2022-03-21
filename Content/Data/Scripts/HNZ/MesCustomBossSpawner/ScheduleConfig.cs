using System;
using System.Xml.Serialization;
using HNZ.Utils;
using HNZ.Utils.Logging;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class ScheduleConfig
    {
        static readonly Logger Log = LoggerManager.Create(nameof(ScheduleConfig));

        float _intervalHours;

        [XmlAttribute]
        public float OffsetHours;

        [XmlAttribute]
        public float IntervalHours
        {
            get { return _intervalHours; }
            set { _intervalHours = value <= 0 ? 24 : value; }
        }

        [XmlAttribute]
        public float SpanHours;

        public bool IsOpen(DateTime dateTime, out float countdownHours)
        {
            var hours = (float)dateTime.TimeSpanSinceMidnight().TotalHours;
            var hoursSinceInterval = MathUtils.PositiveMod(hours - OffsetHours, IntervalHours);
            if (hoursSinceInterval <= SpanHours) // open
            {
                countdownHours = SpanHours - hoursSinceInterval;
                Log.Debug($"closing: {hours} {hoursSinceInterval} {SpanHours} {IntervalHours}");
                return true;
            }
            else //closed
            {
                countdownHours = IntervalHours - hoursSinceInterval;
                Log.Debug($"opening: {hours} {hoursSinceInterval} {SpanHours} {IntervalHours}");
                return false;
            }
        }
    }
}