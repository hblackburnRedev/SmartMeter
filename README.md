# SmartMeter

A distributed smart meter system for recording and managing domestic electricity consumption. Built with C# .NET WebSocket server and Electron desktop client.

## Overview

This application simulates a smart meter system where multiple client applications send electricity readings to a central server. The server calculates bills and pushes updates back to clients in real-time.

## Architecture

- **Server**: C# .NET WebSocket server (SmartMeter.Server)
- **Client**: Electron desktop application (SmartMeter.Client)
- **Communication**: JSON over WebSocket

## Setup

### Prerequisites

- .NET 8.0 SDK
- Node.js 18.x or higher

### Installation

**Server:**
```bash
cd SmartMeter.Server
dotnet restore
```

**Client:**
```bash
cd SmartMeter.Client
npm install
```

## Running

**Start Server:**
```bash
cd SmartMeter.Server
dotnet run
```

**Start Single Client:**
```bash
cd SmartMeter.Client
npm start
```

**Start Multiple Clients:**
```bash
cd SmartMeter.Client
npm run test:multiple
```

## Features

### Server
- Multiple concurrent client connections
- Client authentication
- Real-time bill updates
- Grid alert broadcasting

### Client
- Autonomous operation
- Live bill display
- Auto-reconnect on disconnection
- Error and alert notifications

## Technology Stack

**Server:** C# .NET 8.0, System.Net.WebSockets, System.Text.Json

**Client:** Electron, Native WebSocket, HTML/CSS/JavaScript

## Team Members

- Harrison Blackburn
- Uzair Mohammed
- Connor Clarke