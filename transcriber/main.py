import json
import os
import re
import threading
import tempfile
import time
from pathlib import Path
from typing import Optional

import requests
import torch
import whisper
import yt_dlp
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from youtube_transcript_api import YouTubeTranscriptApi

app = FastAPI(title="social-worker-transcriber")

TRANSCRIPTS_DIR = Path(os.getenv("TRANSCRIPTS_DIR", "/transcripts"))
WHISPER_MODEL_NAME = os.getenv("WHISPER_MODEL", "base")
SUMMARY_ENGINE = os.getenv("SUMMARY_ENGINE", "disabled").strip().lower()
SUMMARY_URL = os.getenv("SUMMARY_URL", "").strip()
SUMMARY_MODEL = os.getenv("SUMMARY_MODEL", "").strip()
SUMMARY_API_KEY = os.getenv("SUMMARY_API_KEY", "").strip()
SUMMARY_MAX_CHARS = int(os.getenv("SUMMARY_MAX_CHARS", "12000"))
YOUTUBE_TRANSCRIPT_RETRIES = max(1, int(os.getenv("YOUTUBE_TRANSCRIPT_RETRIES", "3")))
YOUTUBE_TRANSCRIPT_RETRY_BACKOFF_SECONDS = float(os.getenv("YOUTUBE_TRANSCRIPT_RETRY_BACKOFF_SECONDS", "1.0"))
YOUTUBE_REQUEST_DELAY_SECONDS = float(os.getenv("YOUTUBE_REQUEST_DELAY_SECONDS", "2.0"))
_last_youtube_request_time = 0.0
_youtube_pacing_lock = threading.Lock()

TRANSCRIPTS_DIR.mkdir(parents=True, exist_ok=True)

_model = None
_model_lock = threading.Lock()


class HealthResponse(BaseModel):
    status: str
    whisperModel: str


class ExtractTranscriptRequest(BaseModel):
    videoUrl: str = Field(min_length=1)
    outputPath: str = Field(min_length=1)
    language: Optional[str] = None


class ExtractTranscriptResponse(BaseModel):
    status: str
    transcriptPath: Optional[str] = None
    duration: Optional[float] = None
    language: Optional[str] = None
    error: Optional[str] = None


class SummarizeRequest(BaseModel):
    text: str = Field(min_length=1)
    maxLength: int = Field(default=500, ge=50, le=4000)


class SummarizeResponse(BaseModel):
    status: str
    summary: Optional[str] = None
    error: Optional[str] = None


