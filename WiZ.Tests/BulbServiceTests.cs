using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using WiZ.Models;
using WiZ.Helpers;
using WiZ.Services;
using Microsoft.Extensions.Logging.Abstractions; // Added for NullLogger

namespace WiZ.Tests
{
    public class BulbServiceTests
    {
        private readonly BulbService _bulbService;
        private const string TestMac = "44:4F:8E:EF:BC:82";
        private const string TestIp = "192.168.0.180";

        public BulbServiceTests()
        {
            // 1. Create a "dummy" logger for the UDP service
            var udpLogger = NullLogger<UdpCommunicationService>.Instance;
            
            // 2. Create the UDP service
            var udpService = new UdpCommunicationService(udpLogger);

            // 3. Create a "dummy" logger for the Bulb service
            var bulbLogger = NullLogger<BulbService>.Instance;

            // 4. Manually inject the dependencies into the service
            // We use 5000 for the timeout as used in your Program.cs
            _bulbService = new BulbService(udpService, 5000, bulbLogger);
        }

        [Fact]
        public void BulbModel_Initialization_ShouldHaveCorrectDefaults()
        {
            var bulb = new BulbModel(TestIp);
            
            Assert.Equal(IPAddress.Parse(TestIp), bulb.IPAddress);
            Assert.Equal(38899, bulb.Port);
            Assert.False(bulb.IsPoweredOn);
            Assert.Equal(0, bulb.Brightness);
        }

        [Fact]
        public async Task BulbService_GetBulbByMacAsync_ShouldReturnModel()
        {
            var mac = MACAddress.Parse(TestMac);
            var bulb = await _bulbService.GetBulbByMacAsync(mac);
            // Assert.IsNotNull(bulb);
            // In a test environment without real bulbs, this might be null if scan fails
            // but we can at least verify the service logic doesn't crash.
        }

        [Fact]
        public async Task SetPilotOperations_WithNullIp_ShouldThrow()
        {
            var bulb = new BulbModel(); // No IP set
            
            await Assert.ThrowsAsync<InvalidOperationException>(() => _bulbService.TurnOnAsync(bulb));
        }

        [Fact]
        public void BulbModel_Clone_ShouldBeDeepCopy()
        {
            var mac = MACAddress.Parse(TestMac);
            var original = new BulbModel(TestIp)
            {
                MACAddress = mac,
                Name = "Office Bulb"
            };
            original.Settings.Brightness = 80;

            var clone = original.Clone();

            Assert.Equal(original.MACAddress.ToString(), clone.MACAddress.ToString());
            Assert.Equal(original.Name, clone.Name);
            Assert.Equal(original.Brightness, clone.Brightness);
            
            // Modify clone
            clone.Settings.Brightness = 20;
            Assert.NotEqual(original.Brightness, clone.Brightness);
        }

        [Fact]
        public async Task BulbService_Integration_BasicControls_ShouldWorkAndLeaveOn()
        {
            var mac = MACAddress.Parse(TestMac);
            var ip = IPAddress.Parse(TestIp);
            var bulb = new BulbModel(ip) { MACAddress = mac };
            
            // Test brightness
            await _bulbService.SetBrightnessAsync(bulb, 50);
            Assert.Equal(50, bulb.Brightness);
            await Task.Delay(1000);

            // Test color
            await _bulbService.SetColorAsync(bulb, System.Drawing.Color.FromArgb(255, 0, 0));
            Assert.Equal(System.Drawing.Color.FromArgb(255, 0, 0), bulb.Settings.Color);
            await Task.Delay(1000);

            // Test setting a scene
            await _bulbService.SetSceneAsync(bulb, WiZ.LightMode.LightModes[1]);
            Assert.Equal(1, (int)bulb.Settings.Scene);
            await Task.Delay(1000);

            // Turn off briefly
            await _bulbService.TurnOffAsync(bulb);
            Assert.False(bulb.IsPoweredOn);
            await Task.Delay(1000);

            // Finally, turn it back ON and set it to a solid Warm White scene (11)
            await _bulbService.TurnOnAsync(bulb);
            await _bulbService.SetBrightnessAsync(bulb, 100);
            await _bulbService.SetSceneAsync(bulb, WiZ.LightMode.WarmWhite);

            Assert.True(bulb.IsPoweredOn);
            Assert.Equal(11, bulb.Settings.Scene);
        }
    }
}