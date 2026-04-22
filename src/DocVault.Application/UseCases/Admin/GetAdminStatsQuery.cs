using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin;

public sealed record GetAdminStatsQuery : IQuery<Result<AdminStatsDto>>;
