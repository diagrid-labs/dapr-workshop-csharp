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
