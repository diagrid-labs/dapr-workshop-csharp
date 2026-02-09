# Migration Plan: .NET Aspire Integration with Dapr

## Overview

This plan outlines the steps to migrate the `workflow-workshop` .NET applications to use **.NET Aspire 13.1.0** with **Dapr** integration. The workshop contains 5 microservices that communicate via Dapr for pub/sub messaging, state management, and workflow orchestration.

## Current Architecture

### Projects
| Project | Port | Dapr App ID | Purpose |
|---------|------|-------------|---------|
| PizzaStorefront | 8002 | pizza-storefront | Entry point for orders |
| PizzaOrder | 8001 | pizza-order | Order state management |
| PizzaKitchen | 8003 | pizza-kitchen | Pizza cooking service |
| PizzaDelivery | 8004 | pizza-delivery | Delivery service |
| PizzaWorkflow | 8005 | pizza-workflow | Dapr Workflow orchestration |

### Current Dapr Components
- **State Store**: Redis (`pizzastatestore`) - used by pizza-workflow and pizza-order
- **Pub/Sub**: Redis (`pizzapubsub`) - used by storefront, kitchen, delivery, order
- **Subscription**: Topic `orders` routed to `/order-sub`

### Current Technology Stack
- .NET 10.0
- Dapr SDK 1.16.1
- Dapr multi-app run via `dapr.yaml`

---

## Migration Goals

1. Replace `dapr.yaml` multi-app run with Aspire AppHost orchestration
2. Add Aspire Dashboard for observability
3. Integrate Dapr sidecars via CommunityToolkit.Aspire.Hosting.Dapr
4. Maintain all existing Dapr functionality (pub/sub, state, workflows)
5. Enable local development with Redis container managed by Aspire

---

## Phase 1: Create Aspire Infrastructure Projects

### 1.1 Create AppHost Project

Create a new Aspire AppHost project to orchestrate all services.

**File:** `AppHost/AppHost.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" Version="13.1.0" />
    <PackageReference Include="CommunityToolkit.Aspire.Hosting.Dapr" Version="13.0.0" />
    <PackageReference Include="Aspire.Hosting.Valkey" Version="13.1.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PizzaStorefront\PizzaStorefront.csproj" />
    <ProjectReference Include="..\PizzaOrder\PizzaOrder.csproj" />
    <ProjectReference Include="..\PizzaKitchen\PizzaKitchen.csproj" />
    <ProjectReference Include="..\PizzaDelivery\PizzaDelivery.csproj" />
    <ProjectReference Include="..\PizzaWorkflow\PizzaWorkflow.csproj" />
  </ItemGroup>

</Project>
```

**File:** `AppHost/Program.cs`

```csharp
using System.Collections.Immutable;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Add Valkey (Redis-compatible) for Dapr state store and pub/sub
var storagePassword = builder.AddParameter("storage-password", "zxczxc123", secret: true);
var storage = builder
    .AddValkey("storage", 16379, storagePassword)
    .WithContainerName("redis-state-store")
    .WithDataVolume("redis-state-store-data");

// Define Dapr components path
var resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "resources");

// Add services with Dapr sidecars - all services wait for storage to be ready
var pizzaOrder = builder.AddProject<Projects.PizzaOrder>("pizza-order")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "pizza-order",
        ResourcesPaths = ImmutableHashSet.Create(resourcesPath)
    });
pizzaOrder.WaitFor(storage);

var pizzaStorefront = builder.AddProject<Projects.PizzaStorefront>("pizza-storefront")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "pizza-storefront",
        ResourcesPaths = ImmutableHashSet.Create(resourcesPath)
    });
pizzaStorefront.WaitFor(storage);

var pizzaKitchen = builder.AddProject<Projects.PizzaKitchen>("pizza-kitchen")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "pizza-kitchen",
        ResourcesPaths = ImmutableHashSet.Create(resourcesPath)
    });
pizzaKitchen.WaitFor(storage);

var pizzaDelivery = builder.AddProject<Projects.PizzaDelivery>("pizza-delivery")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "pizza-delivery",
        ResourcesPaths = ImmutableHashSet.Create(resourcesPath)
    });
pizzaDelivery.WaitFor(storage);

var pizzaWorkflow = builder.AddProject<Projects.PizzaWorkflow>("pizza-workflow")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "pizza-workflow",
        ResourcesPaths = ImmutableHashSet.Create(resourcesPath)
    });
pizzaWorkflow.WaitFor(storage);

builder.Build().Run();
```

