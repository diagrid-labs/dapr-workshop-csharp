using Microsoft.AspNetCore.Mvc;
using PizzaKitchen.Models;
using PizzaKitchen.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICookService, CookService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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