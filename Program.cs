using BlazorApp1.Components;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using NpgsqlTypes;
using Serilog;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using System.Security.Claims;
using Serilog.Enrichers.Span;

// gets Name and preferred_username claims for OIDC log events.
static (string? Name, string? PreferredUsername) GetLoggingClaims(ClaimsPrincipal? principal)
{
    if (principal?.Identity?.IsAuthenticated != true)
        return (null, null);

    var name = principal.FindFirst("name")?.Value;
    var preferredUsername = principal.FindFirst("preferred_username")?.Value;
    return (name, preferredUsername);
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// https://codewithmukesh.com/blog/structured-logging-with-serilog-in-aspnet-core/
// used fluent api rather than appsettings.json because the json object is pretty gross,
// and there are likely not many changes to be made to these settings per deployment.
// https://github.com/b00ted/serilog-sinks-postgresql
IDictionary<string, ColumnWriterBase> columnWriters = new Dictionary<string, ColumnWriterBase>
{
    {"timestamp", new TimestampColumnWriter() },
    {"level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
    {"name", new SinglePropertyColumnWriter("Name", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"preferred_username", new SinglePropertyColumnWriter("preferred_username", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"client_ip", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"machine_name", new SinglePropertyColumnWriter("MachineName", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"user_agent", new SinglePropertyColumnWriter("UserAgent", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"request_path", new SinglePropertyColumnWriter("RequestPath", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"request_method", new SinglePropertyColumnWriter("RequestMethod", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"status_code", new SinglePropertyColumnWriter("StatusCode", PropertyWriteMethod.Raw, NpgsqlDbType.Integer, "l") },
    {"elapsed_ms", new SinglePropertyColumnWriter("Elapsed", PropertyWriteMethod.Raw, NpgsqlDbType.Double, "l") },
    {"trace_id", new SinglePropertyColumnWriter("TraceId", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l")},
    {"request_id", new SinglePropertyColumnWriter("RequestId", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") },
    {"message", new RenderedMessageColumnWriter() },
    {"message_template", new MessageTemplateColumnWriter() },
    {"exception", new ExceptionColumnWriter() },
    {"properties", new LogEventSerializedColumnWriter() }
    
};
const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Name} ({preferred_username}, {ClientIp}, {MachineName}) trace:{TraceId} req:{RequestId} {Message:lj}{NewLine}{Exception}";
builder.Services.AddSerilog((services, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("./logs/muddiest.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit:null, outputTemplate:outputTemplate)
    .WriteTo.PostgreSQL(builder.Configuration.GetConnectionString("PostgreSQL"), "Log", columnWriters,
        needAutoCreateTable: true)
    .Enrich.FromLogContext()
    .Enrich.WithClientIp(IpVersionPreference.Ipv4Only)
    .Enrich.WithCorrelationId()
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
            OnRedirectToIdentityProvider = context =>
            {
                // no name specified at this point.
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User attempting login.");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var (name, preferredUsername) = GetLoggingClaims(context.Principal);
                var diagnosticContext = context.HttpContext.RequestServices.GetRequiredService<IDiagnosticContext>();
                diagnosticContext.Set("Name", name);
                diagnosticContext.Set("preferred_username", preferredUsername);
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User {Name} ({preferred_username}) successfully logged in.", name, preferredUsername);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogError("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                // log only is made if there is an actual user requesting logout; null can be thrown out.
                // this event triggers twice when a user logs out, once as the identity and once as null.
                var (name, preferredUsername) = GetLoggingClaims(context.HttpContext.User);
                if (name is not null || preferredUsername is not null) 
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User {Name} ({preferredUsername}) requested logout.", name, preferredUsername);
                }
                return Task.CompletedTask;
            },
            OnSignedOutCallbackRedirect = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User successfully logged out.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// https://stackoverflow.com/questions/43749236/net-core-x-forwarded-proto-not-working
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Important: Clear known networks/proxies if you are in a Docker network 
    // where the proxy IP is dynamic or not a local loopback
    options.KnownIPNetworks.Clear(); 
    options.KnownProxies.Clear();
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();


app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();