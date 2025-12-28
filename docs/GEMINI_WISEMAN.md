# Gemini AI Integration (Wise Man NPC)

The Acorn server includes an integration with Google's Gemini AI to power an interactive "Wise Man" NPC that can respond to player questions with AI-generated responses.

## Setup

### 1. Configure User Secrets

The Gemini API key should be stored securely using .NET User Secrets. Run the following command from the `src/Acorn` directory:

```bash
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY"
```

You can get an API key from [Google AI Studio](https://makersuite.google.com/app/apikey).

### 2. Configuration Options

The following settings can be configured in `appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "",
    "Model": "gemini-2.0-flash",
    "MaxResponseLength": 200,
    "Enabled": true
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `ApiKey` | Your Gemini API key (use user-secrets in production) | `""` |
| `Model` | The Gemini model to use | `gemini-2.0-flash` |
| `MaxResponseLength` | Maximum characters for responses | `200` |
| `Enabled` | Enable/disable the feature | `true` |

## Usage

Players can interact with the Wise Man by saying any of the following phrases in local chat:

- `Hey Wise Man, {question}`
- `Hi Wise Man, {question}`
- `Hello Wise Man, {question}`
- `Wise Man, {question}`
- `Dear Wise Man, {question}`
- `Oh Wise Man, {question}`

### Examples

```
Player: Hey Wise Man, where can I find gold?
[Wise Man]: Ah, young adventurer... seek the ancient mines beyond the eastern ridge...

Player: Wise Man, how do I become stronger?
[Wise Man]: Strength comes not from the blade alone, but from wisdom gained through trials...
```

## Technical Details

- Requests are queued to avoid rate limiting (500ms delay between requests)
- Queue capacity is 100 requests (oldest requests dropped if full)
- Responses are broadcast to all players on the same map
- The NPC speaks "in character" as an ancient, mystical sage
- The system prompt ensures the AI never breaks character

## Troubleshooting

If the Wise Man doesn't respond:

1. Check that `Gemini:ApiKey` is set correctly
2. Ensure `Gemini:Enabled` is `true`
3. Check the server logs for any API errors
4. Verify your API key has quota remaining

