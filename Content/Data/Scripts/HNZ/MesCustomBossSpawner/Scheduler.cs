using System;
using HNZ.Utils.Logging;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Scheduler
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Scheduler));

        DateTime _lastUpdateTime;
        readonly Schedule[] _schedules;

        public Scheduler(Schedule[] schedules)
        {
            _schedules = schedules;
        }

        public float? Countdown { get; private set; }

        public void Initialize(DateTime now)
        {
            _lastUpdateTime = now;
        }

        public bool Update(DateTime now)
        {
            if (_schedules.Length == 0)
            {
                Countdown = null;
                return false;
            }

            var frameChanged = false;
            Countdown = float.MaxValue;
            foreach (var schedule in _schedules)
            {
                frameChanged |= schedule.TestFrameChanged(now, _lastUpdateTime);
                Countdown = Math.Min(Countdown.Value, schedule.GetCountdown(now));
            }

            _lastUpdateTime = now;

            return frameChanged;
        }
    }
}