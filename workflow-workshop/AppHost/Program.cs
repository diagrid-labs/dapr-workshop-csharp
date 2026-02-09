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
