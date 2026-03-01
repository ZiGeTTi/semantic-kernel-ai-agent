# LocalTechAgent

Minimal console app using Semantic Kernel with OpenAI.

## Configuration

You can provide the API key via `appsettings.Development.json`, `appsettings.json`, or the `OPENAI_API_KEY` environment variable. The environment variable overrides the file.

### appsettings.json

Update `appsettings.json` (or `appsettings.Development.json` for local secrets):

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_API_KEY_HERE",
    "ModelId": "gpt-4o-mini"
  }
}
```

> Tip: Do not commit real API keys. Keep real keys in `appsettings.Development.json`, which is ignored by `.gitignore`.

### Environment variable

Set `OPENAI_API_KEY` in your shell or system environment.

## Notion Integration

Add a Notion integration secret via `appsettings.Development.json` or the `NOTION_API_KEY` environment variable.

```json
{
  "Notion": {
    "ApiKey": "YOUR_NOTION_SECRET",
    "BaseUrl": "https://api.notion.com/v1",
    "Version": "2022-06-28",
    "DatabaseId": "YOUR_DATABASE_ID",
    "TitlePropertyName": "Name",
    "BodyPropertyName": "Body",
    "ExtrasPropertyName": "Summary"
  }
}
```

## Run

Build/run with the .NET SDK for `net9.0`.
