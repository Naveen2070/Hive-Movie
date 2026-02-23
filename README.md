🐝 The Hive: Movie & Ticketing Microservice

The core backend service responsible for managing the movie catalog, physical cinema infrastructure, and a highly concurrent seat reservation engine for **The Hive** ecosystem.

## 🚀 Overview

This microservice is built using **.NET 10** and is designed to handle the heavy lifting of movie theater operations. It features a custom, high-performance byte-array engine for managing real-time seat availability, ensuring atomic transactions and preventing double-booking during high-traffic ticket sales.

It is part of a polyglot microservices architecture and is designed to accept JWT authorization from the Kotlin `Hive-Identity` service.

## 🛠️ Tech Stack

* **Framework:** .NET 10 (ASP.NET Core Web API)
* **Language:** C# 13
* **Database:** SQL Server
* **ORM:** Entity Framework Core (with JSON column mapping)
* **Validation:** FluentValidation & Data Annotations
* **API Documentation:** OpenAPI (v3) / Scalar UI
* **Error Handling:** Centralized `IExceptionHandler` (RFC 7807 Problem Details)

## ✨ Key Features

* **Domain-Driven REST API:** Strict standard routing (`/api/{resource}/{id}/{action}`) across all controllers.
* **Complex Auditorium Layouts:** Seamlessly stores and validates custom seating topologies (disabled seats, wheelchair spots) using EF Core JSON mapping and cross-property FluentValidation.
* **High-Concurrency Ticketing:** Utilizes Optimistic Concurrency Control (RowVersion) and a custom `SeatMapEngine` to prevent race conditions during checkout.
* **Standardized Error Responses:** Fully compliant with RFC 7807. The frontend receives predictable JSON payloads for 400s, 404s, and 409s without scattered `try/catch` blocks.

## 🚦 Getting Started

### Prerequisites
* .NET 10 SDK
* SQL Server (LocalDB, Docker, or Cloud)

### Setup Instructions
1. Clone the repository.
2. Update the `DefaultConnection` string in `appsettings.Development.json` to point to your SQL Server instance.
3. Open a terminal in the project root and apply the Entity Framework migrations to build your database schema:
```bash
   dotnet ef database update
```

4. Run the application:
```bash
dotnet run
```

### API Documentation

When running in the Development environment, navigate to the base URL to access the interactive **Scalar UI** developer portal (e.g., `https://localhost:5001/scalar`). Here you can view the fully annotated OpenAPI documentation and test endpoints.

## 🔒 Security (Upcoming)

This service expects a valid JWT Bearer token issued by the Kotlin Identity Service for any mutating operations (`POST`, `PUT`, `DELETE`).
