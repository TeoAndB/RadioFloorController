using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using RadioFloorController.Data;
using RadioFloorController.Domain;
using RadioFloorController.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IFloorControlService, FloorControlService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var groups = app.MapGroup("/groups");

groups.MapPost("/{groupId}/floor", async Task<Results<Ok<MessageResponse>, BadRequest<MessageResponse>, JsonHttpResult<MessageResponse>>> (
        string groupId, UserRequest request, IFloorControlService floorControlService, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return TypedResults.BadRequest(new MessageResponse("Invalid request: userId is required"));
        }

        var result = await floorControlService.ObtainFloorAsync(groupId, request.UserId, ct);
        return result switch
        {
            FloorObtainResult.Obtained => TypedResults.Ok(
                new MessageResponse($"Floor obtained by {request.UserId} for group {groupId}")),
            FloorObtainResult.Conflict(var holderUserId) => TypedResults.Json(
                new MessageResponse($"Floor is currently held by {holderUserId} for group {groupId}"),
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unhandled {nameof(FloorObtainResult)} subtype: {result.GetType()}"),
        };
    })
    .WithName("ObtainFloor");

groups.MapDelete("/{groupId}/floor/{userId}", async Task<Results<Ok<MessageResponse>, JsonHttpResult<MessageResponse>>> (
        string groupId, string userId, IFloorControlService floorControlService, CancellationToken ct) =>
    {
        var result = await floorControlService.ReleaseFloorAsync(groupId, userId, ct);
        return result switch
        {
            FloorReleaseResult.Released => TypedResults.Ok(
                new MessageResponse($"Floor released by {userId} for group {groupId}")),
            FloorReleaseResult.NotHolder => TypedResults.Json(
                new MessageResponse($"User {userId} does not hold the floor for group {groupId}"),
                statusCode: StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException($"Unhandled {nameof(FloorReleaseResult)} subtype: {result.GetType()}"),
        };
    })
    .WithName("ReleaseFloor");

app.Run();

record MessageResponse(string Message);

record UserRequest(string? UserId);
