using Microsoft.EntityFrameworkCore;
using SmartHotelBackend.Data;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=smart_hotel;User=root;Password=root;";

// ✅ Use Parse instead of AutoDetect — AutoDetect tries to CONNECT at startup and crashes if MySQL is not ready
builder.Services.AddDbContext<SmartHotelContext>(options =>
    options.UseMySql(connStr, ServerVersion.Parse("8.0.0-mysql"))
           .EnableDetailedErrors()
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

// Fix circular reference JSON serialization (Order → Items → Order)
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Smart Hotel API", Version = "v1" });
});

// CORS — allow Vite dev server on common ports
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:5175",
            "http://localhost:5176",
            "http://localhost:3000",
            "http://127.0.0.1:5173",
            "http://127.0.0.1:5174",
            "http://127.0.0.1:5175"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

// ✅ Auto-create tables — wrapped safely, app starts even if DB is down
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartHotelContext>();
        // Test connection first
        var canConnect = await db.Database.CanConnectAsync();
        if (canConnect)
        {
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ MySQL connected — tables ready.");
        }
        else
        {
            Console.WriteLine("⚠️  Cannot connect to MySQL. Check your connection string in appsettings.json.");
            Console.WriteLine($"   Connection: {connStr}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Database error: {ex.Message}");
        Console.WriteLine("   App will start — fix DB connection and restart.");
    }
}

// Always enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Hotel API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// ✅ Health check endpoint — useful for debugging
app.MapGet("/health", async (SmartHotelContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "ok", database = canConnect ? "connected" : "disconnected" });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { status = "degraded", database = "error", error = ex.Message });
    }
});

app.Run();
