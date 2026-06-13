using Microsoft.AspNetCore.Mvc;

namespace DocVault.Api.Contracts.Admin;

public sealed record DeadLetterPageRequest(
    [FromQuery] int Page = 1,
    [FromQuery] int Size = 20);
