using BlazorApp1.Components;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;


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
    {"raise_date", new TimestampColumnWriter(NpgsqlDbType.Timestamp) },
    {"level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
    {"message", new RenderedMessageColumnWriter(NpgsqlDbType.Text) },
    {"message_template", new MessageTemplateColumnWriter(NpgsqlDbType.Text) },
    {"exception", new ExceptionColumnWriter(NpgsqlDbType.Text) },
    {"properties", new LogEventSerializedColumnWriter(NpgsqlDbType.Jsonb) },
    {"props_test", new PropertiesColumnWriter(NpgsqlDbType.Jsonb) },
    {"machine_name", new SinglePropertyColumnWriter("MachineName", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") }
};
builder.Services.AddSerilog((services, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.PostgreSQL(builder.Configuration.GetConnectionString("PostgreSQL"), "Log", columnWriters,
        needAutoCreateTable: true)
    .Enrich.FromLogContext());


// https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0&pivots=without-yarp-and-aspire#supply-configuration-with-the-json-configuration-provider-app-settings
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(opts =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(opts);
        opts.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User attempting login.");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var identity =  context.Principal?.Identity;
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User {Name} successfully logged in.", identity!.Name);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogError("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                // TODO: add identity name to response cookies and retrieve them in signedoutcallbackredirect to log the specific Identity's logout.
                var identity = context.HttpContext.User.Identity;
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation("User {Name} requested logout.", identity!.Name);
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