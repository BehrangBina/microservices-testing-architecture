using Microsoft.Playwright;

namespace E2eTests;

/// <summary>
/// Wraps Playwright's IAPIRequestContext for typed HTTP calls to each microservice.
/// Base URLs are read from environment variables so they work both locally and in CI.
///
/// Note: Playwright's API request context is used here for its built-in retry/wait
/// capabilities and rich assertion support — not for browser automation.
/// </summary>
public sealed class PlaywrightApiContext : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private IAPIRequestContext? _context;

    public string UserServiceUrl    { get; } = Env("USER_SERVICE_URL",    "http://localhost:5001");
    public string OrderServiceUrl   { get; } = Env("ORDER_SERVICE_URL",   "http://localhost:5002");
    public string PaymentServiceUrl { get; } = Env("PAYMENT_SERVICE_URL", "http://localhost:5003");
    public string NotifyServiceUrl  { get; } = Env("NOTIFY_SERVICE_URL",  "http://localhost:5004");

    private PlaywrightApiContext(IPlaywright playwright) => _playwright = playwright;

    public static async Task<PlaywrightApiContext> CreateAsync()
    {
        var pw = await Playwright.CreateAsync();
        var ctx = new PlaywrightApiContext(pw);
        ctx._context = await pw.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = "http://localhost", // overridden per-call via full URLs
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept"] = "application/json",
                ["Content-Type"] = "application/json"
            }
        });
        return ctx;
    }

    public Task<IAPIResponse> GetAsync(string url)    => _context!.GetAsync(url);
    public Task<IAPIResponse> PostAsync(string url, object body) =>
        _context!.PostAsync(url, new APIRequestContextOptions { DataObject = body });

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.DisposeAsync();
        _playwright.Dispose();
    }

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) ?? fallback;
}
