namespace Api.Common.Extensions;

using Domain.Common;
using Microsoft.AspNetCore.Mvc;

public static class ResultExtensions
{
    public static IResult ToProblemDetails<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert success result to ProblemDetails.");
        }

        var error = result.Error!;
        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Status = GetStatusCode(error.Code)
        };

        return Results.Problem(problemDetails);
    }

    private static int GetStatusCode(string errorCode) => errorCode switch
    {
        string code when code.Contains("NotFound") => 404,
        string code when code.Contains("Validation") => 400,
        string code when code.Contains("Conflict") => 409,
        string code when code.Contains("Unauthorized") || code.Contains("InvalidCredentials") => 401,
        string code when code.Contains("Forbidden") => 403,
        _ => 500
    };
}
