using System;
using System.Linq;
using HNZ.FlashGps.Interface;
using HNZ.Nexus;
using HNZ.Utils;
using HNZ.Utils.Logging;
using Sandbox.ModAPI;

namespace HNZ.MesCustomBossSpawner
{
    public sealed class NexusGpsClient
    {
        static readonly Logger Log = LoggerManager.Create(nameof(NexusGpsClient));
        readonly NexusAPI _nexusApi;

        public NexusGpsClient()
        {
            _nexusApi = new NexusAPI(7568);
        }

        public event Action<FlashGpsSource> OnGpsAddedOrUpdated;
        public event Action<long> OnGpsRemoved;

        public void Initialize()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_nexusApi.CrossServerModID, OnReceived);
        }

        public void Close()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_nexusApi.CrossServerModID, OnReceived);
        }

        public void AddOrUpdate(FlashGpsSource gps)
        {
            var data = MyAPIGateway.Utilities.SerializeToBinary(gps);
            data = new byte[] { 1 }.Concat(data).ToArray();
            _nexusApi.SendMessageToAllServers(data);
        }

        public void Remove(long gpsId)
        {
            var data = MyAPIGateway.Utilities.SerializeToBinary(gpsId);
            data = new byte[] { 2 }.Concat(data).ToArray();
            _nexusApi.SendMessageToAllServers(data);
        }

        void OnReceived(ushort handlerId, byte[] messageSentBytes, ulong senderPlayerId, bool isArrivedFromServer)
        {
            if (!isArrivedFromServer) return;

            var kind = messageSentBytes[0];
            var data = messageSentBytes.Skip(1).ToArray();

            switch (kind)
            {
                case 1:
                {
                    Log.Debug($"kind: 1, data: {data.SeqToString()}");
                    var gps = MyAPIGateway.Utilities.SerializeFromBinary<FlashGpsSource>(data);
                    OnGpsAddedOrUpdated?.Invoke(gps);
                    return;
                }
                case 2:
                {
                    Log.Debug($"kind: 2, data: {data.SeqToString()}");
                    var gpsId = MyAPIGateway.Utilities.SerializeFromBinary<long>(data);
                    OnGpsRemoved?.Invoke(gpsId);
                    return;
                }
                default:
                {
                    throw new InvalidOperationException($"unknown kind: {kind}");
                }
            }
        }
    }
}