# WiZ .NET API Library
A modern .NET Standard 2.0 library for controlling WiZ Smart Bulbs

> [!IMPORTANT]
> **Fork Information**: This project is a refactored fork of the original [WizLib](https://github.com/nmoschkin/WizLib). It has been modernized and restructured to improve maintainability, separation of concerns, and testability.

## 🚀 Key Improvements in this Fork

- **Architectural Split**: Separated bulb state (`BulbModel`) from operational logic (`BulbService`).
- **Resilient Communication**: Improved UDP communication handling with built-in retry logic and thread-safe operations.
- **Modern C# Patterns**: Refactored codebase using modern C# features (C# 10+) and improved naming conventions.
- **Comprehensive Testing**: Includes a dedicated XUnit test project with integration testing capabilities.

## 🚦 Getting Started

### 1. Discovery
```csharp
var service = new BulbService();
var bulbs = await service.DiscoverBulbsAsync();
```

### 2. Controlling a Bulb
```csharp
var bulb = await service.GetBulbByMacAsync(MACAddress.Parse("44:4F:8E:EF:BC:82"));
await service.TurnOnAsync(bulb);
await service.SetBrightnessAsync(bulb, 100);
await service.SetTemperatureAsync(bulb, 2700); 
```

## 🧪 Testing

The project includes a `WiZ.Console` app for manual verification and a `WiZ.Tests` suite for automated verification.

### Running Tests
```bash
dotnet test WiZ.Tests/WiZ.Tests.csproj
```

