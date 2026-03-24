using Microsoft.EntityFrameworkCore;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Services DI ───

builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MAGI API Gateway",
        Version = "v1",
        Description = "API Gateway для системы автоматизации управления контентом Telegram-каналов MAGI. "
                    + "Обеспечивает единую точку входа для взаимодействия с микросервисами: Parser, Tagger, Publisher."
    });
});

// ─── EF Core + SQLite ───
var dbPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "magi.db");
dbPath = Path.GetFullPath(dbPath);
builder.Services.AddDbContext<MagiDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// HTTP-клиент для Python-сервисов
builder.Services.AddHttpClient<PythonServiceClient>();

// Бизнес-сервисы
builder.Services.AddSingleton<ProcessManager>();
builder.Services.AddSingleton<ServiceOrchestrator>();
builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<ChannelService>();

// Миграция данных из JSON
builder.Services.AddTransient<DataMigrationService>();

// CORS (для AdminPanel и фронтенда)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ─── Startup: создание БД и миграция данных из JSON ───
using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<DataMigrationService>();
    await migrationService.MigrateAsync();
}

// ─── Middleware ───

app.UseCors();

// Global exception handler — всегда возвращаем JSON, даже при необработанных ошибках
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var message = error?.Error?.Message ?? "Internal server error";
        var json = System.Text.Json.JsonSerializer.Serialize(new { success = false, message });
        await context.Response.WriteAsync(json);
    });
});

// Swagger доступен всегда (не только в Development)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MAGI API Gateway v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

app.Run();
