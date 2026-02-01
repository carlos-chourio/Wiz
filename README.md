# WiZ .NET API Library
A modern .NET Standard 2.0 library for controlling WiZ Smart Bulbs

> **Fork Information**: This project is a refactored fork of the original [WizLib](https://github.com/nmoschkin/WizLib). It has been modernized and restructured to improve maintainability, separation of concerns, and testability.

> [!IMPORTANT]
> **Disclaimer**: This is an independent open-source project developed as a hobby initiative. It is not affiliated with, endorsed by, or connected to the official WiZ™ trademark, brand, or its parent company. This library provides unofficial UDP-based communication capabilities for WiZ-compatible smart lighting products.
 


## 🚀 Key Improvements in this Fork

- **Architectural Split**: Separated bulb state (`BulbModel`) from operational logic (`BulbService`).
- **Resilient Communication**: Improved UDP communication handling with built-in retry logic and thread-safe operations.
- **Modern C# Patterns**: Refactored codebase using modern C# features (C# 10+) and improved naming conventions.
- **Interface Abstractions**: Full interface support (`IBulbService`, `IUdpCommunicationService`, `IBulbCache`) for testability.
- **Cancellation Support**: All async operations support `CancellationToken`.
- **Structured Logging**: Comprehensive logging with scopes for better diagnostics.
- **Dependency Injection**: First-class DI support with `AddWiZNET()` extension method.
- **Comprehensive Testing**: Includes a dedicated XUnit test project with integration testing capabilities.

## 📦 Installation

```bash
dotnet add package WiZ.NET
```

## 🚦 Getting Started

### Creating the service

There are two ways to do it: Using **Dependency Injection** or manually creating the needed instances yourself.

#### 1. Dependency Injection (Recommended)

```csharp
using WiZ.NET.Extensions;
using WiZ.NET.Interfaces;

// In your Program.cs or Startup.cs
builder.Services.AddWiZNET();

// With custom timeout
builder.Services.AddWiZNET(timeout: 10000);

// Or using options
builder.Services.AddWiZNET(options =>
{
    options.Timeout = 10000;
});

// Resolve the service
var bulbService = serviceProvider.GetRequiredService<IBulbService>();
```

#### 2. Creating instances manually

```csharp
using Microsoft.Extensions.Logging;
using WiZ.NET.Interfaces;
using WiZ.NET.Services;

// For simple applications or testing
// A logger is needed - you could create a Console logger or a NullLogger
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Create the cache 
var bulbCache = new BulbCache();

// Create the UDP service
var udpService = new UdpCommunicationService(
    loggerFactory.CreateLogger<UdpCommunicationService>());

// Create the bulb service with all dependencies
var bulbService = new BulbService(
    udpService, 
    bulbCache, 
    loggerFactory.CreateLogger<BulbService>());

// Or with custom timeout
var bulbService = new BulbService(
    udpService, 
    bulbCache, 
    timeout: 10000,
    loggerFactory.CreateLogger<BulbService>());
```

### Usage Examples

#### Discovery

```csharp
using WiZ.NET.Interfaces;

// Discover all bulbs on the network
var bulbs = await bulbService.ScanForBulbsAsync();

// With custom timeout
var bulbs = await bulbService.ScanForBulbsAsync(timeout: 10000);

// With progress callback
var bulbs = await bulbService.ScanForBulbsAsync(
    timeout: 5000,
    callback: (bulb) => Console.WriteLine($"Found: {bulb.MACAddress}"));

// With cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var bulbs = await bulbService.ScanForBulbsAsync(
    timeout: 10000,
    cancellationToken: cts.Token);
```

#### Basic Controls

```csharp
// Get a specific bulb by MAC address
var bulb = await bulbService.GetBulbByMacAsync(
    MACAddress.Parse("44:4F:8E:EF:BC:82"));

// Power control
await bulbService.TurnOnAsync(bulb);
await bulbService.TurnOffAsync(bulb);

// Brightness (0-100)
await bulbService.SetBrightnessAsync(bulb, 75);

// Color temperature (Kelvin)
await bulbService.SetTemperatureAsync(bulb, 2700); 

// RGB color
await bulbService.SetColorAsync(bulb, System.Drawing.Color.FromArgb(255, 0, 0)); 

// Built-in scenes
await bulbService.SetSceneAsync(bulb, LightMode.Ocean);
await bulbService.SetSceneAsync(bulb, LightMode.Daylight);
await bulbService.SetSceneAsync(bulb, LightMode.Wakeup);

// All operations support cancellation tokens
await bulbService.TurnOnAsync(bulb, cancellationToken);
```

#### Getting Bulb State

```csharp
// Get current pilot (state) from bulb
await bulbService.RefreshStateAsync(bulb);
Console.WriteLine($"State: {(bulb.IsPoweredOn ? "ON" : "OFF")}");
Console.WriteLine($"Brightness: {bulb.Brightness}%");
Console.WriteLine($"Scene: {bulb.LightMode.Name}");
```

#### Working with the Cache

```csharp
using WiZ.NET.Interfaces;

// Access the cache via DI
var cache = serviceProvider.GetRequiredService<IBulbCache>();

// Or if you created it manually
var cache = new BulbCache();

// Cache operations
if (cache.Contains(macAddress))
{
    var cachedBulb = cache.Get(macAddress);
}

// Get all cached bulbs
var allBulbs = cache.GetAll();

// Clear cache
cache.Clear();
```


## 🧪 Testing

The project includes a `WiZ.Console` app for manual verification and a `WiZ.Tests` suite for automated verification.

### Running Tests

```bash
dotnet test WiZ.Tests/WiZ.Tests.csproj
```

## 📄 License

MIT License - See [LICENSE](LICENSE) for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
