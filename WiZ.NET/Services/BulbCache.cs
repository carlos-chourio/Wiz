using System.Collections.Concurrent;
using System.Collections.Generic;
using WiZ.NET.Interfaces;
using WiZ.NET.Models;

namespace WiZ.NET.Services
{
    /// <summary>
    /// Thread-safe implementation of bulb cache using ConcurrentDictionary.
    /// </summary>
    public class BulbCache : IBulbCache
    {
        private readonly ConcurrentDictionary<MACAddress, BulbModel> _cache = new();

        /// <inheritdoc />
        public BulbModel Get(MACAddress macAddress)
        {
            _cache.TryGetValue(macAddress, out var bulb);
            return bulb;
        }

        /// <inheritdoc />
        public void Set(MACAddress macAddress, BulbModel bulb)
        {
            _cache[macAddress] = bulb;
        }

        /// <inheritdoc />
        public bool Contains(MACAddress macAddress)
        {
            return _cache.ContainsKey(macAddress);
        }

        /// <inheritdoc />
        public bool Remove(MACAddress macAddress)
        {
            return _cache.TryRemove(macAddress, out _);
        }

        /// <inheritdoc />
        public IEnumerable<BulbModel> GetAll()
        {
            return _cache.Values;
        }

        /// <inheritdoc />
        public void Clear()
        {
            _cache.Clear();
        }

        /// <inheritdoc />
        public int Count => _cache.Count;
    }
}
