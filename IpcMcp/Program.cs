using System.Security.Principal;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using IpcMcp.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure port and URLs
var port = int.Parse(Environment.GetEnvironmentVariable("IPC_MCP_PORT") ?? "23481");
builder.WebHost.UseUrls($"http://localhost:{port}");
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(port);
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
builder.Services.AddControllers();
builder.Services.AddSingleton<NamedPipeService>();
builder.Services.AddSingleton<MemoryMappedFileService>();
builder.Services.AddSingleton<PInvokeService>();
builder.Services.AddSingleton<ComService>();

// Store token for middleware
builder.Services.AddSingleton(new TokenService(token));

// Configure authentication
builder.Services.AddAuthentication("Token")
    .AddScheme<TokenAuthenticationSchemeOptions, TokenAuthenticationHandler>(
        "Token", options => { });

// Configure CORS - only allow localhost
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:*", "http://127.0.0.1:*")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .SetIsOriginAllowed(origin => 
              {
                  try
                  {
                      var uri = new Uri(origin);
                      return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                  }
                  catch
                  {
                      return false;
                  }
              });
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
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization();

Console.WriteLine($"IPC MCP Server starting on http://localhost:{port}");
Console.WriteLine("MCP Protocol: HTTP");
Console.WriteLine("Authentication: Token required");
Console.WriteLine("Binding: localhost only");

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
        ISystemClock clock,
        TokenService tokenService)
        : base(options, logger, encoder, clock)
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

        // Check for token in Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var providedToken = authHeader.Substring("Bearer ".Length).Trim();
            if (providedToken == _tokenService.Token)
            {
                var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "authenticated") };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name);
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        // Check for token in X-API-Token header
        var apiToken = Request.Headers["X-API-Token"].FirstOrDefault();
        if (apiToken == _tokenService.Token)
        {
            var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "authenticated") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name);
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid or missing token"));
    }
}
