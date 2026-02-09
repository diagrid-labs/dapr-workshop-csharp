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
