# Best Practices Checks

Runs comprehensive SQL Server best practices analysis based on Microsoft Tiger Toolbox BPCheck.

## Scripts

Run `BestPracticesChecks.sql` using **GO batch separation** (the script contains multiple batches separated by GO statements).

## What It Checks

1. **Memory Pressure** — Checks physical memory availability. Less than 5% available is critical, less than 10% is warning.
2. **Database Backups** — Verifies backup status for all user databases. Flags databases with no full backup, full backups older than 7 days, or missing log backups in FULL recovery model.
3. **Database Integrity (DBCC CHECKDB)** — Checks when DBCC CHECKDB was last run. Never run or older than 7 days is flagged.
4. **MaxDOP Configuration** — Validates max degree of parallelism setting. MAXDOP=0 with many CPUs causes excessive parallelism. Recommended: min(CPU count, 8).
5. **Instant File Initialization** — Checks if IFI is enabled. When disabled, data file operations (autogrow, restore) are significantly slower.
6. **Deprecated Feature Usage** — Reports SQL Server features that are deprecated and will be removed in future versions, sorted by usage count.

## Interpretation

- Results are returned as multiple result sets, one per check area.
- Each result includes a `Status` column with values: `OK`, `Warning: ...`, or `Critical: ...`.
- The `CheckName` column identifies which best practice is being evaluated.
- Focus on `Critical` and `Warning` status items for recommendations.
- Memory pressure findings should be correlated with expensive query results from DMV analysis.
- Backup findings are security-critical and should always be flagged at high severity.

## Source

Microsoft Tiger Toolbox - BPCheck (condensed version with most critical checks)
