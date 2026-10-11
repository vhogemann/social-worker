# MENTAL_MODEL.md — transcriber (Audio Extraction & Whisper Service)

## Purpose

This document details the architecture, audio ingestion pipeline, rate-limiting safeguards, and verification procedures for the Python `transcriber` microservice.

## Tech Stack

- **Runtime**: Python 3.11 with FastAPI and Uvicorn.
- **Speech-to-Text**: `openai-whisper` (PyTorch) with GPU acceleration or CPU fallback.
- **Media Download**: `yt-dlp` for video extraction and `requests` for direct CDN streams.
- **Captions & Subtitles**: `youtube-transcript-api` with paced request scheduling.
- **Audio Processing**: `ffmpeg` (system package inside Docker image).

## Service Architecture & Pipelines

```
transcriber/
├── Dockerfile                         # Python 3.11 slim image + ffmpeg + Whisper cache
├── main.py                            # FastAPI application and audio processing logic
├── requirements.txt                   # Pinned Python dependencies
└── tests/                             # Integration tests
```

### 1. Ingestion Pipeline (`POST /extract-transcript`)

When the .NET API enqueues a video or audio source (e.g. YouTube video, Instagram Reel, direct podcast URL), the transcriber executes the following sequence:

1. **Direct Media Stream Check**:
   - Inspects URL for direct media extensions (`.mp4`, `.m4a`, `.mp3`, `.wav`, `.webm`) or social CDN hostnames (`cdninstagram.com`, `fbcdn.net`).
   - If detected, downloads the media stream directly via chunked HTTP streaming into a temporary directory, bypassing yt-dlp overhead.
2. **YouTube Caption Fast Path**:
   - If the URL is a YouTube video, attempts rapid caption retrieval via `youtube-transcript-api`.
   - Protects against YouTube rate limits through `_youtube_pacing_lock`, configurable inter-request delay (`YOUTUBE_REQUEST_DELAY_SECONDS`), and exponential backoff retries.
3. **yt-dlp Audio Fallback**:
   - If captions are unavailable or the URL is a standard web video, invokes `yt-dlp` extracting the highest quality audio stream (`bestaudio/best`) converted to audio.
4. **Whisper Transcription Engine**:
   - Loads the specified Whisper model (default `base`, cached in `WHISPER_CACHE_DIR`).
   - Synchronizes model inference across threads via `_model_lock`.
   - Generates structured transcription segments with timestamps, text, and detected language.
5. **Transcript Persistence**:
   - Saves results as a JSON transcript document to the mounted `/transcripts` volume (`TRANSCRIPTS_DIR`).

### 2. Text Summarization Pipeline (`POST /summarize`)

An optional text summarization endpoint allows condensing long transcripts:
- Controlled by `SUMMARY_ENGINE` (`disabled`, `openai`, or `local`).
- Forwards prompts to the configured `SUMMARY_URL` using `SUMMARY_MODEL` and `SUMMARY_API_KEY`.

## API Contracts

### `GET /health` / `POST /health`
Returns service availability and loaded Whisper model name:
```json
{
  "status": "ok",
  "whisperModel": "base"
}
```

### `POST /extract-transcript`
Request:
```json
{
  "videoUrl": "https://www.youtube.com/watch?v=...",
  "outputPath": "transcripts/my-video.json",
  "language": "en"
}
```
Response:
```json
{
  "status": "completed",
  "transcriptPath": "/transcripts/my-video.json",
  "duration": 184.2,
  "language": "en",
  "error": null
}
```

## Environment Configuration

| Variable | Default | Purpose |
| :--- | :--- | :--- |
| `TRANSCRIPTS_DIR` | `/transcripts` | Directory where generated transcripts are persisted |
| `WHISPER_MODEL` | `base` | Whisper model name (`tiny`, `base`, `small`, `medium`, `large`) |
| `SUMMARY_ENGINE` | `disabled` | Summarization engine mode (`disabled`, `openai`, `local`) |
| `SUMMARY_URL` | empty | API endpoint for summarization LLM |
| `YOUTUBE_REQUEST_DELAY_SECONDS` | `2.0` | Minimum delay between YouTube transcript requests |
| `YOUTUBE_TRANSCRIPT_RETRIES` | `3` | Max retry attempts for YouTube caption fetching |

## Verification & Testing

The service runs under Docker Compose. To test the container or integration:

```bash
# Verify health check
curl http://localhost:8102/health

# Run service container tests inside Docker
docker compose exec transcriber pytest tests/
```
