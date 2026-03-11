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

> **Hive-Movie** is the ticketing powerhouse of the Hive platform. Built with **C# 13** and **.NET 10**, it handles massive
> concurrent seat bookings using a specialized zero-allocation engine, manages cinema and auditorium lifecycles, and
> maintains a rich movie catalog with multi-tenant ownership.

---

### 🔗 Associated Repositories

* 👉 **[The-Hive-Project (Main Hub)](https://github.com/Naveen2070/The-Hive-Project)**
* 👉 **[Hive-Identity (Auth Service)](https://github.com/Naveen2070/The-Hive-Project/tree/main/services/identity-service)**
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
* **🏗️ Outbox Pattern:** Guarantees reliable asynchronous communication. Business events (like booking confirmations) are persisted to an `OutboxMessages` table and dispatched to **RabbitMQ** by a dedicated background worker.
* **🧹 Automated Cleanup:** Features a `TicketCleanupWorker` that automatically releases reserved seats if payments are
  not confirmed within the expiration window (10 minutes) and marks past showtimes as expired.
* **📊 Organizer Dashboard:** Provides real-time statistical aggregation including revenue trends, sales growth, and
  recent transaction history for cinema owners.

---

## 🛠️ Tech Stack

* **Language:** C# 13 / .NET 10
* **Web Framework:** ASP.NET Core Web API
* **ORM:** Entity Framework Core (EF Core) 10
* **Database:** Microsoft SQL Server 2022
* **Security:** JWT (Multi-tenant Domain Roles), HMAC-SHA256 (Zero-Trust S2S)
* **Messaging:** RabbitMQ (AMQP)
* **Validation:** FluentValidation
* **Internal Communication:** Refit (Type-safe REST client for Identity Service)
* **Documentation:** OpenAPI / Scalar UI

---

## 🏗️ Architecture

Hive-Movie follows a modern microservices pattern focusing on high throughput and eventual consistency.

### 1. High-Level Ecosystem

```mermaid
flowchart TB
    classDef external fill:#f5f5f5,stroke:#9e9e9e,stroke-width:2px,color:#212121
    classDef platform fill:#e3f2fd,stroke:#64b5f6,stroke-width:2px,color:#0d47a1
    classDef movie fill:#fff3e0,stroke:#ffcc80,stroke-width:2px,color:#e65100

    subgraph USERS ["Users"]
        user[End User]
        organizer[Cinema Organizer]
    end

    subgraph HIVE ["The Hive Platform"]
        ui["Hive-Forager (UI)"]:::platform
        identity["Hive-Identity (IAM)"]:::platform
        movies["Hive-Movie (Ticketing)"]:::movie
    end

    subgraph EXTERNAL ["External"]
        email["Email Provider"]:::external
    end

    user --> ui
    organizer --> ui
    ui --> identity
    ui --> movies
    movies -. HMAC S2S .-> identity
    movies -- Publish Notification --> email
```

### 2. Container & Messaging Architecture

```mermaid
flowchart TB
    classDef edge fill:#fff3e0,stroke:#ffcc80,stroke-width:2px,color:#e65100
    classDef movie fill:#fff3e0,stroke:#ffcc80,stroke-width:2px,color:#e65100
    classDef db fill:#eceff1,stroke:#b0bec5,stroke-width:2px,color:#263238
    classDef broker fill:#ffebee,stroke:#ef9a9a,stroke-width:2px,color:#b71c1c

    subgraph DOCKER ["Docker Network"]
        direction TB
        
        subgraph APP ["Movie Service"]
            api["Hive-Movie Engine (.NET 10)"]:::movie
            outbox["Outbox Worker"]:::movie
            cleanup["Cleanup Worker"]:::movie
        end

        subgraph PERSISTENCE ["Data Storage"]
            sql[(SQL Server)]:::db
            cache[(In-Memory Cache)]:::db
        end

        subgraph MESSAGING ["Event Bus"]
            rabbit[(RabbitMQ)]:::broker
        end
    end

    api --> sql
    api --> cache
    outbox --> sql
    outbox -- Publish Events --> rabbit
    cleanup --> sql
```

### 3. Layered Architecture (Internal)

```mermaid
flowchart TB
    classDef layer_api fill:#e3f2fd,stroke:#90caf9,stroke-width:2px,color:#0d47a1
    classDef layer_app fill:#e8f5e9,stroke:#a5d6a7,stroke-width:2px,color:#1b5e20
    classDef layer_dom fill:#fff3e0,stroke:#ffcc80,stroke-width:2px,color:#e65100
    classDef layer_infra fill:#f3e5f5,stroke:#ce93d8,stroke-width:2px,color:#4a148c

    subgraph PRESENTATION ["Presentation Layer"]
        ctrl[Controllers]:::layer_api
        dto[DTOs / Validators]:::layer_api
    end

    subgraph APPLICATION ["Application Layer"]
        svc[Services]:::layer_app
        engine[SeatMapEngine]:::layer_app
        workers[Background Workers]:::layer_app
    end

    subgraph DOMAIN ["Domain Layer"]
        models[Entities / Enums]:::layer_dom
    end

    subgraph INFRA ["Infrastructure Layer"]
        db[ApplicationDbContext]:::layer_infra
        messaging[RabbitMQ Producer]:::layer_infra
        security[HMAC / JWT Handlers]:::layer_infra
    end

    PRESENTATION --> APPLICATION
    APPLICATION --> DOMAIN
    APPLICATION --> INFRA
    INFRA --> DOMAIN
```

### 4. Seat Reservation Lifecycle

```mermaid
sequenceDiagram
    participant U as User
    participant API as TicketService
    participant E as SeatMapEngine
    participant DB as SQL Server
    participant W as CleanupWorker

    U->>API: Reserve Seats (ShowtimeId, Seats)
    API->>DB: Fetch Showtime (RowVersion)
    API->>E: TryReserveSeats(binary_state)
    E-->>API: Success (binary_updated)
    API->>DB: Save Ticket (Pending) + Update Showtime
    DB-->>API: Commit (RowVersion check)
    API-->>U: Booking Reference (HIVE-XXXX)

    Note over U,W: 10 Minute Payment Window
    
    alt Payment Successful
        U->>API: Confirm Payment
        API->>E: MarkAsSold(seats)
        API->>DB: Update Ticket (Confirmed) + Outbox Message
        API-->>U: Success
    else Payment Timeout
        W->>DB: Find Expired Pending Tickets
        W->>E: ReleaseSeat(seats)
        W->>DB: Update Ticket (Expired) + Update Showtime
    end
```

---

## 📊 Entity Relationship Diagram (ERD)

```mermaid
erDiagram
    BASE_AUDITABLE_ENTITY ||--|| CINEMA : "inherits"
    BASE_AUDITABLE_ENTITY ||--|| AUDITORIUM : "inherits"
    BASE_AUDITABLE_ENTITY ||--|| MOVIE : "inherits"
    BASE_AUDITABLE_ENTITY ||--|| SHOWTIME : "inherits"
    BASE_AUDITABLE_ENTITY ||--|| TICKET : "inherits"

    CINEMA ||--o{ AUDITORIUM : "hosts"
    MOVIE ||--o{ SHOWTIME : "scheduled in"
    AUDITORIUM ||--o{ SHOWTIME : "hosts"
    SHOWTIME ||--o{ TICKET : "issues"

    BASE_AUDITABLE_ENTITY {
        guid id PK "UUID v7 (Sequential)"
        datetime created_at_utc "Creation timestamp"
        long created_by "Creator User ID"
        datetime updated_at_utc "Last update timestamp"
        long updated_by "Modifier User ID"
        boolean is_active "Activity status"
        boolean is_deleted "Soft-delete flag"
        datetime deleted_at_utc "Deletion timestamp"
        long deleted_by "Deleter User ID"
    }

    CINEMA {
        string organizer_id "Owner ID (Identity Service)"
        string name "Multiplex Name"
        string location "Physical Address"
        string contact_email "Support Email"
        int approval_status "0:Pending, 1:Approved, 2:Rejected"
    }

    AUDITORIUM {
        guid cinema_id FK "Reference to Cinema"
        string name "Screen/Hall Name"
        int max_rows "Grid Height"
        int max_columns "Grid Width"
        json layout_configuration "JSON: Tiers, Disabled, Wheelchair"
    }

    MOVIE {
        string title "Movie Title"
        string description "Plot Summary"
        int duration_minutes "Runtime in mins"
        datetime release_date "Premiere Date"
        string poster_url "CDN Image Link"
    }

    SHOWTIME {
        guid movie_id FK "Reference to Movie"
        guid auditorium_id FK "Reference to Auditorium"
        datetime start_time_utc "Screening Time"
        decimal base_price "Ticket Base Cost"
        byte_array seat_availability_state "Binary Engine State"
        byte_array row_version "SQL Concurrency Token"
    }

    TICKET {
        string user_id "Identity User ID"
        guid showtime_id FK "Reference to Showtime"
        string booking_reference "UK: HIVE-XXXX"
        json reserved_seats "List: Row/Col pairs"
        decimal total_amount "Final Price (Base + Surcharge)"
        int status "0:Pending... 4:Cancelled"
        datetime paid_at_utc "Payment Timestamp"
    }

    OUTBOX_MESSAGE {
        guid id PK "UUID v4"
        string event_type "e.g., EmailNotification"
        string payload "JSON Data"
        datetime created_at_utc "Message Creation"
        datetime processing_at_utc "Lock Timestamp"
        datetime processed_at_utc "Completion Timestamp"
        int retry_count "Current Attempts"
        string error_message "Failure Log"
    }
```

---

## 📂 Project Structure

```text
Hive-Movie/
├── Configuration/      # DI Registrations, JWT & OpenApi Config
├── Controllers/        # REST Endpoints (Cinemas, Movies, Tickets, etc.)
├── Data/               # DBContext, Migrations, Initializers
├── DTOs/               # Data Transfer Objects & Validation models
├── Engine/             # High-performance binary SeatMapEngine
├── Infrastructure/     # Messaging, S2S Clients, Security Handlers
├── Middleware/         # Global Exception Handling
├── Models/             # Domain Entities (EF Core)
├── Services/           # Business Logic Layer
└── Workers/            # Background Tasks (Outbox, Cleanup)
```

---

## ⚙️ Getting Started

### 1. Prerequisites
* **.NET 10 SDK**
* **SQL Server 2022**
* **RabbitMQ**

### 2. Run with Docker
```bash
docker-compose up --build
```

### 3. Database Migration
```bash
dotnet ef database update
```

---

<p align="center">
Built with ❤️, ☕, and high-performance .NET. 🚀<br>
<b>Architected and maintained by <a href="https://github.com/Naveen2070">Naveen</a></b>
</p>
