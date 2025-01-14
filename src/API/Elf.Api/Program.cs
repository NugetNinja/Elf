using AspNetCoreRateLimit;
using Elf.Api;
using Elf.Api.Auth;
using Elf.Api.Data;
using Elf.Api.Features;
using Elf.Api.TokenGenerator;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Microsoft.Identity.Web;
using Polly;
using System.Data;
using System.Reflection;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddAzureWebAppDiagnostics();

builder.Host.ConfigureAppConfiguration(config =>
{
    if (bool.Parse(builder.Configuration["AppSettings:PreferAzureAppConfiguration"]))
    {
        config.AddAzureAppConfiguration(options =>
        {
            options.Connect(builder.Configuration["ConnectionStrings:AzureAppConfig"])
                .ConfigureRefresh(refresh =>
                {
                    refresh.Register("Elf:Settings:Sentinel", true)
                           .SetCacheExpiration(TimeSpan.FromSeconds(10));
                })
                .UseFeatureFlags(o => o.Label = "Elf");
        });
    }
});

ConfigureServices(builder.Services);

var app = builder.Build();

#region First Run

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ElfDbContext>();
    var canConnect = await context.Database.CanConnectAsync();
    if (!canConnect)
    {
        app.MapGet("/", () => Results.Problem(
            detail: "Database connection test failed, please check your connection string and firewall settings, then RESTART Elf manually.",
            statusCode: 500
            ));
        app.Run();
    }

    await context.Database.EnsureCreatedAsync();
}

#endregion

ConfigureMiddleware(app);
ConfigureEndpoints(app);

app.Run();

void ConfigureServices(IServiceCollection services)
{
    builder.Services.AddMediatR(Assembly.GetExecutingAssembly());

    // Fix docker deployments on Azure App Service blows up with Azure AD authentication
    // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-6.0
    // "Outside of using IIS Integration when hosting out-of-process, Forwarded Headers Middleware isn't enabled by default."
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    services.AddOptions();
    services.AddFeatureManagement();

    var redisConn = builder.Configuration.GetConnectionString("RedisConnection");
    if (!string.IsNullOrWhiteSpace(redisConn))
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConn;
        });
    }
    else
    {
        services.AddDistributedMemoryCache();
    }

    services.Configure<List<ApiKey>>(builder.Configuration.GetSection("ApiKeys"));
    services.AddScoped<IGetApiKeyQuery, AppSettingsGetApiKeyQuery>();
    services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    services.AddDistributedRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.Configure<RouteOptions>(options =>
    {
        options.LowercaseUrls = true;
        options.LowercaseQueryStrings = true;
        options.AppendTrailingSlash = false;
    });

    services.AddDbContext<ElfDbContext>(options => options.UseLazyLoadingProxies()
            .UseSqlServer(builder.Configuration.GetConnectionString("ElfDatabase"))
            .EnableDetailedErrors());

    // Azure
    if (bool.Parse(builder.Configuration["AppSettings:PreferAzureAppConfiguration"]))
    {
        services.AddAzureAppConfiguration();
    }
    services.AddApplicationInsightsTelemetry();

    // Elf
    services.AddSingleton<CannonService>();
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddApiKeySupport(_ => { })
            .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    services.AddAuthorization();

    services.AddScoped<IDbConnection>(_ => new SqlConnection(builder.Configuration.GetConnectionString("ElfDatabase")));
    services.AddSingleton<ITokenGenerator, ShortGuidTokenGenerator>();
    services.AddScoped<ILinkVerifier, LinkVerifier>();

    services.AddHttpClient<IIPLocationService, IPLocationService>()
            .AddTransientHttpErrorPolicy(x => x.WaitAndRetryAsync(3, retryCount => TimeSpan.FromSeconds(Math.Pow(2, retryCount))));

    services.AddCors(o => o.AddPolicy("local", x =>
    {
        x.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader();
    }));
}

void ConfigureMiddleware(IApplicationBuilder appBuilder)
{
    appBuilder.UseForwardedHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.UseCors("local");
        app.UseSwagger();
        app.UseSwaggerUI();
        var tc = app.Services.GetRequiredService<TelemetryConfiguration>();
        tc.DisableTelemetry = true;
        TelemetryDebugWriter.IsTracingDisabled = true;
        appBuilder.UseDeveloperExceptionPage();
    }
    else
    {
        appBuilder.UseStatusCodePages();
    }

    appBuilder.UseHsts();
    appBuilder.UseHttpsRedirection();

    if (bool.Parse(app.Configuration["AppSettings:PreferAzureAppConfiguration"]))
    {
        appBuilder.UseAzureAppConfiguration();
    }

    appBuilder.UseStaticFiles();
    appBuilder.UseIpRateLimiting();
    appBuilder.UseRouting();
    appBuilder.UseAuthentication();
    appBuilder.UseAuthorization();
}

void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
{
    endpoints.MapGet("/", (HttpContext httpContext) =>
    {
        httpContext.Response.Headers.Add("X-Elf-Version", Utils.AppVersion);
        var obj = new
        {
            ElfVersion = Utils.AppVersion,
            DotNetVersion = Environment.Version.ToString(),
            EnvironmentTags = Utils.GetEnvironmentTags()
        };

        return obj;
    });

    endpoints.MapControllers();
}