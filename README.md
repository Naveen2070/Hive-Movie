<p align="center">
<img src="https://raw.githubusercontent.com/Naveen2070/The-Hive-Project/main/assets/hive-movie-logo.png" alt="Hive Movie Logo" width="150"/>
</p>

<h1 align="center">Hive-Movie (Catalog & Ticketing Service)</h1>

<p align="center"><em>The high-performance core engine for movie management, cinema orchestration, and atomic seat reservations within the EventHive ecosystem.</em></p>

<p align="center">
<img src="https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white" alt="C#"/>
<img src="https://img.shields.io/badge/Framework-.NET_10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10"/>
<img src="https://img.shields.io/badge/Database-SQL_Server-CC2927?logo=microsoftsqlserver&logoColor=white" alt="SQL Server"/>
<img src="https://img.shields.io/badge/Messaging-RabbitMQ-FF6600?logo=rabbitmq&logoColor=white" alt="RabbitMQ"/>
<img src="https://img.shields.io/badge/Security-JWT_+_HMAC-red" alt="Security"/>
<img src="https://img.shields.io/badge/ID_Gen-UUID_v7-blue" alt="UUID v7"/>
<img src="https://img.shields.io/badge/Containerization-Docker-2496ED?logo=docker&logoColor=white" alt="Docker"/>
<img src="https://img.shields.io/github/license/Naveen2070/The-Hive-Project" alt="License"/>
</p>

---

> **Hive-Movie** is the ticketing powerhouse of the Hive platform. Built with **C#** and **.NET 10**, it handles massive
> concurrent seat bookings using a specialized zero-allocation engine, manages cinema and auditorium lifecycles, and
> maintains a rich movie catalog with multi-tenant ownership.

---

### 🔗 Associated Repositories

