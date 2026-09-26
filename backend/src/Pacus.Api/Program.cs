using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Api.Middleware;
using Pacus.Api.Security;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Infrastructure.Auth;
using Pacus.Infrastructure.Mongo;
using Pacus.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Fly.io injeta FLY_APP_NAME em runtime. Tratamos esse sinal como fonte
// autoritativa de "producao" para controles de seguranca, mesmo se existir
// algum ASPNETCORE_ENVIRONMENT antigo/stale configurado no provedor.
var isFlyRuntime = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("FLY_APP_NAME"));
var useDevelopmentBehavior =
    builder.Environment.IsDevelopment() && !isFlyRuntime;

// Limites do Kestrel: corpo maximo de 1 MB (a API so recebe JSON pequeno) e sem
// cabecalho "Server" revelando a tecnologia.
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1_048_576;
});

// Carrega explicitamente os User Secrets do projeto.
// Isso evita depender apenas do carregamento automático do ambiente Development.
builder.Configuration.AddUserSecrets<Program>(optional: true);

// MongoDB
builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString =
        builder.Configuration["MongoDb:ConnectionString"]
        ?? builder.Configuration["MONGODB_URI"]
        ?? Environment.GetEnvironmentVariable("MONGODB_URI")
        ?? throw new InvalidOperationException(
            "MONGODB_URI nao configurada.");

    options.DatabaseName =
        builder.Configuration["MongoDb:DatabaseName"]
        ?? builder.Configuration["MONGODB_DATABASE"]
        ?? Environment.GetEnvironmentVariable("MONGODB_DATABASE")
        ?? "pacus";
});

builder.Services.AddSingleton<MongoDbContext>();

// JWT
var jwtSecret =
    builder.Configuration["Jwt:Secret"]
    ?? builder.Configuration["JWT_SECRET"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET");

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "JWT_SECRET nao configurada.");
}

// HS256 exige chave forte: menos de 32 caracteres facilita forca bruta offline do token.
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "JWT_SECRET deve ter pelo menos 32 caracteres.");
}

builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret = jwtSecret;
    options.Issuer =
        builder.Configuration["Jwt:Issuer"]
        ?? "pacus-api";

    options.Audience =
        builder.Configuration["Jwt:Audience"]
        ?? "pacus-clients";
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"]
                    ?? "pacus-api",

                ValidAudience =
                    builder.Configuration["Jwt:Audience"]
                    ?? "pacus-clients",

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret)),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddAuthorization();

