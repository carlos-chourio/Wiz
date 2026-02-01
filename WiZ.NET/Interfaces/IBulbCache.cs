using System.Collections.Generic;
using WiZ.NET.Models;

namespace WiZ.NET.Interfaces
{
    /// <summary>
    /// Interface for bulb caching operations.
    /// Provides thread-safe access to cached bulb models.
    /// </summary>
    public interface IBulbCache
    {
        /// <summary>
        /// Gets a bulb from the cache by MAC address.
        /// </summary>
        /// <param name="macAddress">The MAC address to look up.</param>
        /// <returns>The cached bulb model, or null if not found.</returns>
        BulbModel Get(MACAddress macAddress);

        /// <summary>
        /// Adds or updates a bulb in the cache.
        /// </summary>
        /// <param name="macAddress">The MAC address of the bulb.</param>
        /// <param name="bulb">The bulb model to cache.</param>
        void Set(MACAddress macAddress, BulbModel bulb);

        /// <summary>
        /// Checks if a bulb exists in the cache.
        /// </summary>
        /// <param name="macAddress">The MAC address to check.</param>
        /// <returns>True if the bulb is in the cache.</returns>
        bool Contains(MACAddress macAddress);

        /// <summary>
        /// Removes a bulb from the cache.
        /// </summary>
        /// <param name="macAddress">The MAC address of the bulb to remove.</param>
        /// <returns>True if the bulb was removed, false if not found.</returns>
        bool Remove(MACAddress macAddress);

        /// <summary>
        /// Gets all cached bulbs.
        /// </summary>
        /// <returns>A collection of all cached bulb models.</returns>
        IEnumerable<BulbModel> GetAll();

        /// <summary>
        /// Clears all bulbs from the cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the number of cached bulbs.
        /// </summary>
        int Count { get; }
    }
}
