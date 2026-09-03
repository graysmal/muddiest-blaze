using Audit.Core;
using BlazorApp1.Components;
using BlazorApp1.Context;
using BlazorApp1.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using Prometheus;
using Serilog;
using Serilog.Core;
using Serilog.Enrichers;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot("wwwroot").UseStaticWebAssets();

// https://codewithmukesh.com/blog/structured-logging-with-serilog-in-aspnet-core/
// https://github.com/serilog-contrib/serilog-sinks-grafana-loki
// used fluent api rather than appsettings.json because the json serilog object is pretty gross,
// and there are likely not many changes to be made to these settings per deployment.

const string fileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Name} " +
                                  "({preferred_username}, {ClientIp}, {MachineName}) " +
                                  "trace:{TraceId} req:{RequestId} {Message:lj}{NewLine}{Exception}";
builder.Services.AddSerilog(lc => lc
    .MinimumLevel.ControlledBy(new LoggingLevelSwitch(builder.Configuration.GetValue("Logging:MinimumLevel", LogEventLevel.Information)))
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        builder.Configuration.GetValue<string>("Logging:Path")??"./Logs/app.log", 
        rollingInterval: RollingInterval.Day, retainedFileCountLimit:14, outputTemplate:fileOutputTemplate)
    .WriteTo.GrafanaLoki(
        builder.Configuration.GetValue<string>("Loki:uri"), 
        [new LokiLabel { Key="app", Value="web_app"}])
    .Enrich.FromLogContext()
    .Enrich.WithClientIp(IpVersionPreference.Ipv4Only)
    .Enrich.WithSpan()
    .Enrich.WithRequestHeader("User-Agent")
    .Enrich.WithUserClaims("Name", "preferred_username")
    .Enrich.WithMachineName());

// https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0&pivots=without-yarp-and-aspire#supply-configuration-with-the-json-configuration-provider-app-settings
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(opts =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(opts);
        opts.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = _ =>
            {
                using (AuditScope.Create("OIDC:LoginAttempt", () => new {})) { }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                context.HttpContext.User = context.Principal!;
                using (AuditScope.Create("OIDC:LoginSuccess", () => new {})) { }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                using (AuditScope.Create("OIDC:LoginFailed", () => new {})) { }
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogError("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                // log should only be made if there is an actual user requesting logout; null can be thrown out.
                // this event triggers twice when a user logs out, once as the identity and once as null.
                var (name, preferredUsername) = GetLoggingClaims(context.HttpContext.User);
                if (name is null && preferredUsername is null) return Task.CompletedTask;
                using (AuditScope.Create("OIDC:LogoutRequest", () => new {})) { }
                return Task.CompletedTask;
            },
            OnSignedOutCallbackRedirect = context =>
            {
                using (AuditScope.Create("OIDC:LogoutSuccess", () => new {})) { }
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User successfully logged out.");
                return Task.CompletedTask;
            }
        };
    });

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices()
    .AddAuthorization()
    .AddControllersWithViews()
    .AddMicrosoftIdentityUI();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("APP_Blazor_Admin", policy =>
        policy.RequireClaim("groups", "3c659545-aec6-40b4-aff6-5bfa069e7e10"));
});

var httpContextAccessor = new HttpContextAccessor(); // create httpcontext to share between httpcontext service and Audit.Configuration.
builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);

// https://stackoverflow.com/questions/43749236/net-core-x-forwarded-proto-not-working
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Important: Clear known networks/proxies if you are in a Docker network 
    // where the proxy IP is dynamic or not a local loopback
    options.KnownIPNetworks.Clear(); 
    options.KnownProxies.Clear();
});

builder.Services.AddHttpClient<LokiService>((services, client) =>
{
    var uri = services.GetRequiredService<IConfiguration>().GetValue<string>("Loki:uri")
        ?? throw new InvalidOperationException("Loki:uri not configured.");
    client.BaseAddress = new Uri(uri);
});
builder.Services.AddSingleton<PythonService>();