@app.get("/health", response_model=HealthResponse)
@app.post("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="ok", whisperModel=WHISPER_MODEL_NAME)


def _is_youtube_url(url: str) -> bool:
    lower = url.lower()
    return "youtube.com" in lower or "youtu.be" in lower


def _pace_youtube_request() -> None:
    global _last_youtube_request_time
    if YOUTUBE_REQUEST_DELAY_SECONDS <= 0:
        return
    with _youtube_pacing_lock:
        now = time.time()
        elapsed = now - _last_youtube_request_time
        if elapsed < YOUTUBE_REQUEST_DELAY_SECONDS:
            time.sleep(YOUTUBE_REQUEST_DELAY_SECONDS - elapsed)
        _last_youtube_request_time = time.time()


def _fetch_youtube_oembed_metadata(video_url: str) -> Optional[dict]:
    try:
        resp = requests.get(
            f"https://www.youtube.com/oembed?url={video_url}&format=json",
            timeout=10,
            headers={"User-Agent": "Mozilla/5.0"}
        )
        if resp.status_code == 200:
            return resp.json()
    except Exception:
        pass
    return None


def _extract_video_metadata_fallback(video_url: str) -> Optional[str]:
    metadata_parts = []

    # 1. Try oEmbed
    oembed = _fetch_youtube_oembed_metadata(video_url)
    if oembed:
        title = oembed.get("title")
        author = oembed.get("author_name")
        if title:
            metadata_parts.append(f"Title: {title}")
        if author:
            metadata_parts.append(f"Channel: {author}")

    # 2. Try yt-dlp metadata-only extraction (no video/audio download)
    try:
        base_options = {
            "skip_download": True,
            "quiet": True,
            "no_warnings": True,
            "noplaylist": True,
            "extract_flat": "in_playlist",
        }
        with yt_dlp.YoutubeDL(base_options) as ydl:
            info = ydl.extract_info(video_url, download=False)
            if info:
                title = info.get("title")
                desc = info.get("description")
                uploader = info.get("uploader") or info.get("channel")
                tags = info.get("tags")
                
                if title and not any(p.startswith("Title:") for p in metadata_parts):
                    metadata_parts.append(f"Title: {title}")
                if uploader and not any(p.startswith("Channel:") for p in metadata_parts):
                    metadata_parts.append(f"Channel: {uploader}")
                if desc:
                    metadata_parts.append(f"Description:\n{desc}")
                if tags:
                    tag_str = ", ".join(tags[:10])
                    metadata_parts.append(f"Tags: {tag_str}")
    except Exception:
        pass

    if metadata_parts:
        return "[Source Video Metadata - Full Audio Transcript Unavailable]\n\n" + "\n\n".join(metadata_parts)
    return None


@app.post("/extract-transcript", response_model=ExtractTranscriptResponse)
def extract_transcript(request: ExtractTranscriptRequest) -> ExtractTranscriptResponse:
    output_path = _resolve_output_path(request.outputPath)
    is_yt = _is_youtube_url(request.videoUrl)
    if is_yt:
        _pace_youtube_request()

    try:
        with tempfile.TemporaryDirectory(prefix="sw-transcriber-") as tmp_dir:
            tmp_path = Path(tmp_dir)
            subtitle_result = None
            source_errors: list[str] = []

            youtube_transcript, youtube_error = _fetch_youtube_transcript(request.videoUrl)
            if youtube_transcript is not None:
                transcript, detected_language = youtube_transcript
                duration = None
                payload = {
                    "videoUrl": request.videoUrl,
                    "language": detected_language,
                    "duration": duration,
                    "transcript": transcript,
                    "segments": [],
                }
                output_path.parent.mkdir(parents=True, exist_ok=True)
                output_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding="utf-8")

                return ExtractTranscriptResponse(
                    status="success",
                    transcriptPath=str(output_path.relative_to(TRANSCRIPTS_DIR)),
                    duration=duration,
                    language=detected_language,
                )
            if youtube_error:
                source_errors.append(youtube_error)

            try:
                audio_path = _download_audio(request.videoUrl, tmp_path)
            except Exception as audio_error:
                source_errors.append(f"yt-dlp audio failed: {audio_error}")
                try:
                    subtitle_result = _download_subtitles(request.videoUrl, tmp_path)
                except Exception as subtitle_error:
                    source_errors.append(f"yt-dlp subtitles failed: {subtitle_error}")
                    subtitle_result = None
                if subtitle_result is None:
                    # Try metadata fallback before raising error
                    if is_yt:
                        fallback_text = _extract_video_metadata_fallback(request.videoUrl)
                        if fallback_text:
                            payload = {
                                "videoUrl": request.videoUrl,
                                "language": "en",
                                "duration": None,
                                "transcript": fallback_text,
                                "segments": [],
                            }
                            output_path.parent.mkdir(parents=True, exist_ok=True)
                            output_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding="utf-8")
                            return ExtractTranscriptResponse(
                                status="success",
                                transcriptPath=str(output_path.relative_to(TRANSCRIPTS_DIR)),
                                duration=None,
                                language="en",
                            )

                    raise RuntimeError(_join_source_errors(source_errors))

            if subtitle_result is not None:
                transcript, detected_language = subtitle_result
                duration = None
                payload = {
                    "videoUrl": request.videoUrl,
                    "language": detected_language,
                    "duration": duration,
                    "transcript": transcript,
                    "segments": [],
                }
                output_path.parent.mkdir(parents=True, exist_ok=True)
                output_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding="utf-8")

                return ExtractTranscriptResponse(
                    status="success",
                    transcriptPath=str(output_path.relative_to(TRANSCRIPTS_DIR)),
                    duration=duration,
                    language=detected_language,
                )

            # Check audio file validity before running Whisper
            if not audio_path.exists() or audio_path.stat().st_size < 10240:
                audio_size = audio_path.stat().st_size if audio_path.exists() else 0
                source_errors.append(f"Downloaded audio file is invalid ({audio_size} bytes)")
                if is_yt:
                    fallback_text = _extract_video_metadata_fallback(request.videoUrl)
                    if fallback_text:
                        payload = {
                            "videoUrl": request.videoUrl,
                            "language": "en",
                            "duration": None,
                            "transcript": fallback_text,
                            "segments": [],
                        }
                        output_path.parent.mkdir(parents=True, exist_ok=True)
                        output_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding="utf-8")
                        return ExtractTranscriptResponse(
                            status="success",
                            transcriptPath=str(output_path.relative_to(TRANSCRIPTS_DIR)),
                            duration=None,
                            language="en",
                        )
                raise RuntimeError(_join_source_errors(source_errors))

            try:
                model = _get_model()
                use_fp16 = torch.cuda.is_available()
                result = model.transcribe(str(audio_path), fp16=use_fp16, language=request.language, verbose=False)
            except Exception as whisper_error:
                source_errors.append(f"whisper failed: {whisper_error}")
                if is_yt:
                    fallback_text = _extract_video_metadata_fallback(request.videoUrl)
                    if fallback_text:
                        payload = {
                            "videoUrl": request.videoUrl,
                            "language": "en",
                            "duration": None,
                            "transcript": fallback_text,
                            "segments": [],
                        }
                        output_path.parent.mkdir(parents=True, exist_ok=True)
                        output_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding="utf-8")
                        return ExtractTranscriptResponse(
                            status="success",
                            transcriptPath=str(output_path.relative_to(TRANSCRIPTS_DIR)),
                            duration=None,
                            language="en",
                        )
                raise RuntimeError(_join_source_errors(source_errors))

            transcript = (result.get("text") or "").strip()
            detected_language = result.get("language")
            duration = None
            segments = result.get("segments") or []
            if segments:
                duration = float(segments[-1].get("end", 0.0))

            payload = {
                "videoUrl": request.videoUrl,
                "language": detected_language,
                "duration": duration,
                "transcript": transcript,
                "segments": segments,
            }
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding="utf-8")

            return ExtractTranscriptResponse(
                status="success",
                transcriptPath=str(output_path.relative_to(TRANSCRIPTS_DIR)),
                duration=duration,
                language=detected_language,
            )
    except HTTPException:
        raise
    except Exception as exc:
        return ExtractTranscriptResponse(status="failed", error=str(exc))


