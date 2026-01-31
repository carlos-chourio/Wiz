using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using WiZ.NET.Models;
using WiZ.Observable;
using WiZ.NET.Services;

namespace WiZ.NET.Profiles
{
    public class BulbItem : IBulb
    {
        [KeyProperty]
        [JsonProperty("mac")]
        public virtual MACAddress MACAddress { get; set; }

        [JsonProperty("addr")]
        public virtual IPAddress IPAddress { get; set; }

        [JsonProperty("port")]
        public virtual int Port { get; set; }

        [JsonProperty("name")]
        public virtual string Name { get; set; }

        [JsonProperty("icon")]
        public virtual string Icon { get; set; }

        [JsonProperty("roomId")]
        public int? RoomId { get; set; }

        [JsonProperty("homeId")]
        public int? HomeId { get; set; }

        public static BulbItem CreateItemFromBulb(IBulb source)
        {
            return new BulbItem()
            {
                MACAddress = source.MACAddress,
                IPAddress = source.IPAddress,
                Port = source.Port,
                Name = source.Name,
                HomeId = source.HomeId,
                RoomId = source.RoomId
            };
        }

        public static async Task<IList<BulbModel>> CreateBulbsFromInterfaceList(IEnumerable<IBulb> source)
        {
            var l = new List<BulbModel>();

            foreach (var b in source)
            {
                l.Add(await b.GetBulbModel());
            }

            return l;
        }

        public async Task<BulbModel> GetBulbModel()
        {
            BulbModel b;

            lock (BulbService.BulbCache)
            {
                if (BulbService.BulbCache.ContainsKey(MACAddress))
                {
                    b = BulbService.BulbCache[MACAddress];

                    b.Name = Name;
                    b.Icon = Icon;
                    b.Settings.HomeId = HomeId;
                    b.Settings.RoomId = RoomId;

                    return b;
                }
            }

            // Since we are moving away from Bulb.GetBulbByMacAddress, 
            // and we want to use BulbService, we just create a new model if not in cache.
            // In a real scenario, we might want to trigger a discovery or a specific GetPilot.
            b = new BulbModel(IPAddress, Port)
            {
                MACAddress = MACAddress,
                Name = Name,
                Icon = Icon
            };
            b.Settings.HomeId = HomeId;
            b.Settings.RoomId = RoomId;

            return b;
        }


        public override string ToString()
        {
            return Name ?? MACAddress.ToString();
        }
    }

}
