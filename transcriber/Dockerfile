FROM python:3.11-slim AS base

ARG WHISPER_MODEL=base

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    WHISPER_MODEL=${WHISPER_MODEL}

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

FROM base AS deps

COPY requirements.txt /app/requirements.txt
RUN pip install -r /app/requirements.txt
RUN python - <<'PY'
import os
import whisper

model_name = os.environ.get("WHISPER_MODEL", "base")
whisper.load_model(model_name)
print(f"Preloaded Whisper model: {model_name}")
PY

FROM base AS runtime

COPY --from=deps /usr/local /usr/local
COPY --from=deps /root/.cache/whisper /root/.cache/whisper

COPY main.py /app/main.py

EXPOSE 8000

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
