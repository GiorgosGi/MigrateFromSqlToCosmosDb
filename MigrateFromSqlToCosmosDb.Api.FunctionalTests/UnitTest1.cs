using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Application.Trades;
using MigrateFromSqlToCosmosDb.Controllers;
using MigrateFromSqlToCosmosDb.Domain.Trading;

namespace MigrateFromSqlToCosmosDb.Api.FunctionalTests;

public sealed class TradesControllerTests
{
    [Fact]
    public async Task Get_ReturnsBadRequest_WhenPageSizeIsOutOfRange()
    {
        var controller = CreateController(new FakeReadStore());

        var result = await controller.Get(from: null, to: null, instrument: null, continuationToken: null, pageSize: 0);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsBadRequest_WhenInstrumentIsInvalid()
    {
        var controller = CreateController(new FakeReadStore());

        var result = await controller.Get(from: null, to: null, instrument: "invalid symbol", continuationToken: null, pageSize: 25);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsForbid_WhenAccountClaimMissing()
    {
        var controller = CreateController(new FakeReadStore(), []);

        var result = await controller.Get(from: null, to: null, instrument: null, continuationToken: null, pageSize: 25);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsBadRequest_WhenContinuationTokenIsInvalid()
    {
        var controller = CreateController(new FakeReadStore { ThrowInvalidToken = true });

        var result = await controller.Get(from: null, to: null, instrument: null, continuationToken: "bad-token", pageSize: 25);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static TradesController CreateController(ITradeReadStore readStore, IEnumerable<Claim>? claims = null)
    {
        var service = new TradeQueryService(readStore);
        var controller = new TradesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims ?? [new Claim("account_id", "acc-1")], "test"))
                }
            }
        };

        return controller;
    }

    private sealed class FakeReadStore : ITradeReadStore
    {
        public bool ThrowInvalidToken { get; init; }

        public Task<TradePage> GetPageAsync(TradeQuery query, CancellationToken cancellationToken)
        {
            if (ThrowInvalidToken)
            {
                throw new InvalidOperationException("Invalid continuation token payload.");
            }

            return Task.FromResult(new TradePage(
                [new Trade(1, "acc-1", "AAPL", 1m, 100m, DateTimeOffset.UtcNow, "Done")],
                null));
        }
    }
}
