using Hive_Movie.Configuration;
using Hive_Movie.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Scalar.AspNetCore;

IdentityModelEventSource.ShowPII = true;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

// Configure JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

WebApplication app = builder.Build();

/// ------------------------------------------------------------
/// Middleware And Pipeline Configuration
/// ------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    /// Create scoped service provider for development-only tasks
    /// such as database initialization and seeding.
    using (IServiceScope scope = app.Services.CreateScope())
    {
        IServiceProvider services = scope.ServiceProvider;

        try
        {
            /// Ensures database is created and seeded with initial data.
            ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
            DbInitializer.Initialize(context);
        }
        catch (Exception ex)
        {
            /// Log any failure during database initialization.
            ILogger logger = services.GetRequiredService<ILogger<Program>>();
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

/// Enables authentication middleware
app.UseAuthentication();

/// Enables authorization middleware (requires authentication setup if used and placed after it).
app.UseAuthorization();

/// Maps controller routes.
app.MapControllers();

/// Starts the application.
app.Run();