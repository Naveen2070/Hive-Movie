# Architecture & Development Guidelines

Welcome to the Movie Service (`movie-service`) of The Hive Project. This document outlines the architectural choices,
design patterns, engineering best practices, and development workflows used in this service.

---

## 1. Architectural Patterns

### 1.1. Layered Architecture with Domain Grouping

The project follows a standard **Layered Architecture** common in ASP.NET Core applications, but organizes business
logic by domain within those layers to maintain high cohesion.

* **`Controllers/`**: The entry point for all RESTful communication. Handles HTTP request parsing, model validation, and
  response mapping.
* **`Services/`**: The core business logic layer. Grouped by domain (e.g., `Movies/`, `Cinemas/`, `Tickets/`), these
  services orchestrate data access and cross-cutting concerns.
* **`Models/`**: Domain entities representing the database schema, including audit fields and JSON-mapped properties.
* **`DTOs/`**: Data Transfer Objects defining the API contract, decoupled from internal storage models.
* **`Engine/`**: Specialized high-performance logic, specifically the zero-allocation `SeatMapEngine`.
* **`Infrastructure/`**: External concerns like RabbitMQ producers, S2S authentication handlers, and Refit clients.
* **`Workers/`**: Background services (Hosted Services) for periodic tasks like outbox processing and ticket cleanup.

### 1.2. Transactional Outbox Pattern (Reliable EDA)

The service communicates with other microservices (like the Notification Service) using **RabbitMQ** through the *
*Outbox Pattern**.

* **Logic:** When a critical event occurs (e.g., a Cinema is approved), we save an `OutboxMessage` to the same database
  transaction as the status change.
* **Reliability:** The `NotificationOutboxWorker` periodically polls for unprocessed messages and publishes them to
  RabbitMQ, ensuring "at-least-once" delivery even if the message broker is temporarily down.

---

## 2. Design Patterns

### 2.1. DTO (Data Transfer Object) Pattern

We strictly separate database entities (`Ticket`) from API payloads (`MyTicketResponse`). This prevents database schemas
from leaking to the client and allows independent evolution of the API and the database.

### 2.2. Dependency Injection (DI)

We utilize the built-in .NET dependency injection container with strictly defined lifetimes:

* **Scoped**: Services and Database Contexts (tied to the request lifecycle).
* **Transient**: Lightweight handlers like `S2SAuthenticationHandler` or `IClaimsTransformation`.
* **Singleton**: Messaging producers and global configuration.

### 2.3. Repository / Unit of Work (via EF Core)

Instead of manual repository classes, we leverage **Entity Framework Core's `DbSet`** as our repository and the *
*`DbContext`** as our Unit of Work. This reduces boilerplate while maintaining transactional integrity.

### 2.4. Zero-Allocation Engine (Value Types)

The `SeatMapEngine` is implemented as a `readonly struct` to minimize heap allocations during complex seat availability
checks and reservation logic, optimizing memory usage during high-concurrency booking events.

---

## 3. Engineering Best Practices

### 3.1. Multi-tenant RBAC & Identity Transformation

* **Claims Transformation:** We use `IClaimsTransformation` to intercept JWTs from the Identity Service, parse the
  nested `permissions` JSON, and map them to native C# `ClaimTypes.Role`.
* **Strict Multi-tenancy:** Access is restricted per domain. Users without `movies` domain permissions are flat-out
  rejected from restricted endpoints.

### 3.2. Data Integrity & Performance

* **UUID v7:** All entities use **Sequential UUIDs** (v7) for primary keys, providing the uniqueness of a GUID with the
  index-friendly performance of a BIGINT.
* **Soft Deletion:** Records are never physically deleted. We use **EF Core Global Query Filters** to automatically
  exclude records where `IsDeleted == true` from all queries.
* **Optimistic Concurrency:** High-traffic entities like `Showtime` use a `RowVersion` (Timestamp) to prevent the "Lost
  Update" problem during concurrent seat bookings.

### 3.3. Resilience & Inter-service Communication

* **Refit:** We use Refit for type-safe, interface-driven REST clients when communicating with the Identity Service.
* **Global Exception Handling:** The `GlobalExceptionHandler` converts all unhandled exceptions into RFC 7807 **Problem
  Details** format, providing consistent error responses for the UI.

---

## 4. Testing Strategy

* **Unit Tests:** Located in `Tests/Services/` and `Tests/Engine/`, focusing on pure business logic and the seat map
  engine using Moq for dependency isolation.
* **Integration Tests:** Located in `Tests/Integration/`, using **SqlServerFixture** to run tests against a real SQL
  Server instance. These verify the full pipeline including EF Core mapping, query filters, and status transitions.

---

## 5. How to Add a New Feature (Developer Guide)

If you need to add a new feature (e.g., "Movie Reviews"), follow this workflow:

1. **Define the Entity (`Models/MovieReview.cs`):**
    * Create the entity inheriting from `BaseAuditableEntity`.
    * Update `ApplicationDbContext.cs` to add the `DbSet` and any query filters.
2. **Define the DTOs (`DTOs/MovieReviewDtos.cs`):**
    * Create request and response records.
3. **Define the Service Interface & Implementation (`Services/Movies/`):**
    * Implement the business logic, ensuring ownership checks and audit logging.
4. **Register dependencies (`Configuration/DependencyInjection.cs`):**
    * Register your new service in the DI container.
5. **Expose the API (`Controllers/MovieReviewsController.cs`):**
    * Inject the service and define REST endpoints with appropriate `[Authorize]` attributes.
6. **Write Tests:**
    * Add unit tests for the service.
    * Add integration tests to verify DB persistence and security.