* 👉 **[The-Hive-Project (Main Hub)](https://github.com/Naveen2070/The-Hive-Project)**
* 👉 **[Hive-Identity (Auth Service)](https://github.com/Naveen2070/The-Hive-Project/tree/main/services/identity-service)
  **
* 👉 **[Hive-Forager-UI (Frontend)](https://github.com/Naveen2070/Hive-Forager-UI)**

---

## 🚀 Key Features

* **⚡ High-Performance Seat Engine:** Implements a custom, zero-allocation `SeatMapEngine` utilizing raw byte arrays for
  O(1) status checks and atomic in-memory reservations, ensuring lightning-fast booking even for massive venues.
* **🛡️ Optimistic Concurrency:** Protects against overbooking using SQL Server `RowVersion` tokens, allowing for
  lock-free read operations while guaranteeing data integrity during high-traffic sales.
* **🏢 Multi-Tenant Cinema Management:** Allows organizers to manage their own cinema multiplexes, auditoriums, and
  showtimes with strict ownership validation and administrative approval workflows.
* **🆔 Modern ID Generation:** Uses **UUID v7 (Sequential UUIDs)** for all primary keys, combining the uniqueness of
  GUIDs with the database performance of sequential integers.
* **🏗️ Outbox Pattern:** Guarantees reliable asynchronous communication. Business events (like cinema approvals or
  booking confirmations) are persisted to an `OutboxMessages` table and dispatched to **RabbitMQ** by a dedicated
  background worker.
* **🧹 Automated Cleanup:** Features a `TicketCleanupWorker` that automatically releases reserved seats if payments are
  not confirmed within the expiration window (15 minutes).
* **📊 Organizer Dashboard:** Provides real-time statistical aggregation including revenue trends, sales growth, and
  recent transaction history for cinema owners.

---

## 🛠️ Tech Stack

* **Language:** C# 13 / .NET 10
* **Web Framework:** ASP.NET Core Web API
* **ORM:** Entity Framework Core (EF Core) 10
* **Database:** Microsoft SQL Server
* **Security:** JWT (Multi-tenant Domain Roles), HMAC-SHA256 (S2S)
* **Messaging:** RabbitMQ (AMQP)
* **Validation:** FluentValidation
* **Internal Communication:** Refit (Type-safe REST client)
* **API Documentation:** OpenAPI / Scalar UI

---

## 🏗️ Architecture

The project follows a modular **Clean Architecture** inspired structure with a focus on domain-driven logic:

```text
Hive-Movie/
├── Configuration/      # DI Registrations, JWT & OpenApi Config
├── Controllers/        # REST Endpoints (Cinemas, Movies, Tickets, etc.)
├── Data/               # DBContext, Migrations, Initializers
├── DTOs/               # Data Transfer Objects & Validation models
├── Engine/             # The high-performance SeatMapEngine
├── Infrastructure/     # Messaging, S2S Clients, Security Handlers
├── Middleware/         # Global Exception Handling
├── Models/             # Domain Entities (EF Core)
├── Services/           # Business Logic Layer
└── Workers/            # Background Tasks (Outbox, Cleanup)
```

---

## ⚙️ Getting Started (How to Run)

### Prerequisites

* **.NET 10 SDK**
* **Docker & Docker Compose**
* **SQL Server** (Local or Containerized)
* **RabbitMQ**

### 1. Clone the Repository

```bash
git clone https://github.com/Naveen2070/The-Hive-Project.git
cd The-Hive-Project/services/movie-service
```

### 2. Configuration (`appsettings.Development.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=HiveMovieDb;..."
  },
  "Jwt": {
    "Secret": "YOUR_BASE64_SECRET"
  },
  "RabbitMQ": {
    "Host": "localhost"
  }
}
```

### 3. Database Migration

```bash
dotnet ef database update
```

### 4. Run the Application

```bash
dotnet run
```

---

## 🔌 API Endpoints

### 🎬 Movies & Catalog (Public)

| Method | Endpoint           | Description                            | Access          |
|:-------|:-------------------|:---------------------------------------|:----------------|
| `GET`  | `/api/movies`      | Fetch entire movie catalog (Paginated) | Public          |
| `GET`  | `/api/movies/{id}` | Get detailed movie metadata            | Public          |
| `POST` | `/api/movies`      | Register a new movie                   | Organizer/Admin |

### 🏢 Cinemas & Auditoriums

| Method  | Endpoint                   | Description                        | Access      |
|:--------|:---------------------------|:-----------------------------------|:------------|
| `GET`   | `/api/cinemas`             | List all approved cinema locations | Public      |
| `POST`  | `/api/cinemas`             | Register a new multiplex           | Organizer   |
| `PATCH` | `/api/cinemas/{id}/status` | Approve/Reject a cinema            | Super Admin |
| `GET`   | `/api/auditoriums`         | List all room layouts              | Public      |

### 🎟️ Ticketing & Reservations

| Method | Endpoint                       | Description                         | Access          |
|:-------|:-------------------------------|:------------------------------------|:----------------|
| `GET`  | `/api/showtimes/{id}/map`      | Get real-time seat availability map | Public          |
| `POST` | `/api/tickets/reserve`         | Atomically reserve seats (Pending)  | Auth User       |
| `GET`  | `/api/tickets/my-bookings`     | View user's booking history         | Auth User       |
| `POST` | `/api/tickets/payment/success` | Webhook: Confirm payment & seats    | Public          |
| `POST` | `/api/tickets/check-in`        | Validate ticket at entry (QR Scan)  | Staff/Organizer |

### 📊 Analytics & Management

| Method | Endpoint                | Description                          | Access    |
|:-------|:------------------------|:-------------------------------------|:----------|
| `GET`  | `/api/movies/dashboard` | Organizer financial metrics & trends | Organizer |


---

<p align="center">
Built with ❤️, ☕, and high-performance .NET. 🚀<br>
<b>Architected and maintained by <a href="https://github.com/Naveen2070">Naveen</a></b>
</p>
