namespace VibeTest.Server.Configuration;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public bool Enabled { get; set; } = true;

    public RateLimitPolicyOptions Global { get; set; } = new();
    public RateLimitPolicyOptions AuthLogin { get; set; } = new();
    public RateLimitPolicyOptions AuthRegisterRefresh { get; set; } = new();
}

public class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
}