var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ??
                 throw new InvalidOperationException("Database Provider not specified.");
switch (dbProvider)
{
    case "PostgreSQL":
        var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSQL") ??
                                  throw new InvalidOperationException("PostgreSQL connection string not configured.");
        builder.Services.AddDbContextFactory<PostgresContext>(options =>
        {
            options.UseNpgsql(pgConnectionString);
        });
        Configuration.Setup()
         .UsePostgreSql(config => config
             .ConnectionString(pgConnectionString)
             .TableName("audit_event")
             .CustomColumn("event_date", ev => ev.StartDate)
             .CustomColumn("event_type", ev => ev.EventType)
             .CustomColumn("name", ev => ev.CustomFields["name"])
             .CustomColumn("preferred_username", ev => ev.CustomFields["preferred_username"])
             .CustomColumn("client_ip", ev => ev.CustomFields["client_ip"])
             .CustomColumn("machine_name", ev => ev.CustomFields["machine_name"])
             .CustomColumn("user_agent", ev => ev.CustomFields["user_agent"])
         );
        break;
    case "SQLServer":
        var msConnectionString = builder.Configuration.GetConnectionString("SQLServer") ??
                                  throw new InvalidOperationException("SQLServer connection string not configured.");
        builder.Services.AddDbContextFactory<PostgresContext>(options =>
        {
            options.UseSqlServer(msConnectionString);
        });
        Configuration.Setup()
            .UseSqlServer(config => config
                .ConnectionString(msConnectionString)
                .TableName("audit_event")
                .CustomColumn("event_date", ev => ev.StartDate)
                .CustomColumn("event_type", ev => ev.EventType)
                .CustomColumn("name", ev => ev.CustomFields["name"])
                .CustomColumn("preferred_username", ev => ev.CustomFields["preferred_username"])
                .CustomColumn("client_ip", ev => ev.CustomFields["client_ip"])
                .CustomColumn("machine_name", ev => ev.CustomFields["machine_name"])
                .CustomColumn("user_agent", ev => ev.CustomFields["user_agent"])
            );
        break;
}

Configuration.AddCustomAction(ActionType.OnScopeCreated, scope =>
{
    var httpContext = httpContextAccessor.HttpContext;
    if (httpContext is null) return;
    var (name, preferredUsername) =  GetLoggingClaims(httpContext.User);
    scope.SetCustomField("name", name);
    scope.SetCustomField("preferred_username", preferredUsername);
    scope.SetCustomField("client_ip", httpContext.Connection.RemoteIpAddress?.ToString());
    scope.SetCustomField("machine_name", Environment.MachineName);
    scope.SetCustomField("user_agent", httpContext.Request.Headers.UserAgent.ToString());
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, _) =>
        httpContext.Request.Path.StartsWithSegments("/metrics")
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.MapGet("/api/runs/{id:guid}/download", async (Guid id, PostgresContext db) =>
{
    var run = await db.PythonRuns.FindAsync(id);
    if (run is null) { return Results.NotFound(); }
    var zipPath = $"{Path.GetTempPath()}Scripts/run-{id}/run-{id}-output.zip";
    if (!File.Exists(zipPath)) { return Results.NotFound(); }
    return Results.File(zipPath, "application/zip", $"run-{run.ScriptName}-{run.Started:s}-output.zip");
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpMetrics();
app.MapMetrics();
app.MapControllers();
app.Run();
return;

// gets Name and preferred_username claims for OIDC log events.
static (string? Name, string? PreferredUsername) GetLoggingClaims(ClaimsPrincipal? principal)
{
    if (principal?.Identity?.IsAuthenticated != true)
        return (null, null);

    var name = principal.FindFirst("name")?.Value;
    var preferredUsername = principal.FindFirst("preferred_username")?.Value;
    return (name, preferredUsername);
}