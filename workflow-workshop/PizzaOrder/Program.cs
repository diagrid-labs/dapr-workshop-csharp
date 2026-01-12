using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using Dapr.AspNetCore;
using PizzaOrder.Models;
using PizzaOrder.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IOrderStateService, OrderStateService>();
builder.Services.AddDaprClient();

var app = builder.Build();
app.UseCloudEvents();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/order", async (
    Order order,
    ILogger<Program> logger,
    [FromServices]IOrderStateService orderStateService) =>
{
    logger.LogInformation("Received new order: {OrderId}", order.OrderId);
    var result = await orderStateService.UpdateOrderStateAsync(order);
    return Results.Ok(result);
});

app.MapGet("/order/{orderId}", async (
    string orderId,
    [FromServices]IOrderStateService orderStateService) =>
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
    [FromServices]IOrderStateService orderStateService) =>
{
    var order = await orderStateService.GetOrderAsync(orderId);
    if (order == null)
    {
        return Results.NotFound();
    }

    await orderStateService.DeleteOrderAsync(orderId);
    return Results.Ok(orderId);
});


app.MapPost("/order-sub", async (
    Order order,
    ILogger<Program> logger,
    [FromServices]IOrderStateService orderStateService) =>
{
    logger.LogInformation("Received order update for order {OrderId}",
            order.OrderId);
    var result = await orderStateService.UpdateOrderStateAsync(order);
    return Results.Ok();
});


app.Run();