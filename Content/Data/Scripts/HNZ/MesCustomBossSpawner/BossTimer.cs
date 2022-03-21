using System;
using HNZ.Utils;
using HNZ.Utils.Logging;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class BossTimer
    {
        static readonly Logger Log = LoggerManager.Create(nameof(BossTimer));

        bool _wasOpen;

        public bool IsOpen { get; private set; }
        public float CountdownHours { get; private set; }
        public event Action OnOpen;
        public event Action OnClose;

        public void Update()
        {
            foreach (var schedule in Config.Instance.Schedules)
            {
                float countdownHours;
                IsOpen = schedule.IsOpen(DateTime.Now, out countdownHours);
                CountdownHours = countdownHours;

                if (!_wasOpen && IsOpen)
                {
                    Log.Info("boss timer opened");
                    OnOpen?.Invoke();
                }

                if (_wasOpen && !IsOpen)
                {
                    Log.Info("boss timer closed");
                    OnClose?.Invoke();
                }

                _wasOpen = IsOpen;
            }

            Log.Debug($"open: {IsOpen}, countdown: {LangUtils.HoursToString(CountdownHours)}");
        }
    }
}