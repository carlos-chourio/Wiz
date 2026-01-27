# WiZ Console Testing Application

## Overview

This console application is designed to help test and debug the WiZ bulb library. It can discover bulbs on your network, save a test bulb configuration for repeated testing, and perform various tests to identify and validate fixes for known issues.

## Features

1. **Bulb Discovery**: Scans your local network for WiZ bulbs
2. **Test Bulb Configuration**: Saves bulb information for repeated testing sessions
3. **Connection Testing**: Validates basic bulb communication
4. **Control Testing**: Tests turning bulbs on/off and adjusting brightness
5. **State Caching Validation**: Verifies that state tracking is working correctly
6. **UDP Behavior Testing**: Simulates the debugging scenario that causes UDP port conflicts

## Usage

### First Run
1. Run the console application: `dotnet run`
2. The app will automatically discover bulbs on your network
3. Select a bulb when prompted (or press Enter for the first one)
4. The bulb information will be saved to `test_bulb_config.json` for future runs

### Subsequent Runs
1. The app will automatically load the saved test bulb configuration
2. If the saved bulb is unreachable, it will run discovery again
3. All tests will be performed automatically

## Test Results

The application performs the following tests and reports results:

### Basic Controls Test
- Turns the bulb ON
- Sets brightness to 50%
- Turns the bulb OFF

### State Caching Test
- Verifies that bulb state changes are properly tracked
- Tests state synchronization between bulb and cache

### UDP Behavior Test
- Simulates multiple rapid commands that trigger the UDP port binding issue
- Runs 5 concurrent tasks to test thread safety and port management

## Current Issues Detected

Based on the tests, the application helps identify:

1. **UDP Port Binding Issue**: The concurrent task test can reveal port conflicts that occur during debugging
2. **State Caching Problems**: Compares expected vs actual bulb states
3. **Network Communication Issues**: Validates basic connectivity and command execution

## Configuration File

The `test_bulb_config.json` file stores:
- MAC Address
- IP Address  
- Bulb Type/Model
- Last Seen timestamp

This allows for consistent testing across multiple sessions without needing to rediscover bulbs each time.

## Integration with Development

This console app serves as:
- **Regression Testing**: Run after code changes to verify fixes
- **Debugging Tool**: Helps isolate and reproduce specific issues
- **Performance Testing**: Can be extended to measure response times
- **Manual Testing**: Provides interactive bulb control for manual verification