# FunASR Self-Host Deployment (OpenAI-Compatible)

This project includes a local self-hosted FunASR service that exposes:

- `POST /v1/audio/transcriptions`

The endpoint is compatible with the current AI voice pages in this repo.

## 1. Start Service

From repo root:

```bash
cd deploy/funasr-openai
docker compose up -d --build
```

First startup downloads model files and may take several minutes.

## 2. Check Health

```bash
curl http://127.0.0.1:10098/healthz
```

Expected:

```json
{"ok":true,"model":"iic/SenseVoiceSmall","device":"cpu"}
```

## 3. Configure Web Page

Open any page below:

- `/ai/VoiceAPI`
- `/ai/VoiceSherpa`
- `/ai/VoiceWhisper`
- `/ai/VoiceTransformer`

Fill fields:

- API Address: `http://127.0.0.1:10098`
- Model: `whisper-1` (any value is accepted by compatibility layer)
- API Key: leave empty (unless you configure `FUNASR_API_KEY`)

Then upload/record and click transcribe.

## 4. Optional API Key

Edit `deploy/funasr-openai/docker-compose.yml`:

```yaml
environment:
  FUNASR_API_KEY: "your-secret-key"
```

Restart:

```bash
docker compose up -d --build
```

In pages, put the same value into `API Key`.

## 5. Stop Service

```bash
cd deploy/funasr-openai
docker compose down
```

## 6. Change Model

Edit `FUNASR_MODEL` in `docker-compose.yml`, for example:

- `iic/SenseVoiceSmall`
- `paraformer-zh`

Then restart:

```bash
docker compose up -d --build
```
