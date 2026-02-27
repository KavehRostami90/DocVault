using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public class EfImportJobRepository : IImportJobRepository
{
  private readonly DocVaultDbContext _db;

  public EfImportJobRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  public async Task AddAsync(ImportJob job, CancellationToken cancellationToken = default)
  {
    await _db.ImportJobs.AddAsync(job, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
  }

  public Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    => _db.ImportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

  public async Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default)
  {
    _db.ImportJobs.Update(job);
    await _db.SaveChangesAsync(cancellationToken);
  }
}