@app.post("/summarize", response_model=SummarizeResponse)
def summarize(request: SummarizeRequest) -> SummarizeResponse:
    text = request.text.strip()
    if not text:
        raise HTTPException(status_code=400, detail="Text is required.")

    if SUMMARY_ENGINE == "disabled":
        return SummarizeResponse(status="failed", error="Summarization is disabled.")

    try:
        clipped = text[:SUMMARY_MAX_CHARS]
        prompt = (
            f"Summarize the following source material in under {request.maxLength} characters. "
            "Preserve concrete facts and proper nouns.\n\n"
            f"{clipped}"
        )

        if SUMMARY_ENGINE == "ollama":
            summary = _summarize_with_ollama(prompt)
        elif SUMMARY_ENGINE in {"openrouter", "openai"}:
            summary = _summarize_with_openai_compatible(prompt)
        else:
            return SummarizeResponse(status="failed", error=f"Unsupported summary engine: {SUMMARY_ENGINE}")

        return SummarizeResponse(status="success", summary=summary.strip())
    except Exception as exc:
        return SummarizeResponse(status="failed", error=str(exc))


def _get_model():
    global _model
    if _model is None:
        with _model_lock:
            if _model is None:
                _model = whisper.load_model(WHISPER_MODEL_NAME)
    return _model


def _resolve_output_path(output_path: str) -> Path:
    candidate = Path(output_path)
    if not candidate.is_absolute():
        candidate = TRANSCRIPTS_DIR / candidate

    try:
        resolved = candidate.resolve()
        base = TRANSCRIPTS_DIR.resolve()
        resolved.relative_to(base)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=f"Invalid output path: {exc}") from exc

    return resolved


