using System;
using HNZ.Utils.Logging;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class Scheduler
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Scheduler));

        DateTime? _lastUpdateTime;

        public float? Countdown { get; private set; }

        public bool Update(DateTime now)
        {
            if (_lastUpdateTime == null)
            {
                _lastUpdateTime = now;
                return false;
            }

            if (Config.Instance.Schedules.Length == 0)
            {
                Countdown = null;
                return false;
            }

            var frameChanged = false;
            Countdown = float.MaxValue;
            foreach (var schedule in Config.Instance.Schedules)
            {
                frameChanged |= schedule.TestFrameChanged(now, _lastUpdateTime.Value);
                Countdown = Math.Min(Countdown.Value, schedule.GetCountdown(now));
            }

            _lastUpdateTime = now;

            return frameChanged;
        }
    }
}