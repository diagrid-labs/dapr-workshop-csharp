using PizzaStorefront.Models;
using Dapr.Client;

namespace PizzaStorefront.Services;

public interface IStorefrontService
{
    Task<Order> ProcessOrderAsync(Order order);
}

public class StorefrontService : IStorefrontService
{
    private readonly ILogger<StorefrontService> _logger;
    
    private readonly DaprClient _daprClient;
    private const string PUBSUB_NAME = "pizzapubsub";
    private const string TOPIC_NAME = "orders";

    public StorefrontService(DaprClient daprClient, ILogger<StorefrontService> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    public async Task<Order> ProcessOrderAsync(Order order)
    {
        var stages = new (string status, int duration)[]
        {
            ("validating", 1),
            ("processing", 2),
            ("confirmed", 1)
        };

        try
        {
            // Set pizza order status
            foreach (var (status, duration) in stages)
            {
                order.Status = status;
                _logger.LogInformation("Order {OrderId} - {Status}", order.OrderId, status);
                
                await _daprClient.PublishEventAsync(PUBSUB_NAME, TOPIC_NAME, order);
                await Task.Delay(TimeSpan.FromSeconds(duration));
            }

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", order.OrderId);
            order.Status = "failed";
            order.Error = ex.Message;

            await _daprClient.PublishEventAsync(PUBSUB_NAME, TOPIC_NAME, order);
            return order;
        }
    }
}