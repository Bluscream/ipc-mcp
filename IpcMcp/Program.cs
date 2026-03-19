using System.Security.Principal;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ModelContextProtocol.AspNetCore;
using IpcMcp.Services;

var builder = WebApplication.CreateBuilder(args);

// Ensure all hostnames are allowed (Fixes 400 Bad Request)
builder.Configuration["AllowedHosts"] = "*";

// Configure port and URLs
var port = int.Parse(Environment.GetEnvironmentVariable("IPC_MCP_PORT") ?? "23481");
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

// Parse command line arguments for token
var token = args.FirstOrDefault(arg => arg.StartsWith("-token:"))?.Split(':')[1] 
    ?? args.SkipWhile(arg => arg != "-token").Skip(1).FirstOrDefault();

if (string.IsNullOrEmpty(token))
{
    Console.Error.WriteLine("ERROR: Token is required. Use -token <your-token>");
    Environment.Exit(1);
}

// Add services
builder.Services.AddSingleton<NamedPipeService>();
builder.Services.AddSingleton<MemoryMappedFileService>();
builder.Services.AddSingleton<PInvokeService>();
builder.Services.AddSingleton<ComService>();
builder.Services.AddSingleton<ProcessService>();
builder.Services.AddSingleton<McpService>();
builder.Services.AddSingleton<ServiceService>();
builder.Services.AddSingleton<WindowsService>();
builder.Services.AddSingleton<RegistryService>();
builder.Services.AddSingleton<LogonRegistryService>();
builder.Services.AddSingleton<UpdateService>();
builder.Services.AddSingleton(new TokenService(token));

// Configure authentication
builder.Services.AddAuthentication("Token")
    .AddScheme<TokenAuthenticationSchemeOptions, TokenAuthenticationHandler>(
        "Token", options => { });

builder.Services.AddAuthorization();

// Add MCP server with HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Configure CORS - allow any origin for Tailscale/LAN access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Type", "Cache-Control", "Last-Event-ID");
    });
});

var app = builder.Build();

// Check admin privileges
if (!IsRunningAsAdmin())
{
    Console.Error.WriteLine("WARNING: Not running with Administrator privileges.");
    Console.Error.WriteLine("Some IPC operations may require admin access.");
}

app.UseRouting();

// Add middleware to normalize /mcp path to /mcp/ for streamable HTTP
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null && path.TrimEnd('/') == "/mcp" && !path.EndsWith("/"))
    {
        context.Request.Path = new PathString(path + "/");
    }
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Serve local dashboard directly from the application
app.UseFileServer(new FileServerOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "Dashboard")),
    RequestPath = "/dashboard",
    EnableDefaultFiles = true
});

// Map MCP server with authorization at /mcp endpoint
app.MapMcp("/mcp").RequireAuthorization();

Console.WriteLine($"IPC MCP Server starting on port {port}");
Console.WriteLine("MCP Protocol: HTTP");
Console.WriteLine("Authentication: Token required");
Console.WriteLine("Binding: All interfaces (0.0.0.0)");
Console.WriteLine("MCP Endpoint: /mcp");

app.Run();

static bool IsRunningAsAdmin()
{
    try
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}

// Token service to store the token
public class TokenService
{
    public string Token { get; }
    
    public TokenService(string token)
    {
        Token = token;
    }
}

// Authentication scheme options
public class TokenAuthenticationSchemeOptions : AuthenticationSchemeOptions { }

// Authentication handler
public class TokenAuthenticationHandler : AuthenticationHandler<TokenAuthenticationSchemeOptions>
{
    private readonly TokenService _tokenService;

    public TokenAuthenticationHandler(
        IOptionsMonitor<TokenAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TokenService tokenService)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip authentication for CORS preflight requests
        if (Request.Method == "OPTIONS")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Check for SSE/EventStream requests - they may use query parameters for auth
        var acceptHeader = Request.Headers["Accept"].FirstOrDefault();
        var isEventStream = acceptHeader != null && acceptHeader.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
        
        // Try to authenticate with token from various sources
        if (TryAuthenticateWithToken(isEventStream, out var principal))
        {
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        // Log authentication failure for debugging
        LogAuthenticationFailure(isEventStream);
        return Task.FromResult(AuthenticateResult.Fail("Invalid or missing token"));
    }

    private bool TryAuthenticateWithToken(bool isEventStream, out ClaimsPrincipal principal)
    {
        principal = null!;

        // Check query parameter for SSE requests
        if (isEventStream && Request.Query.TryGetValue("token", out var queryToken))
        {
            if (queryToken == _tokenService.Token)
            {
                principal = CreatePrincipal();
                return true;
            }
        }

        // Check Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var providedToken = authHeader.Substring("Bearer ".Length).Trim();
            if (providedToken == _tokenService.Token)
            {
                principal = CreatePrincipal();
                return true;
            }
        }

        // Check X-API-Token header
        var apiToken = Request.Headers["X-API-Token"].FirstOrDefault();
        if (apiToken == _tokenService.Token)
        {
            principal = CreatePrincipal();
            return true;
        }

        return false;
    }

    private ClaimsPrincipal CreatePrincipal()
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "authenticated") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
        return new ClaimsPrincipal(identity);
    }

    private void LogAuthenticationFailure(bool isEventStream)
    {
        var logLevel = isEventStream ? LogLevel.Debug : LogLevel.Warning;
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        var apiToken = Request.Headers["X-API-Token"].FirstOrDefault();
        var hasQueryToken = isEventStream && Request.Query.TryGetValue("token", out _);

        Logger.Log(
            logLevel,
            "Authentication failed for {Method} {Path} (SSE: {IsEventStream}). Headers: Authorization={AuthHeader}, X-API-Token={ApiToken}, QueryToken={QueryToken}",
                Request.Method,
                Request.Path,
            isEventStream,
                authHeader != null ? "present" : "missing",
            apiToken != null ? "present" : "missing",
            hasQueryToken ? "present" : "missing");

        if (isEventStream)
        {
            Logger.LogError(
                "SSE authentication failed - token missing or invalid. Ensure token is sent in Authorization header (Bearer), X-API-Token header, or query parameter 'token'");
        }
    }
}
