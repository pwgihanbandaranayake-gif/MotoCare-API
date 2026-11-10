using Microsoft.Data.SqlClient;
using MotoCare.Domain;

namespace MotoCare.Api.Infrastructure;

/// <summary>Turns business-rule and SQL errors into clear JSON responses.</summary>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (BusinessRuleException ex) { await Write(context, 400, ex.Message); }
        catch (KeyNotFoundException ex) { await Write(context, 404, ex.Message); }
        catch (SqlException ex) when (ex.Number == 50000) { await Write(context, 400, ex.Message); }
        catch (SqlException ex) when (ex.Number is 2627 or 2601) { await Write(context, 409, "A record with the same value already exists."); }
        catch (SqlException ex) when (ex.Number == 547) { await Write(context, 409, "This record is linked to other records and cannot be changed this way."); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error on {Path}", context.Request.Path);
            await Write(context, 500, "Something went wrong on the server. Check the API log for details.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string message)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { message });
    }
}
