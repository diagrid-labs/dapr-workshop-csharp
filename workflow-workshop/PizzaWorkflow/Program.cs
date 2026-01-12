using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using Dapr.Workflow;
using PizzaWorkflow.Activities;
using PizzaWorkflow.Models;
using PizzaWorkflow.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDaprClient();

builder.Services.AddDaprWorkflow(options =>
{
  // Register workflows
  options.RegisterWorkflow<PizzaOrderingWorkflow>();

  // Register activities
  options.RegisterActivity<StorefrontActivity>();
  options.RegisterActivity<CookingActivity>();
  options.RegisterActivity<ValidationActivity>();
  options.RegisterActivity<DeliveryActivity>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/workflow/start-order", async (
    Order order,
    ILogger<Program> logger,
    [FromServices] DaprWorkflowClient daprWorkflowClient) =>
{
    try
    {
        logger.LogInformation("Starting workflow for order {OrderId}", order.OrderId);
        var instanceId = $"pizza-order-{order.OrderId}";
        
        await daprWorkflowClient.ScheduleNewWorkflowAsync(
            nameof(PizzaOrderingWorkflow),
            instanceId,
            order);
        logger.LogInformation("Workflow started successfully for order {OrderId}", order.OrderId);
        return Results.Ok(new
        {
            order_id = order.OrderId,
            workflow_instance_id = instanceId,
            status = "started"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to start workflow for order {OrderId}", order.OrderId);
        throw;
    }
});

app.MapGet("/workflow/get-status", async (
    ManageWorkflowRequest request,
    ILogger<Program> logger,
    [FromServices] DaprWorkflowClient daprWorkflowClient) =>
{
    var instanceId = $"pizza-order-{request.OrderId}";
        
    try
    {
        logger.LogInformation("Getting workflow status for order {OrderId}", request.OrderId);
        var status = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
        logger.LogInformation("Workflow status retrieved successfully for order {request.OrderId}", request.OrderId);
        return Results.Ok(new
        {
            order_id = request.OrderId,
            status
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get workflow status for order {OrderId}", request.OrderId);
        throw;
    }
});

app.MapPost("/workflow/validate-pizza", async (
    ValidationRequest request,
    ILogger<Program> logger,
    [FromServices] DaprWorkflowClient daprWorkflowClient) =>
{
    var instanceId = $"pizza-order-{request.OrderId}";
        
    try
    {
        logger.LogInformation("Getting workflow status for order {OrderId}", request.OrderId);
        await daprWorkflowClient.RaiseEventAsync(
            instanceId,
            "ValidationComplete",
            request);
        logger.LogInformation("Workflow status retrieved successfully for order {OrderId}", request.OrderId);
        return Results.Ok(new
        {
            order_id = request.OrderId,
            validation_status = request.Approved ? "approved" : "rejected"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get workflow status for order {OrderId}", request.OrderId);
        throw;
    }
});

app.MapPost("/workflow/pause-order", async (
    ManageWorkflowRequest request,
    ILogger<Program> logger,
    [FromServices] DaprWorkflowClient daprWorkflowClient) =>
{
    var instanceId = $"pizza-order-{request.OrderId}";
        
    try
    {
        logger.LogInformation("Pausing workflow for order {OrderId}", request.OrderId);
        await daprWorkflowClient.SuspendWorkflowAsync(instanceId);
        logger.LogInformation("Workflow paused successfully for order {OrderId}", request.OrderId);
        return Results.Ok(new
        {
            order_id = request.OrderId,
            status = "paused"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to pause workflow for order {orderId}", request.OrderId);
        throw;
    }
});

app.MapPost("/workflow/resume-order", async (
    ManageWorkflowRequest request,
    ILogger<Program> logger,
    [FromServices] DaprWorkflowClient daprWorkflowClient) =>
{
    var instanceId = $"pizza-order-{request.OrderId}";
        
    try
    {
        logger.LogInformation("Resuming workflow for order {OrderId}", request.OrderId);
        await daprWorkflowClient.ResumeWorkflowAsync(instanceId);
        logger.LogInformation("Workflow resumed successfully for order {OrderId}", request.OrderId);
        return Results.Ok(new
        {
            order_id = request.OrderId,
            status = "resumed"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to resume workflow for order {OrderId}", request.OrderId);
        throw;
    }
});

app.MapPost("/workflow/cancel-order", async (
    ManageWorkflowRequest request,
    ILogger<Program> logger,
    [FromServices] DaprWorkflowClient daprWorkflowClient) =>
{
    var instanceId = $"pizza-order-{request.OrderId}";
        
    try
    {
        logger.LogInformation("Cancelling workflow for order {OrderId}", request.OrderId);
        await daprWorkflowClient.TerminateWorkflowAsync(instanceId);
        logger.LogInformation("Workflow cancelled successfully for order {OrderId}", request.OrderId);
        return Results.Ok(new
        {
            order_id = request.OrderId,
            status = "terminated"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to cancel workflow for order {OrderId}", request.OrderId);
        throw;
    }
});

app.Run();
