import os
import tempfile
import unittest
import wave

import whisper


def _create_silence_wav(path: str, duration_seconds: float = 1.0, sample_rate: int = 16000) -> None:
    frame_count = int(duration_seconds * sample_rate)
    silence_frame = (0).to_bytes(2, byteorder="little", signed=True)

    with wave.open(path, "wb") as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(silence_frame * frame_count)


@unittest.skipUnless(
    os.getenv("RUN_WHISPER_INTEGRATION_TEST") == "1",
    "Set RUN_WHISPER_INTEGRATION_TEST=1 to run Whisper integration tests.",
)
class WhisperLocalIntegrationTests(unittest.TestCase):
    def test_transcribe_generated_silence_audio(self) -> None:
        with tempfile.TemporaryDirectory(prefix="whisper-local-test-") as tmp_dir:
            wav_path = os.path.join(tmp_dir, "silence.wav")
            _create_silence_wav(wav_path)

            model_name = os.getenv("WHISPER_MODEL", "base")
            model = whisper.load_model(model_name)
            result = model.transcribe(wav_path, fp16=False, verbose=False)

            self.assertIsInstance(result, dict)
            self.assertIn("text", result)
            self.assertIsInstance(result.get("text") or "", str)


if __name__ == "__main__":
    unittest.main()
