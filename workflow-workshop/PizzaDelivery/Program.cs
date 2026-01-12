using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using PizzaDelivery.Models;
using PizzaDelivery.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDaprClient();
builder.Services.AddSingleton<IDeliveryService, DeliveryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/delivery", async (
    [FromBody] Order order,
    ILogger<Program> logger,
    [FromServices]IDeliveryService deliveryService) =>
{
    logger.LogInformation("Starting delivery for order: {OrderId}", order.OrderId);
    var result = await deliveryService.DeliverPizzaAsync(order);
    return Results.Ok(result);
});

app.Run();