def _is_direct_media_url(url: str) -> bool:
    clean = url.lower().split("?")[0]
    return clean.endswith((".mp4", ".m4a", ".mp3", ".wav", ".webm", ".ogg", ".aac")) or "cdninstagram.com" in clean or "fbcdn.net" in clean


def _download_direct_media(media_url: str, tmp_dir: Path) -> Path:
    clean = media_url.lower().split("?")[0]
    ext = ".mp4"
    for candidate_ext in [".mp4", ".m4a", ".mp3", ".wav", ".webm", ".ogg", ".aac"]:
        if clean.endswith(candidate_ext):
            ext = candidate_ext
            break

    dest = tmp_dir / f"source{ext}"
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    }
    with requests.get(media_url, stream=True, headers=headers, timeout=60) as resp:
        resp.raise_for_status()
        with open(dest, "wb") as f:
            for chunk in resp.iter_content(chunk_size=65536):
                if chunk:
                    f.write(chunk)
    return dest


def _download_audio(video_url: str, tmp_dir: Path) -> Path:
    if _is_direct_media_url(video_url):
        try:
            return _download_direct_media(video_url, tmp_dir)
        except Exception:
            pass

    output_template = str(tmp_dir / "source.%(ext)s")
    base_options = {
        "format": "bestaudio/best",
        "force_ipv4": True,
        "quiet": True,
        "no_warnings": True,
        "outtmpl": output_template,
        "noplaylist": True,
        "continuedl": True,
        "retries": 10,
        "file_access_retries": 3,
    }

    option_variants = [
        {
            **base_options,
            "extractor_args": {
                "youtube": {
                    "player_client": ["android"],
                }
            },
            "format": "18/bestaudio/best",
        },
        {
            **base_options,
            "extractor_args": {
                "youtube": {
                    "player_client": ["android", "web", "ios"],
                }
            },
            "format": "18/bestaudio/best",
        },
        base_options,
    ]

    last_error = None
    downloaded = None
    for options in option_variants:
        try:
            with yt_dlp.YoutubeDL(options) as ydl:
                info = ydl.extract_info(video_url, download=True)
                downloaded = Path(ydl.prepare_filename(info))
                break
        except Exception as exc:
            last_error = exc

    if downloaded is None:
        try:
            return _download_direct_media(video_url, tmp_dir)
        except Exception:
            pass
        raise RuntimeError(str(last_error) if last_error else "Failed to download audio for transcription.")

    if downloaded.exists():
        return downloaded

    candidates = sorted(tmp_dir.glob("source.*"))
    if not candidates:
        raise RuntimeError("Failed to download audio for transcription.")

    return candidates[0]


def _download_subtitles(video_url: str, tmp_dir: Path) -> Optional[tuple[str, str]]:
    base_options = {
        "force_ipv4": True,
        "quiet": True,
        "no_warnings": True,
        "skip_download": True,
        "noplaylist": True,
        "outtmpl": str(tmp_dir / "source.%(ext)s"),
        "extractor_args": {
            "youtube": {
                "player_client": ["android"],
            }
        },
    }

    with yt_dlp.YoutubeDL(base_options) as ydl:
        info = ydl.extract_info(video_url, download=False)

    automatic_captions = info.get("automatic_captions") or {}
    subtitles = info.get("subtitles") or {}
    available = {**automatic_captions, **subtitles}
    if not available:
        return None

    preferred_languages = ["en", "en-US", "en-GB"]
    chosen_language = next((lang for lang in preferred_languages if lang in available), None)
    if chosen_language is None:
        chosen_language = sorted(available.keys())[0]

    subtitle_options = {
        **base_options,
        "writeautomaticsub": True,
        "writesubtitles": True,
        "subtitlesformat": "vtt",
        "subtitleslangs": [chosen_language],
    }

    with yt_dlp.YoutubeDL(subtitle_options) as ydl:
        ydl.download([video_url])

    subtitle_files = sorted(tmp_dir.glob("source*.vtt"))
    if not subtitle_files:
        return None

    transcript = _parse_vtt(subtitle_files[0])
    if not transcript.strip():
        return None

    return transcript, chosen_language


