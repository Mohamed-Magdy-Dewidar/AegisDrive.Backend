using AegisDrive.Api.Contracts;
using AegisDrive.Api.CustomMiddleWares;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities.Identity;
using AegisDrive.Api.Extensions;
using AegisDrive.Api.Features.Ingestion.Consumers;
using AegisDrive.Api.Hubs;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.Auth;
using AegisDrive.Api.Shared.Email;
using AegisDrive.Api.Shared.Services;
using Amazon;
using Amazon.S3;
using Amazon.SimpleEmail;
using Amazon.SQS;
using Carter;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =================================================================
// 1. CONFIGURATION & SETTINGS
// =================================================================
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.ConfigurationSection));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<S3Settings>(builder.Configuration.GetSection(S3Settings.SectionName));
builder.Services.Configure<SqsSettings>(builder.Configuration.GetSection(SqsSettings.SectionName));


// =================================================================
// 2. INFRASTRUCTURE (Database, Redis, AWS)
// =================================================================

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddIdentityCore<ApplicationUser>(options => { /* Identity options here */ })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();


// Redis Cache
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
if (string.IsNullOrEmpty(redisConnectionString))
    throw new ArgumentNullException("RedisConnection string is missing in configuration");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));



// AWS Services
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonSimpleEmailService>();




// S3 Service (Manual registration to inject custom settings if needed, or use AddAWSService<IAmazonS3>)
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var s3Settings = sp.GetRequiredService<IOptions<S3Settings>>().Value;
    var config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(s3Settings.Region) };
    return new AmazonS3Client(config);
});



// SIGNALR
builder.Services.AddSignalR();



// =================================================================
// 3. APPLICATION SERVICES (DI)
// =================================================================

// Generic Repositories & Helpers
builder.Services.AddScoped<ITokenProvider, JwtTokenProvider>();
builder.Services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
builder.Services.AddScoped<IDbIntializer, DbIntializer>();
builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();




// Notifications
// Register as Default AND Keyed to prevent "Unable to resolve" errors
builder.Services.AddScoped<INotificationService, SesNotificationService>();
builder.Services.AddKeyedScoped<INotificationService, SesNotificationService>("Email");

// Background Workers (SQS Consumers)
builder.Services.AddHostedService<CriticalEventSqsConsumer>();
builder.Services.AddHostedService<SafetyEventSqsConsumer>();


// =================================================================
// 4. LIBRARIES (MediatR, Carter, Validation)
// =================================================================
var assembly = typeof(Program).Assembly;

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(assembly);
    // Register Transactional Middleware
    cfg.AddOpenBehavior(typeof(TransactionalMiddleware.TransactionPipelineBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(assembly);
builder.Services.AddCarter(configurator: config => config.WithValidatorLifetime(ServiceLifetime.Scoped));


// =================================================================
// 5. API & SECURITY (Swagger, Auth)
// =================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
    };

    // NEW: Allow SignalR to authenticate via Query String
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for the Hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/fleet"))
            {
                // Read the token from the URL
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddLogging();


// Swagger UI
builder.Services.AddSwaggerGen(options =>
{
    // Fix schema conflicts (Command vs Command)
    options.CustomSchemaIds(type => type.ToString().Replace("+", "_"));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});



// Cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .SetIsOriginAllowed((host) => true) // Allows any origin (including file system)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()); // Essential for SignalR
});




// =================================================================
// 6. PIPELINE CONFIGURATION                                           
// =================================================================
var app = builder.Build();



// Initialization
await app.IntializeDataBase();

// Initialize Email Templates (Uncomment in Dev if needed)
// if (app.Environment.IsDevelopment()) {
//    using var scope = app.Services.CreateScope();
//    var emailService = scope.ServiceProvider.GetRequiredService<IAmazonSimpleEmailService>();
//    await EmailTemplates.InitializeTemplates(emailService);
// }


app.UseMiddleware<CustomExceptionHandlerMiddleware>();




if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AegisDrive FleetManagement API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// ---------------------------------------------------------
// CORS MUST BE HERE (Before Auth & SignalR)
// ---------------------------------------------------------
app.UseCors("AllowAll");



app.UseAuthentication();
app.UseAuthorization();
app.MapCarter();


// SignalR Hubs
app.MapHub<FleetHub>("/hubs/fleet");

app.Run();