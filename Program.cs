using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using JobOnlineAPI.Repositories;
using JobOnlineAPI.Services;
using JobOnlineAPI.Models;
using JobOnlineAPI.DAL;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});
var logger = loggerFactory.CreateLogger<Program>();

// Log configuration sources
logger.LogInformation("Configuration sources:");
foreach (var source in builder.Configuration.Sources)
{
    logger.LogInformation(" - {Source}", source.GetType().Name);
}

// Clear default sources and add in specific order
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    // .AddUserSecrets<Program>(optional: false)
    .AddEnvironmentVariables();

// Log connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
logger.LogInformation("DefaultConnection: {ConnectionString}", connectionString ?? "null");

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<DapperContextHrms>();
builder.Services.AddSingleton<DapperContext>();

AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

builder.Services.AddScoped<IApplicantRepository, ApplicantRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
builder.Services.AddScoped<IHRStaffRepository, HRStaffRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ILdapService, LdapService>();
builder.Services.AddScoped<IConsentService, ConsentService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<FileStorageConfig>(
builder.Configuration.GetSection("FileStorage"));

builder.Services.AddSingleton(resolver =>
    resolver.GetRequiredService<IOptions<FileStorageConfig>>().Value);


builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        sp.GetRequiredService<ILogger<Program>>().LogError("Database connection string is not configured.");
        throw new InvalidOperationException("Database connection string is not configured.");
    }
    return new SqlConnection(connectionString);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtKey = builder.Configuration["JwtSettings:AccessSecret"];
    if (string.IsNullOrEmpty(jwtKey))
    {
        logger.LogError("JWT AccessSecret is not configured.");
        throw new InvalidOperationException("JWT AccessSecret is not configured.");
    }
    var issuer = builder.Configuration["JwtSettings:Issuer"];
    var audience = builder.Configuration["JwtSettings:Audience"];
    if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
    {
        logger.LogError("JWT Issuer or Audience is not configured.");
        throw new InvalidOperationException("JWT Issuer or Audience is not configured.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "JobOnlineAPI", Version = "v1" });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and your JWT token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.UseAllOfToExtendReferenceSchemas();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var error = new { Error = "An unexpected error occurred. Please try again later." };
            await context.Response.WriteAsJsonAsync(error);
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("An error occurred: {Error}", context.Response.StatusCode);
        });
    });
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DefaultModelsExpandDepth(-1);
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobOnlineAPI v1");
});

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();