### 1.2 Create ServiceDefaults Project

Create a shared ServiceDefaults project for common Aspire configuration.

**File:** `ServiceDefaults/ServiceDefaults.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.5.0" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.5.0" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.14.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.14.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.14.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.14.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.14.0" />
  </ItemGroup>

</Project>
```

**File:** `ServiceDefaults/Extensions.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
```

---

## Phase 2: Update Existing Projects

### 2.1 Update Project Files

Add ServiceDefaults reference to each service project:

**Changes for each .csproj file (PizzaStorefront, PizzaOrder, PizzaKitchen, PizzaDelivery, PizzaWorkflow):**

Add to `<ItemGroup>`:
```xml
<ProjectReference Include="..\ServiceDefaults\ServiceDefaults.csproj" />
```

### 2.2 Create EndpointExtension Classes

Create an `EndpointExtensions.cs` file in each service project to organize endpoint mappings. Move all endpoint definitions from `Program.cs` to a static extension method.

#### PizzaStorefront/EndpointExtensions.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using PizzaStorefront.Models;
using PizzaStorefront.Services;

namespace PizzaStorefront;

public static class EndpointExtensions
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        app.MapPost("/storefront/order", async (
            [FromBody] Order order,
            ILogger<Program> logger,
            [FromServices] IStorefrontService storefrontService) =>
        {
            logger.LogInformation("Received new order: {OrderId}", order.OrderId);
            var result = await storefrontService.ProcessOrderAsync(order);
            return Results.Ok(result);
        });

        return app;
    }
}
```

#### PizzaOrder/EndpointExtensions.cs
```csharp
using Dapr;
using Microsoft.AspNetCore.Mvc;
using PizzaOrder.Models;
using PizzaOrder.Services;

namespace PizzaOrder;

public static class EndpointExtensions
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        app.MapPost("/order", async (
            [FromBody] Order order,
            ILogger<Program> logger,
            [FromServices] IOrderStateService orderStateService) =>
        {
            logger.LogInformation("Received new order: {OrderId}", order.OrderId);
            var result = await orderStateService.UpdateOrderStateAsync(order);
            return Results.Ok(result);
        });

        app.MapGet("/order/{orderId}", async (
            string orderId,
            [FromServices] IOrderStateService orderStateService) =>
        {
            var order = await orderStateService.GetOrderAsync(orderId);
            if (order == null)
            {
                return Results.NotFound();
            }
            return Results.Ok(order);
        });

        app.MapDelete("/order/{orderId}", async (
            string orderId,
            [FromServices] IOrderStateService orderStateService) =>
        {
            var order = await orderStateService.GetOrderAsync(orderId);
            if (order == null)
            {
                return Results.NotFound();
            }

            await orderStateService.DeleteOrderAsync(orderId);
            return Results.Ok(orderId);
        });

        // Programmatic Dapr pub/sub subscription using Topic attribute
        app.MapPost("/order-sub", [Topic("pizzapubsub", "orders")] async (
            [FromBody] Order order,
            ILogger<Program> logger,
            [FromServices] IOrderStateService orderStateService) =>
        {
            logger.LogInformation("Received order update for order {OrderId}", order.OrderId);
            var result = await orderStateService.UpdateOrderStateAsync(order);
            return Results.Ok();
        });

        return app;
    }
}
```

#### PizzaKitchen/EndpointExtensions.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using PizzaKitchen.Models;
using PizzaKitchen.Services;

namespace PizzaKitchen;

public static class EndpointExtensions
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        app.MapPost("/cook", async (
            [FromBody] Order order,
            ILogger<Program> logger,
            [FromServices] ICookService cookService) =>
        {
            logger.LogInformation("Starting cooking for order: {OrderId}", order.OrderId);
            var result = await cookService.CookPizzaAsync(order);
            return Results.Ok(result);
        });

        return app;
    }
}
```