// Rate limiting -- protege login (adulto/crianca) e criacao de familia contra
// forca bruta. PIN da crianca tem so 4 digitos (10.000 combinacoes), entao sem
// limite de tentativas da pra forcar bruta sem nenhum bloqueio. Particionado por
// IP real do cliente (cabecalho Fly-Client-IP do proxy do Fly.io, senao RemoteIpAddress).
// Auditoria de seguranca, Fase A item A1.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Teto geral por IP para toda a API (alem das politicas mais rigorosas abaixo):
    // limita scraping/abuso e tentativas de enumeracao em qualquer endpoint.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(GetClientIp(httpContext), _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy("auth", httpContext =>
    {
        var partitionKey = GetClientIp(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });

    options.AddPolicy("bootstrap", httpContext =>
    {
        var partitionKey = GetClientIp(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

builder.Services.AddHttpContextAccessor();

// Tratamento de excecao global (achado #1 da auditoria de API de 2026-09-01) --
// ver Middleware/AppExceptionHandler.cs pro mapeamento de cada tipo de excecao pro
// status HTTP certo. AddProblemDetails() e exigido pelo AddExceptionHandler mesmo
// quando a resposta final e o JSON customizado (o handler retorna true e assume o
// controle da resposta, ver TryHandleAsync).
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPacusRepository, PacusRepository>();
builder.Services.AddScoped<IHabitatRepository, HabitatRepository>();
builder.Services.AddScoped<IDailyRoutineRepository, DailyRoutineRepository>();
builder.Services.AddScoped<ITaskTemplateRepository, TaskTemplateRepository>();
builder.Services.AddScoped<IPointTransactionRepository, PointTransactionRepository>();
builder.Services.AddScoped<IWaterIntakeRepository, WaterIntakeRepository>();
builder.Services.AddScoped<ITaskEventRepository, TaskEventRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IPacusGrowthRepository, PacusGrowthRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IChatReadStateRepository, ChatReadStateRepository>();

// Auth
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBootstrapService, BootstrapService>();

// Services
builder.Services.AddScoped<IPointsService, PointsService>();
builder.Services.AddScoped<IDailyRoutineService, DailyRoutineService>();
builder.Services.AddScoped<IDayClosingService, DayClosingService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<ITaskTemplateService, TaskTemplateService>();
builder.Services.AddScoped<IDataExportService, DataExportService>();
builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();
builder.Services.AddScoped<IFamilyTimezoneService, FamilyTimezoneService>();
builder.Services.AddScoped<IAutonomyService, AutonomyService>();

// Cache em memoria usado por FamilyTimezoneService (revisao de API, 2026-09-11,
// achado #1) -- singleton por processo, coerente com FamilyTimezoneService sendo
// Scoped mas dependendo de um cache compartilhado entre requisicoes.
builder.Services.AddMemoryCache();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            JsonNamingPolicy.CamelCase;

        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));

        options.JsonSerializerOptions.Converters.Add(
            new ObjectIdJsonConverter());
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type =
                Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In =
                Microsoft.OpenApi.Models.ParameterLocation.Header
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType
                                    .SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

// CORS
// Em producao, somente o dominio oficial pode chamar a API pelo navegador.
// CORS_ALLOWED_ORIGINS continua util em Development para frontends locais,
// mas nao pode reabrir origens antigas em producao.
builder.Services.AddCors(options =>
{
    var configuredOrigins =
        Environment.GetEnvironmentVariable(
            "CORS_ALLOWED_ORIGINS")
        ?? builder.Configuration["Cors:AllowedOrigins"];

    var origins = CorsOriginPolicy.Resolve(
        useDevelopmentBehavior,
        configuredOrigins);

    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Migracao online, idempotente e retrocompativel: completa sourceType/sourceId das
// point_transactions criadas antes do ledger auditavel. Roda apenas nos documentos
// ainda incompletos, portanto reinicios subsequentes nao reescrevem o historico.
using (var migrationScope = app.Services.CreateScope())
{
    var points = migrationScope.ServiceProvider.GetRequiredService<IPointTransactionRepository>();
    var migrated = await points.BackfillSourceReferencesAsync();
    if (migrated > 0)
        app.Logger.LogInformation("Backfilled source references for {Count} point transactions.", migrated);

    var mongo = migrationScope.ServiceProvider.GetRequiredService<MongoDbContext>();
    var waterIndexKeys = MongoDB.Driver.Builders<Pacus.Domain.Entities.WaterIntake>.IndexKeys.Combine(
        MongoDB.Driver.Builders<Pacus.Domain.Entities.WaterIntake>.IndexKeys.Ascending(x => x.FamilyId),
        MongoDB.Driver.Builders<Pacus.Domain.Entities.WaterIntake>.IndexKeys.Ascending(x => x.EventId));
    var waterEventIndex = new MongoDB.Driver.CreateIndexModel<Pacus.Domain.Entities.WaterIntake>(
        waterIndexKeys,
        new MongoDB.Driver.CreateIndexOptions
        {
            Unique = true,
            Name = "water_event_idempotency"
        });
    await mongo.WaterIntakes.Indexes.CreateOneAsync(waterEventIndex);
}

if (useDevelopmentBehavior)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Primeiro na pipeline de proposito -- pra capturar excecao de qualquer middleware
// depois dele, nao so das actions dos controllers.
app.UseExceptionHandler();

// Cabecalhos de seguranca em toda resposta da API (JSON puro: nada de carregar
// scripts/estilos/frames, entao a CSP pode ser a mais restritiva possivel).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Resource-Policy"] = "same-site";
        return Task.CompletedTask;
    });

    await next();
});

if (!useDevelopmentBehavior)
{
    // HSTS: o navegador so volta a falar com a API por HTTPS.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors();

// So aplica rate limiting fora de Development -- os testes de integracao
// (PacusApiFactory) sobem o app em ambiente Development e fazem varias
// chamadas de bootstrap/login em sequencia no mesmo host; com o limiter
// ativo ali os testes comecariam a tomar 429 sem nenhuma relacao com o que
// estao validando. Em producao (Fly.io) o ambiente nao e Development, entao
// o limite continua valendo de verdade.
if (!useDevelopmentBehavior)
{
    app.UseRateLimiter();
}

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Em producao (Fly.io) a API fica atras de proxy reverso -- sem ler o IP real do
// cliente, todo mundo apareceria com o IP do proxy e compartilharia o mesmo limite.
static string GetClientIp(HttpContext context)
{
    // Fly-Client-IP e definido pelo proxy do Fly.io (sobrescreve qualquer valor enviado
    // pelo cliente), entao e a fonte confiavel do IP real. X-Forwarded-For NAO e usado:
    // o cliente pode enviar o proprio cabecalho com um IP falso a cada requisicao e
    // contornar o rate limit por completo.
    var flyIp = context.Request.Headers["Fly-Client-IP"].ToString();
    if (!string.IsNullOrWhiteSpace(flyIp))
    {
        return flyIp.Trim();
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

app.Run();

public sealed class ObjectIdJsonConverter
    : JsonConverter<ObjectId>
{
    public override ObjectId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (ObjectId.TryParse(value, out var objectId))
        {
            return objectId;
        }

        throw new JsonException(
            $"'{value}' nao e um ObjectId valido.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ObjectId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
