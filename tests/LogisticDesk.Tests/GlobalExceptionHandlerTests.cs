using LogisticDesk.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticDesk.Tests;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public void Concurrency_conflicts_map_to_409()
    {
        var (status, _) = GlobalExceptionHandler.Map(new DbUpdateConcurrencyException());
        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public void Database_update_failures_map_to_409()
    {
        var (status, _) = GlobalExceptionHandler.Map(new DbUpdateException("duplicate key"));
        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public void Unknown_exceptions_map_to_500_without_leaking_the_message()
    {
        var (status, title) = GlobalExceptionHandler.Map(new InvalidOperationException("connection string: secret"));
        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("secret", title);
    }
}
