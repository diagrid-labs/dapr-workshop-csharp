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
