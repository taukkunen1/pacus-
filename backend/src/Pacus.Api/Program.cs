using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Infrastructure.Auth;
using Pacus.Infrastructure.Mongo;
using Pacus.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddHttpContextAccessor();

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPacusRepository, PacusRepository>();
builder.Services.AddScoped<IHabitatRepository, HabitatRepository>();
builder.Services.AddScoped<IDailyRoutineRepository, DailyRoutineRepository>();
builder.Services.AddScoped<ITaskTemplateRepository, TaskTemplateRepository>();
builder.Services.AddScoped<IPointTransactionRepository, PointTransactionRepository>();
builder.Services.AddScoped<ITaskEventRepository, TaskEventRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IPacusGrowthRepository, PacusGrowthRepository>();

// Auth
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBootstrapService, BootstrapService>();

// Services
builder.Services.AddScoped<IPointsService, PointsService>();
builder.Services.AddScoped<IDailyRoutineService, DailyRoutineService>();
builder.Services.AddScoped<IDayClosingService, DayClosingService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<ITaskTemplateService, TaskTemplateService>();

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
builder.Services.AddCors(options =>
{
    var origins =
        Environment.GetEnvironmentVariable(
            "CORS_ALLOWED_ORIGINS")
        ?? builder.Configuration["Cors:AllowedOrigins"]
        ?? "http://localhost:5500";

    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                origins.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

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