def _fetch_youtube_transcript(video_url: str) -> tuple[Optional[tuple[str, str]], Optional[str]]:
    video_id = _extract_video_id(video_url)
    if video_id is None:
        return None, None

    last_error = None
    for attempt in range(1, YOUTUBE_TRANSCRIPT_RETRIES + 1):
        try:
            transcript = YouTubeTranscriptApi().fetch(video_id)

            text_parts = []
            language = None
            for item in transcript:
                if language is None:
                    language = getattr(item, "language_code", None)
                text = getattr(item, "text", "")
                if text:
                    text_parts.append(text.strip())

            text = "\n".join(part for part in text_parts if part)
            if not text:
                return None, "youtube-transcript-api returned an empty transcript"

            return (text, language or "unknown"), None
        except Exception as exc:
            last_error = exc
            if attempt < YOUTUBE_TRANSCRIPT_RETRIES and _is_retryable_youtube_error(exc):
                delay_seconds = YOUTUBE_TRANSCRIPT_RETRY_BACKOFF_SECONDS * (2 ** (attempt - 1))
                time.sleep(delay_seconds)
                continue
            break

    if last_error is None:
        return None, None

    return None, f"youtube-transcript-api failed: {last_error}"


def _is_retryable_youtube_error(exc: Exception) -> bool:
    message = str(exc).lower()
    retryable_markers = [
        "connection refused",
        "max retries exceeded",
        "timed out",
        "temporarily unavailable",
        "too many requests",
        "http error 429",
        "newconnectionerror",
    ]
    return any(marker in message for marker in retryable_markers)


def _join_source_errors(errors: list[str]) -> str:
    deduped = []
    for error in errors:
        cleaned = error.strip()
        if cleaned and cleaned not in deduped:
            deduped.append(cleaned)
    if not deduped:
        return "Failed to extract transcript."
    return " | ".join(deduped)


def _extract_video_id(url: str) -> Optional[str]:
    match = re.search(r"(?:v=|youtu\.be/)([\w-]{11})", url)
    return match.group(1) if match else None


def _parse_vtt(path: Path) -> str:
    text = path.read_text(encoding="utf-8", errors="ignore")
    lines = []
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or line == "WEBVTT" or line.startswith("NOTE"):
            continue
        if "-->" in line:
            continue
        if re.fullmatch(r"\d+", line):
            continue
        cleaned = re.sub(r"<[^>]+>", "", line)
        if cleaned:
            lines.append(cleaned)

    deduped = []
    for line in lines:
        if not deduped or deduped[-1] != line:
            deduped.append(line)
    return "\n".join(deduped)


def _summarize_with_ollama(prompt: str) -> str:
    if not SUMMARY_URL or not SUMMARY_MODEL:
        raise RuntimeError("SUMMARY_URL and SUMMARY_MODEL must be set for Ollama summarization.")

    response = requests.post(
        SUMMARY_URL,
        json={"model": SUMMARY_MODEL, "prompt": prompt, "stream": False},
        timeout=120,
    )
    response.raise_for_status()
    payload = response.json()
    return payload.get("response", "")


def _summarize_with_openai_compatible(prompt: str) -> str:
    if not SUMMARY_URL or not SUMMARY_MODEL or not SUMMARY_API_KEY:
        raise RuntimeError("SUMMARY_URL, SUMMARY_MODEL, and SUMMARY_API_KEY must be set for OpenAI-compatible summarization.")

    response = requests.post(
        SUMMARY_URL.rstrip("/") + "/chat/completions",
        headers={
            "Authorization": f"Bearer {SUMMARY_API_KEY}",
            "Content-Type": "application/json",
        },
        json={
            "model": SUMMARY_MODEL,
            "messages": [
                {"role": "system", "content": "You summarize source material concisely and accurately."},
                {"role": "user", "content": prompt},
            ],
            "temperature": 0.2,
        },
        timeout=120,
    )
    response.raise_for_status()
    payload = response.json()
    return payload["choices"][0]["message"]["content"]
