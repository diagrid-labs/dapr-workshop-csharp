using Dapr.Client;
using PizzaDelivery.Models;

namespace PizzaDelivery.Services;

public interface IDeliveryService
{
    Task<Order> DeliverPizzaAsync(Order order);
}

public class DeliveryService : IDeliveryService
{
    private readonly ILogger<DeliveryService> _logger;
    private readonly DaprClient _daprClient;
    private const string PUBSUB_NAME = "pizzapubsub";
    private const string TOPIC_NAME = "orders";

    public DeliveryService(DaprClient daprClient, ILogger<DeliveryService> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    public async Task<Order> DeliverPizzaAsync(Order order)
    {
        var stages = new (string status, int duration)[]
        {
            ("delivery_finding_driver", 2),
            ("delivery_driver_assigned", 1),
            ("delivery_picked_up", 2),
            ("delivery_on_the_way", 5),
            ("delivery_arriving", 2),
            ("delivery_at_location", 1)
        };

        try
        {
            foreach (var (status, duration) in stages)
            {
                order.Status = status;
                _logger.LogInformation("Order {OrderId} - {Status}", order.OrderId, status);

                await _daprClient.PublishEventAsync(PUBSUB_NAME, TOPIC_NAME, order);
                await Task.Delay(TimeSpan.FromSeconds(duration));
            }

            order.Status = "delivered";
            await _daprClient.PublishEventAsync(PUBSUB_NAME, TOPIC_NAME, order);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering order {OrderId}", order.OrderId);
            order.Status = "delivery_failed";
            order.Error = ex.Message;
            await _daprClient.PublishEventAsync(PUBSUB_NAME, TOPIC_NAME, order);
            return order;
        }
    }
}

