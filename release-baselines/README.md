# Game release baseline

`game-files-7.3.23.json` is the immutable SHA-256 and byte-size inventory copied from the verified 7.3.23 channel package. `approvals-7.3.23.json` binds that exact file by SHA-256 and pins the release base to launcher game version `7.3.23` / AllGen `0.9.9`.

The ledger's `approvedChanges` array is empty initially. Each later approved change is one row with:

- `path`: exact game-relative runtime path;
- `beforeSha256`: baseline/current approved hash, or `null` for a newly added path;
- `afterSha256`: approved resulting hash, or `null` for an approved removal;
- `size`: resulting byte size when `afterSha256` is present;
- `reason`: specific review rationale.

Rows are path-unique and must match their exact before-hash. A changed runtime path therefore needs both old and new hashes; an addition/removal is explicit. No directory-wide, `Data`, `Scripts`, or `Mods` allowances exist. Missing files, malformed/stale ledger data, extra files, and any unlisted hash change stop packaging, local parity, and archive-only upload verification.

`tests/test_package.py` has synthetic-project helpers that write their own fixture baseline and empty ledger before packaging. Regression cases use the checked-in 7.3.23 baseline to reject an AllGen 0.9.5 content rollback, removal of `patch/Data/items.dat`, an unexplained new resource, a missing ledger, and an approval with the wrong after-hash.
