using Dapr.Client;
using Dapr.Workflow;
using PizzaWorkflow.Models;

namespace PizzaWorkflow.Activities;

public class ValidationActivity : WorkflowActivity<Order, Order>
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<ValidationActivity> _logger;

    public ValidationActivity(DaprClient daprClient, ILogger<ValidationActivity> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    public override async Task<Order> RunAsync(WorkflowActivityContext context, Order order)
    {
        try
        {
            _logger.LogInformation("Starting validation process for order {OrderId}", order.OrderId);
            
            await _daprClient.SaveStateAsync(
                "pizzastatestore",
                $"validation_{order.OrderId}",
                new { order.OrderId, Status = "pending_validation" });

            _logger.LogInformation("Validation state saved for order {OrderId}", order.OrderId);
            
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in validation process for order {OrderId}", order.OrderId);
            throw;
        }
    }
}