using Chiniseapp.Application.Auth;
using Chiniseapp.Infrastructure;
using Chiniseapp.Infrastructure.Auth;
using Chiniseapp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// M1 smoke check: proves Api -> Infrastructure -> Postgres wiring works end-to-end
// before any entities/migrations exist. Superseded by real health checks in a later milestone.
app.MapGet("/health/db", async (ChiniseDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "ok", database = "reachable" })
        : Results.Problem("Database is not reachable", statusCode: 503);
});

// M3: idempotent bootstrap of the first super_admin account from
// Seed:SuperAdminEmail/Seed:SuperAdminPassword (user-secrets locally). No-op
// once any super_admin already exists.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbSeeder.SeedSuperAdminAsync(
        services.GetRequiredService<ChiniseDbContext>(),
        services.GetRequiredService<IPasswordHasherService>(),
        services.GetRequiredService<IConfiguration>(),
        services.GetRequiredService<ILogger<Program>>());
}

app.Run();
