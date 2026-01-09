using Microsoft.AspNetCore.Mvc;
using PizzaKitchen.Models;
using PizzaKitchen.Services;
using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICookService, CookService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDaprClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/cook", async (
    Order order,
    ILogger<Program> logger,
    [FromServices]ICookService cookService) =>
{
    logger.LogInformation("Starting cooking for order: {OrderId}", order.OrderId);
    var result = await cookService.CookPizzaAsync(order);
    return Results.Ok(result);
});

app.Run();