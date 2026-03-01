# TempDB Configuration Checks

Validates TempDB configuration against Microsoft best practices for optimal performance.

## Scripts

Run `TempDBChecks.sql` using **GO batch separation** (the script contains variable declarations that require batch separation).

## What It Checks

1. **TempDB File Count** — Number of TempDB data files should match CPU count (up to 8). Too few files cause allocation contention (PFS/GAM/SGAM page waits). Too many (>8) adds unnecessary overhead.
2. **TempDB File Size Equality** — All TempDB data files must be the same size. Unequal sizes cause proportional fill to favor larger files, defeating the purpose of multiple files.
3. **TempDB Autogrow Settings** — Percentage-based autogrow is not recommended for TempDB. Growth increments should be between 64MB and 1GB. Too-small increments cause frequent growths, too-large increments cause allocation delays.
4. **TempDB File Location** — TempDB should not be on the C: (system) drive. It should be on fast dedicated storage.

## Interpretation

- Results are returned as multiple result sets, one per check area.
- Each result includes a `Status` column with `OK` or `Warning: ...`.
- **File count issues** are Medium severity — they cause contention under high concurrency.
- **File size inequality** is Medium severity — fix by using `ALTER DATABASE tempdb MODIFY FILE` to equalize sizes.
- **Percentage autogrow** is Low severity but should be corrected to fixed-size growth.
- **System drive placement** is Medium severity — significant I/O impact on busy systems.
- TempDB changes typically require a **SQL Server restart** to take full effect.

## Source

Microsoft Tiger Toolbox - BPCheck (TempDB section)
