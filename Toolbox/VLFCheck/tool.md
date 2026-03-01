# VLF (Virtual Log File) Check

Analyzes transaction log Virtual Log File counts across all databases to identify log file fragmentation issues.

## Scripts

Run `VLFCheck.sql` as a **single execution** (no GO batch separation needed).

## What It Checks

- Counts VLFs (Virtual Log Files) per database using `sp_MSforeachdb`
- Reports log file size in MB
- Classifies severity:
  - **Critical**: VLF count > 1000
  - **Warning**: VLF count > 100
  - **Monitor**: VLF count > 50
  - **OK**: VLF count <= 50
- Only databases exceeding 50 VLFs are shown in results

## Interpretation

- High VLF counts cause **transaction log performance degradation** — each log operation must traverse more VLFs.
- Common causes: frequent small log file autogrowths over time.
- **Remediation** requires a maintenance window:
  1. Back up the transaction log
  2. Shrink the log file (`DBCC SHRINKFILE`)
  3. Regrow to appropriate size with larger initial size and growth increments
- VLF count > 1000 should be flagged as **High severity** — it directly impacts write performance.
- VLF count 100-1000 is **Medium severity** — schedule remediation during next maintenance window.
- Results include `DatabaseName`, `VLFCount`, `LogSizeMB`, `Status`, and `Recommendation` columns.

## Source

Microsoft Tiger Toolbox - VLF diagnostic scripts
