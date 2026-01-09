using Microsoft.AspNetCore.Mvc;
using PizzaStorefront.Services;
using PizzaStorefront.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IStorefrontService, StorefrontService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/order", async (
    Order order,
    ILogger<Program> logger,
    [FromServices]IStorefrontService storefrontService) =>
{
    logger.LogInformation("Received new order: {OrderId}", order.OrderId);
    var result = await storefrontService.ProcessOrderAsync(order);
    return Results.Ok(result);
});

app.Run();