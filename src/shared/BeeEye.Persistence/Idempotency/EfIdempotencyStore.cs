using BeeEye.Persistence.Entities;
using BeeEye.Shared.Idempotency;
using BeeEye.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BeeEye.Persistence.Idempotency;

/// <summary>
/// EF Core implementation of <see cref="IIdempotencyStore"/> over the operational database.
/// <para>
/// Scoped, and deliberately sharing the request's <see cref="BeeEyeDbContext"/>: the transaction it
/// opens is the one the handler's own <c>SaveChangesAsync</c> enlists in, which is what makes "the key
/// and the effect commit together" true rather than aspirational.
/// </para>
/// </summary>
public sealed class EfIdempotencyStore(BeeEyeDbContext db, IClock clock) : IIdempotencyStore
{
    /// <summary>
    /// How long a key is remembered (ADR 0007 §2.1). Must exceed any client's retry horizon —
    /// TanStack Query's exponential backoff and the Service Bus delivery count both sit far inside it.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(48);

    private IDbContextTransaction? _transaction;

    public async Task<IdempotencyEntry?> FindAsync(string key, CancellationToken cancellationToken)
    {
        var record = await db.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Key == key, cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (record.ExpiresAtUtc <= clock.UtcNow)
        {
            // Past its window the key is unseen, per the ADR. Removed here rather than left to a
            // sweep, because leaving it would make the unique index reject the fresh submission we
            // have just decided to allow.
            db.IdempotencyRecords.Remove(new IdempotencyRecord { Id = record.Id });
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return null;
        }

        return new IdempotencyEntry(
            record.Key,
            record.Route,
            record.RequestFingerprint,
            record.ResponseStatus,
            record.ResponseBody,
            record.PrincipalId);
    }

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        // An ambient transaction already exists only if a caller nested two idempotent operations,
        // which the filter never does. Guarding keeps a future misuse from silently committing early.
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already open on this request's DbContext; idempotent operations do not nest.");
        }

        _transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task<bool> TryCompleteAsync(IdempotencyEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var now = clock.UtcNow;

        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            Key = entry.Key,
            Route = entry.Route,
            RequestFingerprint = entry.RequestFingerprint,
            ResponseStatus = entry.ResponseStatus,
            ResponseBody = entry.ResponseBody,
            PrincipalId = entry.PrincipalId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(Retention),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex))
        {
            // A concurrent request committed this key first. Rolling back discards *our* effect, so
            // the operation happens exactly once — the outcome the ADR asks for.
            await RollbackAsync(cancellationToken);
            return false;
        }

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        return true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;

        // The context still holds the entities the rolled-back attempt added; leaving them tracked
        // would have the next save retry the discarded work.
        db.ChangeTracker.Clear();
    }
}
