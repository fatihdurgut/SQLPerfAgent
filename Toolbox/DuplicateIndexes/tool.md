# Duplicate & Redundant Index Detection

Identifies duplicate and redundant indexes that waste storage and slow down write operations.

## Scripts

Run `DuplicateIndexes.sql` as a **single execution** (no GO batch separation needed).

## What It Checks

- **Exact Duplicates** — Indexes with identical key columns AND identical included columns. One can always be safely dropped.
- **Duplicate Keys** — Indexes with the same key columns but different included columns. Usually one can be consolidated.
- **Redundant Indexes** — One index is a prefix of another (e.g., Index1 on `(A, B)` is redundant when Index2 on `(A, B, C)` exists). The shorter index can typically be dropped.

## Interpretation

- Results include: `TableName`, `Index1`, `Index2`, `KeyColumns`, `DuplicateType`, `Recommendation`, and `DropScript`.
- The `Recommendation` column suggests which index to keep vs. drop based on:
  - Primary keys are always kept
  - Unique constraints are preserved
  - When ambiguous, the older index (lower `index_id`) is kept
- **Before dropping any index**, verify:
  1. No query hints (`WITH (INDEX = ...)`) reference the index by name
  2. No plan guides force the index
  3. The covering index actually satisfies all queries the dropped index served
- Exact duplicates are **Medium severity** — safe to drop, wastes storage and INSERT/UPDATE/DELETE overhead.
- Redundant indexes are **Low severity** — require more careful analysis before removal.
- Generate drop scripts with `ONLINE = ON` when possible for production environments.
- This is an advanced analysis tool — recommend running during comprehensive performance reviews.

## Source

Microsoft Tiger Toolbox - Index-Information diagnostic scripts
