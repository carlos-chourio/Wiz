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

        /// <summary>
        /// Gets a BulbModel from this BulbItem.
        /// Creates a new BulbModel with the information from this item.
        /// </summary>
        /// <returns>A BulbModel instance.</returns>
        public Task<BulbModel> GetBulbModel()
        {
            // Create a new BulbModel from this item's data
            var bulb = new BulbModel(IPAddress, Port)
            {
                MACAddress = MACAddress,
                Name = Name,
                Icon = Icon
            };
            bulb.Settings.HomeId = HomeId;
            bulb.Settings.RoomId = RoomId;

            return Task.FromResult(bulb);
        }


        public override string ToString()
        {
            return Name ?? MACAddress.ToString();
        }
    }

}
