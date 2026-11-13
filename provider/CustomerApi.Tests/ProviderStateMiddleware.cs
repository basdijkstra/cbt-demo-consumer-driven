using System.Text.Json;
using CustomerApi.Data;
using CustomerApi.Models;
using Microsoft.AspNetCore.Http;

namespace CustomerApi.Tests;

/// <summary>
/// Handles the provider state change requests that the Pact verifier sends
/// before replaying each interaction from the contract.
/// </summary>
public class ProviderStateMiddleware
{
    public const string Path = "/provider-states";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly Dictionary<string, Action> _stateHandlers;

    public ProviderStateMiddleware(RequestDelegate next, ICustomerRepository repository)
    {
        _next = next;
        _stateHandlers = new Dictionary<string, Action>
        {
            ["customer 12345 exists"] = () => repository.Add(new Customer(
                Id: 12345,
                Name: "Susan Jones",
                Status: "ACTIVE",
                Address: new Address("Main Street", 15, "New York", "Y1A 2B3"),
                Email: "susan.jones@example.com",
                Phone: "+1-555-555-0123",
                LastModified: new DateTime(2026, 10, 15, 10, 30, 12, DateTimeKind.Utc))),
            ["customer 99999 does not exist"] = () => repository.Remove(99999),
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var providerState = await JsonSerializer.DeserializeAsync<ProviderState>(context.Request.Body, JsonOptions);

        // Only act on setup calls; teardown needs no work for this in-memory store.
        // Interactions without a 'given' clause arrive with an empty state: nothing to do.
        if (!string.IsNullOrWhiteSpace(providerState?.State) && providerState.Action is null or "setup")
        {
            if (!_stateHandlers.TryGetValue(providerState.State, out var handler))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync($"Unknown provider state '{providerState.State}'");
                return;
            }

            handler();
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private record ProviderState(string? State, string? Action);
}
