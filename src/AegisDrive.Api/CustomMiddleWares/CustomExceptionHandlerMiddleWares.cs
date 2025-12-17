using AegisDrive.Api.Contracts.Common; // Ensure this matches where you put ErrorToReturn
using AegisDrive.Api.Shared.Exceptions; // Ensure this matches your exceptions namespace
using System.Text.Json;

namespace AegisDrive.Api.CustomMiddleWares;

public class CustomExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomExceptionHandlerMiddleware> _logger;

    public CustomExceptionHandlerMiddleware(RequestDelegate next, ILogger<CustomExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            // Handle 404 for routes that don't match any endpoint
            await HandleNotFoundEndpointAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // 1. Determine Status Code based on Exception Type
        var statusCode = ex switch
        {
            BadRequestException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            FluentValidation.ValidationException => StatusCodes.Status400BadRequest, // Support FluentValidation
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        // 2. Create Response Object
        var response = new ErrorToReturn
        {
            StatusCode = statusCode,
            ErrorMessage = statusCode == 500 ? "Internal Server Error" : ex.Message
        };

        // 3. Handle specific data (like Validation Errors)
        if (ex is BadRequestException badRequestEx)
        {
            response.Errors = badRequestEx.Errors;
        }

        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task HandleNotFoundEndpointAsync(HttpContext context)
    {
        // Only handle 404 if the response hasn't started writing yet (empty body)
        if (context.Response.StatusCode == StatusCodes.Status404NotFound && !context.Response.HasStarted)
        {
            var response = new ErrorToReturn
            {
                StatusCode = StatusCodes.Status404NotFound,
                ErrorMessage = $"Endpoint {context.Request.Path} was not found"
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}