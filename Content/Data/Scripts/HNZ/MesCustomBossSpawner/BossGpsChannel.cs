using HNZ.FlashGps.Interface;

namespace HNZ.MesCustomBossSpawner
{
    //todo move to Flash GPS
    public sealed class BossGpsChannel
    {
        readonly FlashGpsApi _flashGpsApi;
        readonly NexusGpsClient _nexus;

        public BossGpsChannel(FlashGpsApi flashGpsApi)
        {
            _flashGpsApi = flashGpsApi;
            _nexus = new NexusGpsClient();
        }

        public void Initialize()
        {
            _nexus.Initialize();
            _nexus.OnGpsAddedOrUpdated += OnNexusGpsAddedOrUpdated;
            _nexus.OnGpsRemoved += OnNexusGpsRemoved;
        }

        public void Close()
        {
            _nexus.Close();
            _nexus.OnGpsAddedOrUpdated -= OnNexusGpsAddedOrUpdated;
            _nexus.OnGpsRemoved -= OnNexusGpsRemoved;
        }

        public void AddOrUpdate(FlashGpsSource gps)
        {
            _flashGpsApi.AddOrUpdate(gps);
            _nexus.AddOrUpdate(gps);
        }

        void OnNexusGpsAddedOrUpdated(FlashGpsSource gps)
        {
            _flashGpsApi.AddOrUpdate(gps);
        }

        void OnNexusGpsRemoved(long gpsId)
        {
            _flashGpsApi.Remove(gpsId);
        }

        public void Remove(long gpsId)
        {
            _flashGpsApi.Remove(gpsId);
            _nexus.Remove(gpsId);
        }
    }
}