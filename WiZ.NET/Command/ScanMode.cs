namespace WiZ.NET
{
    /// <summary>
    /// Bulb discovery scan modes.
    /// </summary>
    public enum ScanMode
    {
        /// <summary>
        /// Scan for the current pilot (state) of the bulb.
        /// </summary>
        GetPilot,

        /// <summary>
        /// Scan for the system configuration of the bulb.
        /// </summary>
        GetSystemConfig,

        /// <summary>
        /// Scan to register a new bulb on the network.
        /// </summary>
        Registration
    }
}
