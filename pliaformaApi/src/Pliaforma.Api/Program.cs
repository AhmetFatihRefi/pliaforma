using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Pliaforma.Api.Json;
using Pliaforma.Api.Middleware;
using Pliaforma.Api.RateLimiting;
using Pliaforma.Api.Setup;
using Pliaforma.Application;
using Pliaforma.Infrastructure;
using Pliaforma.Persistence;
using Pliaforma.Persistence.Contexts;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Üretimde yapısal (JSON) log.
if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

// nginx vb. ters proxy arkasında gerçek istemci IP'si (rate limit için).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

const string CorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                     ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials())); // httpOnly refresh cookie için gerekli.

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Giden tüm DateTime'lar Türkiye saatine normalize edilir (UTC→İstanbul; duvar saati olduğu gibi).
        o.JsonSerializerOptions.Converters.Add(new TurkeyDateTimeJsonConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Pliaforma API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

builder.Services.AddAppRateLimiting(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı değil.");

builder.Services
    .AddApplication()
    .AddPersistence(connectionString)
    .AddInfrastructure(builder.Configuration, builder.Environment.IsProduction(), builder.Environment.IsDevelopment());

// Kimlik/doğrulama ayarları (e-posta doğrulama zorunluluğu vb.). Varsayılan PROD-güvenli (zorunlu).
builder.Services.Configure<Pliaforma.Application.Common.Settings.AuthSettings>(
    builder.Configuration.GetSection(Pliaforma.Application.Common.Settings.AuthSettings.SectionName));

// Hatırlatma worker'ı (idempotent; Notifications:WorkerEnabled ile kapatılabilir).
builder.Services.AddHostedService<Pliaforma.Api.Workers.ReminderBackgroundService>();

// Sprint 31D-1: retention purge worker (AuditPurge:Enabled ile açılır; ilk deploy DryRun=true).
builder.Services.AddHostedService<Pliaforma.Api.Workers.AuditRetentionPurgeBackgroundService>();

var app = builder.Build();

app.UseForwardedHeaders();

// Migration + seed (fail-fast: hata olursa uygulama açılmaz).
await app.MigrateAndSeedAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Sprint 31-MFA: mfa_pending (kurulum beklenen) token'ları yalnız MFA-setup uçlarına izinli kıl (API enforcement).
app.UseMiddleware<Pliaforma.Api.Middleware.MfaPendingGuardMiddleware>();

// Prometheus: her HTTP isteği için süre/sayı metrikleri toplanır.
app.UseHttpMetrics();

app.MapControllers();

// Prometheus scrape endpoint'i: /metrics (HTTP + .NET runtime/process metrikleri). Grafana bunu okur.
// Not: PROD'da dışarıya kapatın (nginx ile internal'e kısıtlayın); pilot/lokalde açık.
// AllowAnonymous: FallbackPolicy (RequireAuthenticatedUser) bu uçtaki scrape'i 401'e düşürmesin.
app.MapMetrics().AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await context.Response.WriteAsync(payload);
    },
}).AllowAnonymous();

app.Logger.LogInformation("Pliaforma API başladı. Ortam={Environment}", app.Environment.EnvironmentName);

app.Run();
