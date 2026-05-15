using CompanyDirectory.Api.Data;
using CompanyDirectory.Api.Hubs;
using CompanyDirectory.Api.Identity;
using CompanyDirectory.Api.Middleware;
using CompanyDirectory.Api.Sms;
using Microsoft.AspNetCore.SignalR;
using CompanyDirectory.Infrastructure.Extensions;
using CompanyDirectory.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Identity.Web;
using Serilog;
using System.Security.Claims;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseWindowsService(options =>
        options.ServiceName = "CompanyDirectory API");

    // Serilog
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "Logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // Controllers
    builder.Services.AddControllers();

    // Swagger / OpenAPI — only for Development
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "CompanyDirectory API", Version = "v1" });
    });

    // Hybrid authentication: Windows (Negotiate) for on-prem, JWT Bearer (Entra ID) for remote/HomeOffice
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme          = "Hybrid";
        options.DefaultChallengeScheme = "Hybrid";
        options.DefaultAuthenticateScheme = "Hybrid";
    })
    .AddPolicyScheme("Hybrid", "Hybrid", opt =>
    {
        opt.ForwardDefaultSelector = ctx =>
        {
            var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
            return auth?.StartsWith("Bearer ") == true
                ? JwtBearerDefaults.AuthenticationScheme
                : NegotiateDefaults.AuthenticationScheme;
        };
    })
    .AddNegotiate()
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    var adminGroup  = builder.Configuration["Authentication:AdminGroup"] ?? string.Empty;
    var adminLogins = (builder.Configuration["Messages:AdminLogins"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    static string ExtractLogin(ClaimsPrincipal user)
    {
        var name = user.Identity?.Name ?? string.Empty;
        if (name.Contains('\\')) return name.Split('\\')[1].ToLowerInvariant();
        if (name.Contains('@'))  return name.Split('@')[0].ToLowerInvariant();
        var upn = user.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? string.Empty;
        if (upn.Contains('@')) return upn.Split('@')[0].ToLowerInvariant();
        return name.ToLowerInvariant();
    }

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = options.DefaultPolicy;

        options.AddPolicy("AdminOnly", policy =>
            policy.RequireAssertion(ctx =>
            {
                if (ctx.User.Identity?.IsAuthenticated != true) return false;
                var login = ExtractLogin(ctx.User);
                return adminLogins.Contains(login) ||
                       (!string.IsNullOrEmpty(adminGroup) && ctx.User.IsInRole(adminGroup));
            }));
    });

    // Rate limiting: 100 req/min per authenticated user
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("FixedWindow", opt =>
        {
            opt.PermitLimit = 100;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 10;
        });
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // Configuration sections
    builder.Services.Configure<CompanyDirectory.Shared.Configuration.LdapSettings>(
        builder.Configuration.GetSection(CompanyDirectory.Shared.Configuration.LdapSettings.SectionName));
    builder.Services.Configure<SearchSettings>(
        builder.Configuration.GetSection(SearchSettings.SectionName));

    // LDAP services from Infrastructure
    builder.Services.AddLdapServices(builder.Configuration);

    // Application services
    builder.Services.AddScoped<CompanyDirectory.Api.Services.IUserDirectoryService,
                               CompanyDirectory.Api.Services.UserDirectoryService>();

    // Messages DB + repository
    builder.Services.AddSingleton<MessageDbInitializer>();
    builder.Services.AddSingleton<IMessageRepository, MessageRepository>();
    builder.Services.AddSingleton<IChatRepository, ChatRepository>();

    // SMS gateway (SMSAPI.pl)
    builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection(SmsSettings.SectionName));
    builder.Services.AddHttpClient<ISmsGateway, SmsApiGateway>((sp, client) =>
    {
        var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmsSettings>>().Value;
        client.BaseAddress = new Uri(cfg.ApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiToken);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // SignalR
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IUserIdProvider, WindowsUserIdProvider>();

    // CORS — permissive only in Development
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

    var app = builder.Build();

    // Initialize messages database
    app.Services.GetRequiredService<MessageDbInitializer>().Initialize();

    // Exception handling middleware (before everything else)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CompanyDirectory API v1"));
        app.UseCors("DevCors");
    }

    app.UseSerilogRequestLogging();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<MessageHub>("/hubs/messages").RequireAuthorization();
    app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
