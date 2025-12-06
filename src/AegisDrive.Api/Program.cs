using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Events;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Extensions;
using AegisDrive.Api.Features.Ingestion.Consumers;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.Email;
using AegisDrive.Api.Shared.Services;
using AegisDrive.Infrastructure.Services.Notification.Templates;
using Amazon;
using Amazon.S3;
using Amazon.SimpleEmail;
using Amazon.SQS;
using Carter;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// 1. CONFIGURE SWAGGER
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
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



builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions()); 

// --- APPLICATION CONFIGURATION ---


builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.ConfigurationSection));


builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<S3Settings>(builder.Configuration.GetSection(S3Settings.SectionName));
builder.Services.Configure<SqsSettings>(builder.Configuration.GetSection(SqsSettings.SectionName));


builder.Services.AddAWSService<IAmazonSQS>(); // By Default Singleton
//builder.Services.AddSingleton<IAmazonSQS>();
builder.Services.AddHostedService<CriticalEventSqsConsumer>();




//builder.Services.AddAWSMessageBus(bus =>
//{
//    var sqsSettings = builder.Configuration.GetSection(SqsSettings.SectionName).Get<SqsSettings>();

//    // ---------------------------------------------------------
//    // 1. CRITICAL Queue & Handler
//    // ---------------------------------------------------------
//    if (!string.IsNullOrEmpty(sqsSettings?.DrowsinessCriticalEventsQueue))
//    {
//        bus.AddSQSPoller(sqsSettings.DrowsinessCriticalEventsQueue, options =>
//        {
//        })
//     .AddMessageHandler<CriticalEventMessageHandler, CriticalEventMessage>();
//    }

//    // ---------------------------------------------------------
//    // 2. REGULAR Queue & Handler
//    // ---------------------------------------------------------
//    if (!string.IsNullOrEmpty(sqsSettings?.DrowsinessEventsQueue))
//    {
//        bus.AddSQSPoller(sqsSettings.DrowsinessEventsQueue)
//           .AddMessageHandler<SafetyEventHandler, SafetyEventMessage>();
//    }

//    // Global Settings
//    bus.ConfigureBackoffPolicy(cfg => cfg.UseCappedExponentialBackoff());
//});


//add aws service 
builder.Services.AddAWSService<IAmazonSimpleEmailService>();

builder.Services.AddKeyedScoped<INotificationService, SesNotificationService>("Email");

// --- DATABASE & IDENTITY ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// LOGGING
builder.Services.AddLogging();




// 2. CONFIGURE JWT AUTHENTICATION
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
});

builder.Services.AddAuthorization();

// --- CUSTOM APPLICATION SERVICES ---
//builder.Services.AddScoped<ITokenProvider, JwtTokenProvider>();
builder.Services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
builder.Services.AddScoped<IDbIntializer, DbIntializer>();
builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();



// --- EXTERNAL SERVICES (AWS, REDIS) ---
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var s3Settings = sp.GetRequiredService<IOptions<S3Settings>>().Value;
    var config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(s3Settings.Region) };
    return new AmazonS3Client(config);
});



var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
if (string.IsNullOrEmpty(redisConnectionString))    
    throw new ArgumentNullException("RedisConnection string is missing in configuration");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));





// --- LIBRARIES ---
var assembly = typeof(Program).Assembly;

builder.Services.AddCarter(configurator: config => config.WithValidatorLifetime(ServiceLifetime.Scoped));
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(assembly);

    // Register the Transaction Pipeline Behavior (using the shared IResult marker)
    cfg.AddOpenBehavior(typeof(TransactionalMiddleware.TransactionPipelineBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(assembly);


var app = builder.Build();

await app.IntializeDataBase();

//var emailService = app.Services.GetRequiredService<IAmazonSimpleEmailService>();
//await EmailTemplates.InitializeTemplates(emailService);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();



app.Run();


