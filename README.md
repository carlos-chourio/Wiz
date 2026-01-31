# WiZ .NET API Library
A modern .NET Standard 2.0 library for controlling WiZ Smart Bulbs

> **Fork Information**: This project is a refactored fork of the original [WizLib](https://github.com/nmoschkin/WizLib). It has been modernized and restructured to improve maintainability, separation of concerns, and testability.

> [!IMPORTANT]
> **Disclaimer**: This is an independent open-source project developed as a hobby initiative. It is not affiliated with, endorsed by, or connected to the official WiZ™ trademark, brand, or its parent company. This library provides unofficial UDP-based communication capabilities for WiZ-compatible smart lighting products.
 


## 🚀 Key Improvements in this Fork

- **Architectural Split**: Separated bulb state (`BulbModel`) from operational logic (`BulbService`).
- **Resilient Communication**: Improved UDP communication handling with built-in retry logic and thread-safe operations.
- **Modern C# Patterns**: Refactored codebase using modern C# features (C# 10+) and improved naming conventions.
- **Comprehensive Testing**: Includes a dedicated XUnit test project with integration testing capabilities.

## 🚦 Getting Started

### Creating the service

There are two ways to do it. Using DI or manually creating the needed instances yourself.

#### 1. Dependency Injection 
```csharp
// In your Program.cs or Startup.cs
builder.Services.AddWiZNET();
var bulbService = serviceProvider.GetRequiredService<BulbService>();
```

#### 2. Creating instances manually
```csharp
// For simple applications or testing
using Microsoft.Extensions.Logging;

// A logger is needed you could create a Console logger or a NullLogger if you do not want to log anything
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var udpService = new UdpCommunicationService();
var bulbService = new BulbService(udpService, loggerFactory.CreateLogger<BulbService>());
```

### Usage Examples

#### Discovery
```csharp
var bulbs = await bulbService.DiscoverBulbsAsync();
```

#### Basic Controls
```csharp
var bulb = await bulbService.GetBulbByMacAsync(MACAddress.Parse("44:4F:8E:EF:BC:82"));

// Power control
await bulbService.TurnOnAsync(bulb);
await bulbService.TurnOffAsync(bulb);

// Brightness and temperature
await bulbService.SetBrightnessAsync(bulb, 75);
await bulbService.SetTemperatureAsync(bulb, 2700); 

await bulbService.SetColorAsync(bulb, System.Drawing.Color.FromArgb(255, 0, 0)); 

// Set built-in scenes
await bulbService.SetSceneAsync(bulb, LightMode.Ocean);
await bulbService.SetSceneAsync(bulb, LightMode.Daylight);
await bulbService.SetSceneAsync(bulb, LightMode.Wakeup);
```

## 🧪 Testing

The project includes a `WiZ.Console` app for manual verification and a `WiZ.Tests` suite for automated verification.

### Running Tests
```bash
dotnet test WiZ.Tests/WiZ.Tests.csproj
```

