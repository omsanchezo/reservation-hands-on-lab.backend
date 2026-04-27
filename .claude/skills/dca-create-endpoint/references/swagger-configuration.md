# Swagger Configuration

Swagger/OpenAPI setup in FastEndpoints: installation, endpoint documentation, schema customization, authentication, and UI configuration.

---

## Installation

```bash
dotnet add package FastEndpoints.Swagger
```

```xml
<PackageReference Include="FastEndpoints" Version="7.0.1" />
<PackageReference Include="FastEndpoints.Swagger" Version="7.0.1" />
```

---

## Basic Setup

### Service Registration (before app.Build())

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hashira Stone API",
        Version = "v1",
        Description = "API for Hashira Stone Backend",
        Contact = new OpenApiContact
        {
            Name = "APSYS Development Team",
            Email = "dev@apsys.mx"
        }
    });

    // JWT Bearer authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
```

### Middleware Configuration (after app.Build())

```csharp
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hashira Stone API v1");
    c.RoutePrefix = string.Empty;          // Serve at app root
    c.DocExpansion(DocExpansion.None);      // Collapse all sections
    c.DisplayRequestDuration();            // Show request timing
});

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api";
    config.Serializer.Options.PropertyNamingPolicy = null;
});

app.UseSwaggerGen();
```

---

## Documenting Endpoints

### Description Method

Fluent API for simple, quick metadata:

```csharp
public override void Configure()
{
    Get("users/{userId}");
    Policies("MustBeApplicationUser");

    Description(d => d
        .WithTags("Users")
        .WithName("GetUser")
        .WithDescription("Retrieves a user by their unique identifier")
        .Produces<GetUserResponse>(200, "application/json")
        .ProducesProblemDetails(404)
        .ProducesProblemDetails(401)
        .ProducesProblemDetails(403));
}
```

Full options:

```csharp
Description(d => d
    .WithTags("Users", "Administration")
    .WithName("GetUserById")
    .WithDescription("Detailed description here")
    .WithSummary("Short summary")
    .Accepts<GetUserRequest>("application/json")
    .Produces<GetUserResponse>(200)
    .Produces<ErrorResponse>(400)
    .ProducesProblemDetails(404)
    .WithMetadata(new CustomMetadata())
    .ClearDefaultProduces()
    .ExcludeFromDescription());
```

### Summary Method

Detailed documentation with request/response examples:

```csharp
public override void Configure()
{
    Put("technical-standards/{id}");
    Policies("MustBeApplicationAdministrator");

    Summary(s =>
    {
        s.Summary = "Update a technical standard";
        s.Description = "Updates an existing technical standard with new information. " +
                        "Only administrators can perform this operation.";
        s.RequestParam(r => r.Id, "The unique identifier of the technical standard to update");
        s.Response<UpdateTechnicalStandardResponse>(200, "Technical standard updated successfully");
        s.Response(400, "Invalid request data");
        s.Response(401, "Unauthorized - Missing or invalid authentication token");
        s.Response(403, "Forbidden - User does not have administrator privileges");
        s.Response(404, "Technical standard not found");
        s.Response(409, "Conflict - A technical standard with this name already exists");

        s.ExampleRequest = new UpdateTechnicalStandardRequest
        {
            Id = 1,
            Name = "NOM-001-SEDE-2012",
            Description = "Electrical installations (utilization)",
            Category = "Electrical",
            Version = "2.0",
            IsActive = true
        };
    });
}
```

### When to Use Which

- **Description()**: Simple endpoints, straightforward metadata, no examples needed
- **Summary()**: Complex endpoints, parameter docs, request/response examples, multiple status codes
- Do NOT mix both methods on the same endpoint

---

## Tags and Operation Names

```csharp
// Tags group endpoints in Swagger UI -- group by feature/domain
Description(d => d.WithTags("Users"));
Description(d => d.WithTags("Prototypes"));
Description(d => d.WithTags("Authentication"));

// Operation names must be unique across all endpoints
.WithName("GetUserById")
.WithName("CreateUser")
.WithName("UpdateUserEmail")
.WithName("ListUsers")
```

---

## Response Types

```csharp
// Using Produces<T>()
Description(d => d
    .Produces<GetUserResponse>(200, "application/json")
    .Produces<ErrorResponse>(400)
    .Produces(404)
    .Produces(401));

// Using ProducesProblemDetails()
Description(d => d
    .Produces<GetUserResponse>(200)
    .ProducesProblemDetails(400)
    .ProducesProblemDetails(404)
    .ProducesProblemDetails(500));

// Using Summary responses
Summary(s =>
{
    s.Response<UserResponse>(200, "User retrieved successfully");
    s.Response(400, "Invalid user ID format");
    s.Response(401, "Authentication required");
    s.Response(404, "User not found");
});
```

---

## Request Examples

```csharp
Summary(s =>
{
    s.ExampleRequest = new CreateUserRequest
    {
        Email = "john.doe@example.com",
        Name = "John Doe",
        PhoneNumber = "+1234567890",
        Roles = new[] { "User", "Viewer" },
        Department = "Engineering",
        IsActive = true
    };
});
```

---

## Authentication in Swagger

Anonymous endpoints:

```csharp
public override void Configure()
{
    Post("auth/login");
    AllowAnonymous();
    Description(d => d
        .WithTags("Authentication")
        .WithDescription("Authenticates a user and returns a JWT token"));
}
```

---

## SwaggerUI Customization

```csharp
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
    c.RoutePrefix = "api-docs";
    c.DocumentTitle = "My API Documentation";
    c.DocExpansion(DocExpansion.List);
    c.DefaultModelsExpandDepth(-1);       // Hide schema section
    c.DisplayOperationId();
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
    c.EnableFilter();
    c.EnableTryItOutByDefault();
    c.PersistAuthorization(true);         // Remember auth token
});
```

---

## XML Documentation for DTOs

Enable in `.csproj`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

Document DTOs:

```csharp
/// <summary>
/// Request to create a new user
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// User's email address (must be unique)
    /// </summary>
    /// <example>john.doe@example.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's full name
    /// </summary>
    /// <example>John Doe</example>
    public string Name { get; set; } = string.Empty;
}
```

Include in Swagger:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
```

---

## Anti-Patterns

- **No documentation**: every endpoint should have at least tags, operation name, and description
- **Undocumented response codes**: document all possible HTTP status codes
- **Generic operation names**: use `GetUserById` not `Get`
- **Missing request examples**: provide realistic examples for POST/PUT endpoints
- **Mixing Description() and Summary()**: choose one per endpoint
- **Hardcoded Swagger in production**: use environment checks to disable in production if needed