#### PizzaDelivery/EndpointExtensions.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using PizzaDelivery.Models;
using PizzaDelivery.Services;

namespace PizzaDelivery;

public static class EndpointExtensions
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        app.MapPost("/delivery", async (
            [FromBody] Order order,
            ILogger<Program> logger,
            [FromServices] IDeliveryService deliveryService) =>
        {
            logger.LogInformation("Starting delivery for order: {OrderId}", order.OrderId);
            var result = await deliveryService.DeliverPizzaAsync(order);
            return Results.Ok(result);
        });

        return app;
    }
}
```

#### PizzaWorkflow/EndpointExtensions.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Dapr.Workflow;
using PizzaWorkflow.Models;
using PizzaWorkflow.Workflows;

namespace PizzaWorkflow;

public static class EndpointExtensions
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        app.MapPost("/workflow/start-order", async (
            [FromBody] Order order,
            ILogger<Program> logger,
            [FromServices] DaprWorkflowClient daprWorkflowClient) =>
        {
            try
            {
                logger.LogInformation("Starting workflow for order {OrderId}", order.OrderId);
                var instanceId = $"pizza-order-{order.OrderId}";

                await daprWorkflowClient.ScheduleNewWorkflowAsync(
                    nameof(PizzaOrderingWorkflow),
                    instanceId,
                    order);
                logger.LogInformation("Workflow started successfully for order {OrderId}", order.OrderId);
                return Results.Ok(new
                {
                    order_id = order.OrderId,
                    workflow_instance_id = instanceId,
                    status = "started"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start workflow for order {OrderId}", order.OrderId);
                throw;
            }
        });

        app.MapPost("/workflow/get-status", async (
            [FromBody] ManageWorkflowRequest request,
            ILogger<Program> logger,
            [FromServices] DaprWorkflowClient daprWorkflowClient) =>
        {
            var instanceId = $"pizza-order-{request.OrderId}";

            try
            {
                logger.LogInformation("Getting workflow status for order {OrderId}", request.OrderId);
                var status = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
                logger.LogInformation("Workflow status retrieved successfully for order {OrderId}", request.OrderId);
                return Results.Ok(new
                {
                    order_id = request.OrderId,
                    status
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get workflow status for order {OrderId}", request.OrderId);
                throw;
            }
        });

        app.MapPost("/workflow/validate-pizza", async (
            [FromBody] ValidationRequest request,
            ILogger<Program> logger,
            [FromServices] DaprWorkflowClient daprWorkflowClient) =>
        {
            var instanceId = $"pizza-order-{request.OrderId}";

            try
            {
                logger.LogInformation("Validating pizza for order {OrderId}", request.OrderId);
                await daprWorkflowClient.RaiseEventAsync(
                    instanceId,
                    "ValidationComplete",
                    request);
                logger.LogInformation("Validation complete for order {OrderId}", request.OrderId);
                return Results.Ok(new
                {
                    order_id = request.OrderId,
                    validation_status = request.Approved ? "approved" : "rejected"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to validate pizza for order {OrderId}", request.OrderId);
                throw;
            }
        });

        app.MapPost("/workflow/pause-order", async (
            [FromBody] ManageWorkflowRequest request,
            ILogger<Program> logger,
            [FromServices] DaprWorkflowClient daprWorkflowClient) =>
        {
            var instanceId = $"pizza-order-{request.OrderId}";

            try
            {
                logger.LogInformation("Pausing workflow for order {OrderId}", request.OrderId);
                await daprWorkflowClient.SuspendWorkflowAsync(instanceId);
                logger.LogInformation("Workflow paused successfully for order {OrderId}", request.OrderId);
                return Results.Ok(new
                {
                    order_id = request.OrderId,
                    status = "paused"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to pause workflow for order {OrderId}", request.OrderId);
                throw;
            }
        });

        app.MapPost("/workflow/resume-order", async (
            [FromBody] ManageWorkflowRequest request,
            ILogger<Program> logger,
            [FromServices] DaprWorkflowClient daprWorkflowClient) =>
        {
            var instanceId = $"pizza-order-{request.OrderId}";

            try
            {
                logger.LogInformation("Resuming workflow for order {OrderId}", request.OrderId);
                await daprWorkflowClient.ResumeWorkflowAsync(instanceId);
                logger.LogInformation("Workflow resumed successfully for order {OrderId}", request.OrderId);
                return Results.Ok(new
                {
                    order_id = request.OrderId,
                    status = "resumed"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resume workflow for order {OrderId}", request.OrderId);
                throw;
            }
        });

        app.MapPost("/workflow/cancel-order", async (
            [FromBody] ManageWorkflowRequest request,
            ILogger<Program> logger,
            [FromServices] DaprWorkflowClient daprWorkflowClient) =>
        {
            var instanceId = $"pizza-order-{request.OrderId}";

            try
            {
                logger.LogInformation("Cancelling workflow for order {OrderId}", request.OrderId);
                await daprWorkflowClient.TerminateWorkflowAsync(instanceId);
                logger.LogInformation("Workflow cancelled successfully for order {OrderId}", request.OrderId);
                return Results.Ok(new
                {
                    order_id = request.OrderId,
                    status = "terminated"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cancel workflow for order {OrderId}", request.OrderId);
                throw;
            }
        });

        return app;
    }
}
```

