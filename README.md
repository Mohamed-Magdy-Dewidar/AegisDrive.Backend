# ☁️ AegisDrive.Cloud
### High-Performance Fleet Management Backend API

![.NET 9](https://img.shields.io/badge/.NET-9.0-purple) ![Architecture](https://img.shields.io/badge/Architecture-Vertical%20Slice-blue) ![Status](https://img.shields.io/badge/Build-Passing-success) ![License](https://img.shields.io/badge/License-MIT-green)

The **AegisDrive.Cloud** backend is a scalable, event-driven Web API built to power the AegisDrive driver safety platform. It handles high-frequency telemetry ingestion from IoT devices, manages fleet operations, and delivers real-time critical alerts to the frontend dashboard.

---

## 🏗️ Architecture & Tech Stack

This project is built using **Vertical Slice Architecture (VSA)** with **CQRS** to ensure high cohesion, scalability, and maintainability.

### **Core Stack**
* **Framework:** .NET 9 Web API
* **Database:** PostgreSQL / SQL Server (via Entity Framework Core)
* **Caching:** Redis (for real-time vehicle state)
* **Messaging:** AWS SQS (via AWS Messaging Library)
* **Real-Time:** SignalR (WebSockets)

### **Design Patterns**
* **Vertical Slices:** Features are organized by functional area (e.g., `Features/Drivers/RegisterDriver`), not by technical layers.
* **CQRS:** Read (Queries) and Write (Commands) operations are separated using **MediatR**.
* **Result Pattern:** Standardized success/failure responses using a custom `Result<T>` type.
* **Smart Ingestion:** High-throughput "Store-and-Forward" architecture using AWS SQS to decouple edge devices from the database.

---

## 🚀 Key Features

### 1. IoT Data Ingestion
* **High-Frequency Telemetry:** Ingests GPS, Speed, and G-Force data from ESP32 "Black Boxes" at 10Hz.
* **Smart Alerts:** Processes contextual safety events (Drowsiness + Traffic Hazards) from Raspberry Pi edge units.
* **Resilience:** Returns `202 Accepted` instantly to devices while processing data asynchronously via background workers.

### 2. Fleet Management
* **Asset Tracking:** Full CRUD for Vehicles, Drivers, and Devices.
* **Shift Management:** Dynamic assignment of drivers to vehicles with automatic shift tracking and history.
* **Emergency Contacts:** Management of family notifications for critical driver incidents.

### 3. Real-Time Monitoring
* **Live Map:** Exposes a `GET /monitor/live` endpoint backed by **Redis** for millisecond-latency fleet positioning.
* **Instant Notifications:** Pushes `CRITICAL` alerts (e.g., "Driver Asleep!") to the React dashboard via **SignalR** immediately upon ingestion.

### 4. Evidence & Analytics
* **S3 Integration:** Securely stores and retrieves incident snapshots using **Presigned URLs**.
* **Driver Scoring:** Calculates safety scores (0-100) based on event frequency and severity.

---

## 📂 Project Structure

The solution follows a clean separation of concerns:

```
AegisDrive.Cloud/
├── src/
│   ├── AegisDrive.API/           # Entry Point (Minimal APIs with Carter)
│   │   ├── Features/             # Vertical Slices (Commands, Queries, Handlers)
│   │   │   ├── Drivers/
│   │   │   ├── Fleet/
│   │   │   └── Ingestion/
│   │   ├── Hubs/                 # SignalR Hubs
│   │   └── Program.cs
│   │
│   ├── AegisDrive.Core/          # Domain Entities, Enums, Interfaces
│   │
│   ├── AegisDrive.Infrastructure/# Database Context, External Services (AWS, Redis)
│   │
│   └── AegisDrive.Tests/         # Unit and Integration Tests
└── docker-compose.yaml           # Local Dev Environment (SQL, Redis)
```

---

## ⚡ Getting Started

### Prerequisites
* .NET 9 SDK
* Docker Desktop
* AWS Account (SQS & S3)

### 1. Start Infrastructure

```bash
docker-compose up -d
```

### 2. Configuration

Update `src/AegisDrive.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AegisDriveDB;",
    "RedisConnection": "localhost:6379"
  },
  "AWS": {
    "Region": "us-east-1",
    "QueueUrl": "https://sqs.us-east-1.amazonaws.com/.../queue",
    "BucketName": "aegis-drive-storage"
  }
}
```

### 3. Run Migrations

```bash
cd src/AegisDrive.API
dotnet ef database update
```

### 4. Run the API

```bash
dotnet run
```

Swagger: `https://localhost:7199/swagger`

---

## 🔌 API Overview

| Feature | Method | Endpoint | Description |
|--------|--------|----------|-------------|
| Ingest | POST | `/ingest/telemetry` | Push GPS/Sensor data |
| Ingest | POST | `/ingest/safety-event` | Push critical alerts |
| Fleet | GET | `/fleet/vehicles` | List all vehicles |
| Fleet | POST | `/fleet/assignments/start` | Start a driver shift |
| Monitor | GET | `/monitor/live` | Real-time fleet data |

---

## 🧪 Testing

```bash
dotnet test
```

---

## 👥 Author
**Mohamed Magdy Dewidar**  
*Lead Backend Architect*
