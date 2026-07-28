# Simple Discount Verifier

## Customer profile import

The repository includes a separate console utility for direct bulk import into
Azure Table Storage:

```text
tools/CustomerProfileImporter
```

The importer writes customer profile rows to the `CustomerProfiles` table. It
expects JSON with the `ancets` array:

The approved profile contract now also requires top-level
`physicalCardNumber`. Importer support for mapping and validating that field is
tracked as `PCN-02` in
`docs/architecture/physical-card-number-implementation-plan.md` and is not yet
implemented. Do not use the current importer for the post-change reload until
that task is completed.

```json
{
  "ancets": [
    {
      "tel": "+38 (050) 123-45-67",
      "fio": "Ivan Petrenko",
      "dish": "Pizza Margherita"
    }
  ]
}
```

Source-to-questionnaire mapping is configured in a separate JSON file:

```json
{
  "fields": {
    "tel": "phone",
    "fio": "fullName",
    "dish": "favoriteDish",
    "birthday": "birthDate"
  },
  "dateFormats": {
    "birthDate": [
      "yyyy-MM-dd",
      "dd.MM.yyyy H:mm:ss"
    ]
  }
}
```

Mapping rules:

| Source field | Meaning |
| --- | --- |
| Any string key mapped to `phone` | Customer phone. The importer normalizes Ukrainian phones and skips rows that cannot be normalized. |
| Any string key mapped to `fullName` | Full name. Required by the current questionnaire. |
| Any string key mapped to `birthDate` | Birth date. Optional, must use `YYYY-MM-DD` when present. |
| Any string key mapped to `favoriteDish` | Favorite dish. Optional. |

The mapping must contain exactly one source key mapped to `phone`. Unmapped
source fields are ignored. Optional questionnaire fields missing from the
mapping are stored as empty optional answers.

When source dates are not already in `YYYY-MM-DD`, configure `dateFormats` for
the target questionnaire code. Matching dates are normalized to `YYYY-MM-DD`
before profile validation.

Build:

```powershell
dotnet build .\tools\CustomerProfileImporter\CustomerProfileImporter.csproj
```

Validate input without writing to storage:

```powershell
dotnet run --project .\tools\CustomerProfileImporter\CustomerProfileImporter.csproj -- --input .\import\ancets.json --mapping .\import\ancets.mapping.json --dry-run --quiet
```

Run the included sample:

```powershell
dotnet run --project .\tools\CustomerProfileImporter\CustomerProfileImporter.csproj -- --input .\tools\CustomerProfileImporter\sample.ancets.json --mapping .\tools\CustomerProfileImporter\sample.mapping.json --dry-run
```

Validate the current POS export shape without writing to Azure:

```powershell
dotnet run --project .\tools\CustomerProfileImporter\CustomerProfileImporter.csproj -- --input "C:\Users\Pasha\Downloads\vopac.json" --mapping .\tools\CustomerProfileImporter\vopac.mapping.json --dry-run --quiet
```

The POS mapping file maps:

| POS source field | Target |
| --- | --- |
| `_1` | `fullName` |
| `_2` | `phone` |
| `_3` | `favoriteDish` |
| `_4` | `birthDate` |

Import new rows into Azure Table Storage:

```powershell
dotnet run --project .\tools\CustomerProfileImporter\CustomerProfileImporter.csproj -- --input .\import\ancets.json --mapping .\import\ancets.mapping.json --connection-string "<storage account connection string>" --table-name CustomerProfiles --mode insert
```

Non-dry-run imports write valid rows to Azure Table Storage in batches of up to
100 rows. Azure Table batch transactions require all rows in the batch to share
one `PartitionKey`; customer profiles use the constant `phone` partition, so
the importer can batch them efficiently. If a batch fails, for example because
`insert` hits an existing phone, the importer falls back to row-by-row writes
for that batch so duplicates can be reported and skipped.

For real imports, do not wrap the command in a short external timeout. The
importer itself does not impose a total runtime limit, and Azure Table Storage
does not require the whole file to finish within a fixed window. Large files,
network latency, and `insert` fallback for duplicate phones can make a valid
import run longer than 15 minutes. Let the process finish and rely on the final
summary to confirm how many rows were inserted, updated, skipped, or detected
as duplicates.

Import modes:

| Mode | Behavior |
| --- | --- |
| `insert` | Add new rows. Existing normalized phones are reported as duplicate/skipped. |
| `skip-existing` | Same write behavior as `insert`, with duplicate rows intentionally skipped. |
| `upsert` | Insert new rows or replace existing rows by normalized phone. |

The utility can also read the connection string from the
`AppStorageConnectionString` environment variable when `--connection-string` is
omitted.

## POS HMAC COM component

The `tools/PosHmacCom` project provides the registered COM component used by
the 1C POS module to calculate UTF-8 HMAC-SHA256 signatures in Base64 without
activating .NET Framework system classes through COM.

Build:

```powershell
dotnet build `
  .\tools\PosHmacCom\SimpleDiscountVerifier.PosHmacCom.csproj `
  --configuration Release
```

Registration, verification, unregistration, and 1C usage instructions are in
[`tools/PosHmacCom/README.md`](tools/PosHmacCom/README.md).
