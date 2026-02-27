using DocVault.Domain.Imports;

namespace DocVault.Application.Abstractions.Persistence;

public interface IImportJobRepository
{
  Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);
  Task AddAsync(ImportJob job, CancellationToken cancellationToken = default);
  Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default);
}
