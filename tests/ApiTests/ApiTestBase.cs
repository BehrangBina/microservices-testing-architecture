using RestSharp;

namespace ApiTests;

/// <summary>
/// Base class for all API tests.
/// Reads service base URLs from environment variables so tests work both locally
/// (against docker-compose) and in CI pipelines.
/// </summary>
public abstract class ApiTestBase
{
    protected RestClient UserClient    { get; } = new(Env("USER_SERVICE_URL",    "http://localhost:5001"));
    protected RestClient OrderClient   { get; } = new(Env("ORDER_SERVICE_URL",   "http://localhost:5002"));
    protected RestClient PaymentClient { get; } = new(Env("PAYMENT_SERVICE_URL", "http://localhost:5003"));
    protected RestClient NotifyClient  { get; } = new(Env("NOTIFY_SERVICE_URL",  "http://localhost:5004"));

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) ?? fallback;
}