### 2.3 Update Program.cs Files

Refactor each service's `Program.cs` to use the new endpoint extensions and add Aspire service defaults:

#### PizzaStorefront/Program.cs
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaStorefront;
using PizzaStorefront.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IStorefrontService, StorefrontService>();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();
```

#### PizzaOrder/Program.cs
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaOrder;
using PizzaOrder.Services;
using Dapr.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IOrderStateService, OrderStateService>();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

var app = builder.Build();
app.UseCloudEvents();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable Dapr pub/sub subscription endpoint discovery
app.MapSubscribeHandler();
app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();
```

#### PizzaKitchen/Program.cs
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaKitchen;
using PizzaKitchen.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddSingleton<ICookService, CookService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();
```

#### PizzaDelivery/Program.cs
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaDelivery;
using PizzaDelivery.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDeliveryService, DeliveryService>();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();
```

#### PizzaWorkflow/Program.cs
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaWorkflow;
using Dapr.Workflow;
using PizzaWorkflow.Activities;
using PizzaWorkflow.Workflows;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

builder.Services.AddDaprWorkflow(options =>
{
    // Register workflows
    options.RegisterWorkflow<PizzaOrderingWorkflow>();

    // Register activities
    options.RegisterActivity<StorefrontActivity>();
    options.RegisterActivity<CookingActivity>();
    options.RegisterActivity<ValidationActivity>();
    options.RegisterActivity<DeliveryActivity>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();
```

---

## Phase 3: Update Solution File

### 3.1 Update DaprWorkshop.sln

Add the new Aspire projects to the solution:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "AppHost", "AppHost\AppHost.csproj", "{NEW-GUID-1}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ServiceDefaults", "ServiceDefaults\ServiceDefaults.csproj", "{NEW-GUID-2}"
EndProject
```

---

## Phase 4: Update Dapr Component Files (Required)

The Dapr component files in `resources/` must be updated to use the Valkey storage container managed by Aspire on port 16379.

### 4.1 Update statestore.yaml (Required)
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pizzastatestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: "localhost:16379"
  - name: redisPassword
    value: "zxczxc123"
  - name: actorStateStore
    value: "true"
scopes:
- pizza-workflow
- pizza-order
```

### 4.2 Update pubsub.yaml (Required)
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pizzapubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
  - name: redisHost
    value: "localhost:16379"
  - name: redisPassword
    value: "zxczxc123"
scopes:
- pizza-storefront
- pizza-kitchen
- pizza-delivery
- pizza-order
```

---

## Phase 5: Create Launch Profile

### 5.1 Add launchSettings.json for AppHost

**File:** `AppHost/Properties/launchSettings.json`

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:17178;http://localhost:15178",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:21178",
        "DOTNET_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:22178"
      }
    },
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:15178",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:19178",
        "DOTNET_RESOURCE_SERVICE_ENDPOINT_URL": "http://localhost:20178"
      }
    }
  }
}
```

---

## Implementation Checklist

### Prerequisites
- [ ] Ensure .NET 10 SDK is installed
- [ ] Ensure Dapr CLI is installed and initialized
- [ ] Ensure Docker is running (for Redis container)

### Phase 1: Create Infrastructure
- [ ] Create `AppHost` directory
- [ ] Create `AppHost.csproj`
- [ ] Create `AppHost/Program.cs`
- [ ] Create `AppHost/Properties/launchSettings.json`
- [ ] Create `ServiceDefaults` directory
- [ ] Create `ServiceDefaults.csproj`
- [ ] Create `ServiceDefaults/Extensions.cs`

### Phase 2: Update Services
- [ ] Create `PizzaStorefront/EndpointExtensions.cs`
- [ ] Update `PizzaStorefront.csproj` - add ServiceDefaults reference
- [ ] Update `PizzaStorefront/Program.cs` - refactor to use EndpointExtensions
- [ ] Create `PizzaOrder/EndpointExtensions.cs`
- [ ] Update `PizzaOrder.csproj` - add ServiceDefaults reference
- [ ] Update `PizzaOrder/Program.cs` - refactor to use EndpointExtensions
- [ ] Create `PizzaKitchen/EndpointExtensions.cs`
- [ ] Update `PizzaKitchen.csproj` - add ServiceDefaults reference
- [ ] Update `PizzaKitchen/Program.cs` - refactor to use EndpointExtensions
- [ ] Create `PizzaDelivery/EndpointExtensions.cs`
- [ ] Update `PizzaDelivery.csproj` - add ServiceDefaults reference
- [ ] Update `PizzaDelivery/Program.cs` - refactor to use EndpointExtensions
- [ ] Create `PizzaWorkflow/EndpointExtensions.cs`
- [ ] Update `PizzaWorkflow.csproj` - add ServiceDefaults reference
- [ ] Update `PizzaWorkflow/Program.cs` - refactor to use EndpointExtensions

### Phase 3: Update Solution
- [ ] Add AppHost project to solution
- [ ] Add ServiceDefaults project to solution

### Phase 4: Testing
- [ ] Build solution: `dotnet build`
- [ ] Run with Aspire: `dotnet run --project AppHost`
- [ ] Verify Aspire Dashboard opens
- [ ] Test pizza ordering workflow
- [ ] Verify Dapr sidecars are running
- [ ] Verify pub/sub messaging works
- [ ] Verify state store operations work
- [ ] Verify workflow execution works

---

## Final Project Structure

```
workflow-workshop/
├── DaprWorkshop.sln
├── AppHost/
│   ├── AppHost.csproj
│   ├── Program.cs
│   └── Properties/
│       └── launchSettings.json
├── ServiceDefaults/
│   ├── ServiceDefaults.csproj
│   └── Extensions.cs
├── PizzaDelivery/
│   ├── PizzaDelivery.csproj
│   ├── Program.cs
│   ├── EndpointExtensions.cs
│   ├── Models/
│   ├── Services/
│   └── Properties/
├── PizzaKitchen/
│   ├── PizzaKitchen.csproj
│   ├── Program.cs
│   ├── EndpointExtensions.cs
│   ├── Models/
│   ├── Services/
│   └── Properties/
├── PizzaOrder/
│   ├── PizzaOrder.csproj
│   ├── Program.cs
│   ├── EndpointExtensions.cs
│   ├── Models/
│   ├── Services/
│   └── Properties/
├── PizzaStorefront/
│   ├── PizzaStorefront.csproj
│   ├── Program.cs
│   ├── EndpointExtensions.cs
│   ├── Models/
│   ├── Services/
│   └── Properties/
├── PizzaWorkflow/
│   ├── PizzaWorkflow.csproj
│   ├── Program.cs
│   ├── EndpointExtensions.cs
│   ├── Activities/
│   ├── Models/
│   ├── Workflows/
│   └── Properties/
├── resources/
│   ├── pubsub.yaml
│   ├── statestore.yaml
│   └── subscription.yaml
└── Endpoints.http
```

---

## Running the Application

### With Aspire (New)
```bash
cd workflow-workshop
dotnet run --project AppHost
```

The Aspire Dashboard will automatically open, providing:
- Real-time service health monitoring
- Distributed tracing across all services
- Log aggregation
- Metrics visualization

### Without Aspire (Legacy - Keep for reference)
```bash
cd workflow-workshop
dapr run -f dapr.yaml
```

---

## Benefits of Migration

1. **Unified Orchestration**: Single entry point for all services
2. **Aspire Dashboard**: Built-in observability without additional setup
3. **Service Discovery**: Automatic service endpoint resolution
4. **Health Checks**: Standardized health endpoints across all services
5. **OpenTelemetry**: Distributed tracing and metrics out of the box
6. **Container Management**: Redis and other dependencies managed by Aspire
7. **Developer Experience**: Hot reload, live restart, and better debugging

---

## Package Versions Summary

| Package | Version |
|---------|---------|
| Aspire.Hosting.AppHost | 13.1.0 |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0 |
| Aspire.Hosting.Valkey | 13.1.0 |
| Microsoft.Extensions.Http.Resilience | 9.5.0 |
| Microsoft.Extensions.ServiceDiscovery | 9.5.0 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.14.0 |
| OpenTelemetry.Extensions.Hosting | 1.14.0 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.14.0 |
| OpenTelemetry.Instrumentation.Http | 1.14.0 |
| OpenTelemetry.Instrumentation.Runtime | 1.14.0 |
| Dapr.AspNetCore | 1.16.1 (existing) |
| Dapr.Client | 1.16.1 (existing) |
| Dapr.Workflow | 1.16.1 (existing) |

---

## Potential Issues & Mitigations

### 2. Dapr Component Files Compatibility
**Issue:** The existing `resources/*.yaml` Dapr component files use `localhost:6379` for Redis, which won't work when Aspire manages the Redis container (dynamic port assignment).

**Resolution:** Use Valkey (Redis-compatible) with a fixed port (16379) and password. Update the Dapr component files to use `localhost:16379` with the configured password. All services use `WaitFor(storage)` to ensure the storage container is ready before starting.

### 3. Missing `using` Statement in AppHost
**Issue:** The `AppHost/Program.cs` uses `ImmutableHashSet` without the required `using` statement.

**Resolution:** Add `using System.Collections.Immutable;` or use the simpler API that doesn't require it.


### 5. Missing Dapr Subscription Handling
**Issue:** When using Aspire-managed Dapr, programmatic subscriptions are preferred over YAML-based subscriptions for better maintainability and type safety.

**Resolution:** Use programmatic subscriptions instead of declarative YAML subscriptions:
1. Add `app.MapSubscribeHandler()` in `PizzaOrder/Program.cs` to enable subscription endpoint discovery
2. Add `[Topic("pizzapubsub", "orders")]` attribute to the `/order-sub` endpoint in `PizzaOrder/EndpointExtensions.cs`
3. Add `using Dapr;` to import the `Topic` attribute
4. Remove or comment out the `resources/subscription.yaml` file (no longer needed)

The `[Topic]` attribute parameters are:
- First parameter: pub/sub component name (`pizzapubsub`)
- Second parameter: topic name (`orders`)

### 6. Port Configuration
**Issue:** The current services have hardcoded ports in `dapr.yaml`. Aspire dynamically assigns ports.

**Mitigation:** Remove hardcoded port configurations from service `appsettings.json` files if present, or use Aspire's port configuration.

### 7. Dapr Sidecar Startup Timing
**Issue:** Services may start before Dapr sidecars are ready, causing initial connection failures.

**Mitigation:** Add health checks and retry logic (already partially addressed by `Microsoft.Extensions.Http.Resilience`).

### 8. Missing `using` for JsonOptions
**Issue:** The Program.cs files use `JsonOptions` from `Microsoft.AspNetCore.Http.Json` but the Dapr client configuration needs `System.Text.Json.JsonSerializerOptions`.

**Mitigation:** Ensure both using statements are present:
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
```

### 9. CloudEvents Middleware
**Issue:** `PizzaOrder/Program.cs` uses `app.UseCloudEvents()` but this needs to be before endpoint mapping.

**Mitigation:** Ensure correct middleware ordering:
```csharp
app.UseCloudEvents();
app.MapSubscribeHandler();  // Add this for programmatic subscriptions
app.MapDefaultEndpoints();
app.MapServiceEndpoints();
```
