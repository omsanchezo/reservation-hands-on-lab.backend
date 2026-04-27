# Authentication & Authorization

## JWT Bearer Authentication

The project uses JWT Bearer Authentication with Auth0 as Identity Provider. Requests include tokens via:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

JWT payload contains claims:

```json
{
  "email": "user@example.com",
  "username": "user@example.com",
  "name": "John Doe",
  "sub": "auth0|123456",
  "iat": 1699999999,
  "exp": 1700086399
}
```

## Middleware Configuration

Configure in `Program.cs` — order is critical:

```csharp
app.UseCors("CorsPolicy")
    .UseHttpsRedirection()
    .UseRouting()
    .UseAuthentication()    // Must come before UseAuthorization
    .UseAuthorization()     // Must come after UseAuthentication
    .UseFastEndpoints();
```

## JWT Bearer Setup

```csharp
public static IServiceCollection ConfigureIdentityServerClient(
    this IServiceCollection services,
    IConfiguration configuration)
{
    string? identityServerUrl = configuration.GetSection("IdentityServerConfiguration:Address").Value;
    if (string.IsNullOrEmpty(identityServerUrl))
        throw new InvalidOperationException("No identityServer configuration found in the configuration file");

    services.AddAuthentication("Bearer")
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = identityServerUrl;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false
            };
        });

    return services;
}
```

## Authorization Policies

Register policies in `ConfigurePolicy`:

```csharp
public static IServiceCollection ConfigurePolicy(this IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        options.AddPolicy("DefaultAuthorizationPolicy", policy =>
            policy.RequireAuthenticatedUser());

        options.AddPolicy("MustBeApplicationUser", policy =>
            policy.AddRequirements(new MustBeApplicationUser.Requirement()));

        options.AddPolicy("MustBeApplicationAdministrator", policy =>
            policy.Requirements.Add(new MustBeApplicationAdministrator.Requirement()));
    });

    services.AddScoped<IAuthorizationHandler, MustBeApplicationUser.Handler>();
    services.AddScoped<IAuthorizationHandler, MustBeApplicationAdministrator.Handler>();

    return services;
}
```

Built-in policy helpers:

```csharp
options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
options.AddPolicy("AdminOrManager", policy => policy.RequireRole("Admin", "Manager"));
options.AddPolicy("EmployeesOnly", policy => policy.RequireClaim("EmployeeNumber"));
options.AddPolicy("HROnly", policy => policy.RequireClaim("Department", "HR"));
options.AddPolicy("SeniorManagers", policy =>
    policy.RequireRole("Manager").RequireClaim("Seniority", "Senior"));
```

## Custom Authorization Handler Pattern

```csharp
public static class MyCustomPolicy
{
    public class Requirement : IAuthorizationRequirement
    {
        public string MinimumLevel { get; }
        public Requirement(string minimumLevel) { MinimumLevel = minimumLevel; }
    }

    public class Handler(IMyService myService) : AuthorizationHandler<Requirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, Requirement requirement)
        {
            var userLevel = context.User.FindFirst("Level")?.Value;
            if (string.IsNullOrEmpty(userLevel))
                return;  // Fail silently — never call context.Fail()

            var authorized = await myService.CheckLevelAsync(userLevel, requirement.MinimumLevel);
            if (authorized)
                context.Succeed(requirement);
        }
    }
}

// Register:
options.AddPolicy("RequiresLevel", policy =>
    policy.AddRequirements(new MyCustomPolicy.Requirement("Gold")));
services.AddScoped<IAuthorizationHandler, MyCustomPolicy.Handler>();
```

## Declarative Security in Endpoints

```csharp
// Policies (ALL must pass)
Policies("MustBeApplicationUser", "RequiresVerifiedEmail");

// Roles (ANY matches)
Roles("Admin", "SuperAdmin");

// Claims (ANY matches)
Claims("EmployeeID", "ContractorID");

// ClaimsAll (ALL must exist)
ClaimsAll("Verified", "Active");

// Permissions (ANY matches)
Permissions("Users.Update", "Users.Manage");

// PermissionsAll (ALL must exist)
PermissionsAll("Users.Delete", "Users.Manage");
```

Combining requirements — all groups joined with AND:

```csharp
public override void Configure()
{
    Post("/api/restricted");
    Claims("AdminID", "EmployeeID");           // (AdminID OR EmployeeID)
    Roles("Admin", "Manager");                 // AND (Admin OR Manager)
    Permissions("UpdateUsers", "DeleteUsers"); // AND (UpdateUsers OR DeleteUsers)
    Policies("VerifiedEmail");                 // AND VerifiedEmail policy
}
```

## Claims and IPrincipal

Centralize claim type constants:

```csharp
public static class ClaimTypeResource
{
    public const string Email = "email";
    public const string UserName = "username";
}
```

Extension methods for claim access:

```csharp
public static class IPrincipalExtender
{
    private const string EMAILCLAIMTYPE = "email";
    private const string USERNAMECLAIMTYPE = "username";
    private const string NAMECLAIMTYPE = "name";

    public static Claim? GetClaim(this IPrincipal principal, string claimType)
    {
        ClaimsPrincipal? claims = principal as ClaimsPrincipal;
        return claims?.FindFirst(claimType);
    }

    public static string GetClaimValue(this IPrincipal principal, string claimType)
        => principal.GetClaim(claimType)?.Value ?? string.Empty;

    public static string GetUserName(this IPrincipal principal)
    {
        var userName = principal.GetClaimValue(USERNAMECLAIMTYPE);
        if (string.IsNullOrEmpty(userName))
            throw new ConfigurationErrorsException(
                "No username claim found. Verify Auth0 custom action adds claims in login flow");
        return userName;
    }

    public static string GetUserEmail(this IPrincipal principal)
    {
        var email = principal.GetClaimValue(EMAILCLAIMTYPE);
        if (string.IsNullOrEmpty(email))
            throw new ConfigurationErrorsException(
                "No email claim found. Verify Auth0 custom action adds claims in login flow");
        return email;
    }

    public static string GetName(this IPrincipal principal)
        => principal.GetClaimValue(NAMECLAIMTYPE);
}
```

Usage in endpoints:

```csharp
var userEmail = User.GetUserName();  // Extension method on HttpContext.User
```

## Anonymous Access

```csharp
// Full anonymous access
AllowAnonymous();

// Anonymous only for specific verbs
Verbs(Http.GET, Http.POST, Http.PUT);
Routes("/users");
AllowAnonymous(Http.POST);  // Only POST is anonymous
```

## Anti-Patterns

1. **Authorization logic in handler instead of policies** — use `Policies()`, `Roles()`, etc. in `Configure()`.
2. **Magic strings for claims** — use `ClaimTypeResource` constants.
3. **Duplicated claim extraction** — use `IPrincipalExtender` methods.
4. **Hardcoded secrets** — read authority URL from `IConfiguration`.
5. **Calling `context.Fail()`** in handlers — return silently to let other handlers run.
6. **`AllowAnonymous()` on sensitive operations** — never on destructive endpoints.
7. **Missing audience/scheme defaults** when using multiple auth schemes — set `DefaultScheme` explicitly.
8. **Exposing sensitive data in claims** — never include passwords or SSNs.
