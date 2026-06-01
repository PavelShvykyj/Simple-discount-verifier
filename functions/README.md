# Scanner Survey Azure Functions

This API stores temporary scanner compatibility survey results for restaurant branches and terminals.

## Endpoint

```http
POST /api/scanner-survey
Content-Type: application/json
```

Example payload:

```json
{
  "branchName": "Pizza Center Obolon",
  "submittedAtClient": "2026-06-01T14:30:00.000Z",
  "terminals": [
    {
      "terminalName": "POS-01",
      "answers": [
        {
          "barcodeId": "code128-web-prefix-short",
          "isReadable": true,
          "comment": ""
        },
        {
          "barcodeId": "ean13-valid-discount-card",
          "isReadable": false,
          "comment": "Scanner beeps, POS ignores the value"
        }
      ]
    }
  ],
  "comment": "Branch-wide note"
}
```

The function intentionally does not validate business values. If the JSON body can be read, it stores the submitted values. Invalid JSON returns `400 Bad Request`.

## Table Storage Shape

Table name defaults to `ScannerSurveyResults`.

Each barcode answer is stored as a separate Azure Table Storage row so later queries can answer:

- which branch was tested;
- which terminal/workplace was tested;
- which barcode sample did not read;
- which scanner/POS combinations have comments.

Rows are partitioned by a technical `BranchKey` derived from `branchName`. The original `branchName` is stored unchanged in the `BranchName` property.

Important properties:

- `RecordType`
- `SubmissionId`
- `BranchName`
- `TerminalName`
- `BarcodeId`
- `IsReadable`
- `AnswerComment`
- `SubmissionComment`
- `SubmittedAtUtc`
- `SubmittedAtClient`
- `UserAgent`

## Azure Portal Setup

1. Open the Azure resource group that contains the Static Web App.
2. Create or choose a Storage Account for scanner survey data.
3. In the Storage Account, open **Data storage -> Tables** and create a table:

   ```text
   ScannerSurveyResults
   ```

4. In the Storage Account, open **Security + networking -> Access keys** and copy a connection string.
5. Open the Static Web App.
6. Open **Settings -> Environment variables**.
7. Add these application settings:

   ```text
   AppStorageConnectionString=<storage account connection string>
   ScannerSurveyTableName=ScannerSurveyResults
   ```

8. Save the settings and redeploy the Static Web App if needed.
9. After deployment, test:

   ```http
   POST https://<your-static-web-app-domain>/api/scanner-survey
   ```

## Local Settings

Copy `local.settings.json.example` to `local.settings.json` for local development. Do not commit `local.settings.json`.
