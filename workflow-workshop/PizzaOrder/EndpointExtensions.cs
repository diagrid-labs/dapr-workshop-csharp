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
