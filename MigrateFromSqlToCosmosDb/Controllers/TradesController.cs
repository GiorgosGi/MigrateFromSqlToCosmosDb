using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Application.Trades;
using MigrateFromSqlToCosmosDb.Contracts.Trades;

namespace MigrateFromSqlToCosmosDb.Controllers;

[ApiController]
[Authorize(Policy = "TradesRead")]
[Route("api/v1/trades")]
public sealed partial class TradesController(TradeQueryService service) : ControllerBase
{
    private const int MaxPageSize = 100;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,32}$")]
    private static partial Regex InstrumentRegex();

    [HttpGet]
    [ProducesResponseType<PagedResponse<TradeResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TradeResponse>>> Get(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? instrument,
        [FromQuery] string? continuationToken,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > MaxPageSize)
        {
            return BadRequest($"pageSize must be between 1 and {MaxPageSize}.");
        }

        if (from > to)
        {
            return BadRequest("from must be earlier than or equal to to.");
        }

        if (!string.IsNullOrWhiteSpace(instrument))
        {
            instrument = instrument.Trim();
            if (!InstrumentRegex().IsMatch(instrument))
            {
                return BadRequest("instrument must match ^[A-Za-z0-9._-]{1,32}$.");
            }
        }

        var accountId = User.FindFirstValue("account_id");
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Forbid();
        }

        try
        {
            return await service.GetPageAsync(
                new TradeQuery(accountId, from, to, instrument, pageSize, continuationToken),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
