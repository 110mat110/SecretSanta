using SecretSanta.ApiService.Database;
using Microsoft.EntityFrameworkCore;
using SecretSanta.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddTransient<UserService>();
builder.Services.AddTransient<GroupService>();
builder.Services.AddTransient<WishService>();

builder.Services.AddDbContext<Context>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.")));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !app.Environment.IsDevelopment())
    {
        var expectedKey = builder.Configuration["ApiSecurity:InternalApiKey"];
        var suppliedKey = context.Request.Headers["X-Internal-Api-Key"].ToString();
        if (string.IsNullOrEmpty(expectedKey)
            || string.IsNullOrEmpty(suppliedKey)
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(expectedKey),
                System.Text.Encoding.UTF8.GetBytes(suppliedKey)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
