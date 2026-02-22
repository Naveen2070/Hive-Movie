using Hive_Movie.Configuration;
using Hive_Movie.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

/// ------------------------------------------------------------
/// Service Registration
/// ------------------------------------------------------------

/// Registers the application's primary database context.
/// Connection string is resolved from configuration (appsettings).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

/// Registers all custom application services (business logic layer).
/// Keeps Program.cs clean and modular.
builder.Services.AddApplicationServices();

/// Adds MVC controllers support (API endpoints).
builder.Services.AddControllers();

/// Registers the standard Problem Details formatting
builder.Services.AddProblemDetails();

/// Registers our custom catcher's mitt
builder.Services.AddExceptionHandler<Hive_Movie.Middleware.GlobalExceptionHandler>();

/// Registers OpenAPI document generation.
builder.Services.AddOpenApiDocumentation();


var app = builder.Build();

/// ------------------------------------------------------------
/// Middleware And Pipeline Configuration
/// ------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    /// Create scoped service provider for development-only tasks
    /// such as database initialization and seeding.
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        try
        {
            /// Ensures database is created and seeded with initial data.
            var context = services.GetRequiredService<ApplicationDbContext>();
            DbInitializer.Initialize(context);
        }
        catch (Exception ex)
        {
            /// Log any failure during database initialization.
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred seeding the database.");
        }
    }

    /// Exposes OpenAPI JSON endpoint.
    app.MapOpenApi();

    /// Maps Scalar UI for API documentation (alternative to Swagger UI).
    app.MapScalarApiReference();
}

/// Activates the exception handler middleware
app.UseExceptionHandler();

/// Redirects HTTP traffic to HTTPS.
app.UseHttpsRedirection();

/// Enables authorization middleware (requires authentication setup if used).
app.UseAuthorization();

/// Maps controller routes.
app.MapControllers();

/// Starts the application.
app.Run();