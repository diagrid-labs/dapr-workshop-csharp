using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaDelivery;
using PizzaDelivery.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IDeliveryService, DeliveryService>();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/healthz");
app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapServiceEndpoints();

app.Run();

