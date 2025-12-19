import os
import tempfile
import io
import numpy as np
import scipy.io.wavfile as wav

from fastapi import HTTPException, UploadFile

from . import app_context
import json
import ollama
from .config import OLLAMA_MODEL, OLLAMA_HOST, TTS_LANGUAGE, TTS_SPEAKER_WAV


async def transcribe_audio_file(file: UploadFile) -> str:
    """
    Handles transcription of the user's voice
    """
    if not app_context.WHISPER_MODEL:
        print("Error: Whisper model is not initialized.")
        raise HTTPException(500, "Whisper model is not initialized.")

    temp_audio_path = None
    try:

        original_filename = file.filename or "audio.webm"
        _, file_extension = os.path.splitext(original_filename)

        file_extension = file_extension.lower()

        if not file_extension:
            file_extension = ".webm"

        # Copies all audio content into a temp file
        with tempfile.NamedTemporaryFile(
            delete=False, suffix=file_extension
        ) as temp_file:
            content = await file.read()
            temp_file.write(content)
            temp_audio_path = temp_file.name

        print(
            f"Voice command received ({file_extension}), temporary file: {temp_audio_path}"
        )

        # Transcribing using out faster-whisper model
        user_text = ""
        async with app_context.MODEL_LOCK:
            print("Transcribing with Faster-Whisper...")
            # Whisper kütüphanesi dosya uzantısına bakarak formatı otomatik tanır
            segments, info = app_context.WHISPER_MODEL.transcribe(
                temp_audio_path, beam_size=5
            )
            segment_texts = [segment.text for segment in segments]
            user_text = "".join(segment_texts).strip()

        if not user_text or len(user_text.strip()) < 2:
            raise HTTPException(
                400, f"No speech detected in audio or text is too short: '{user_text}'"
            )

        return user_text

    except Exception as e:
        print(f"Transcription error: {e}")
        import traceback

        traceback.print_exc()
        if isinstance(e, HTTPException):
            raise e
        raise HTTPException(500, f"Transcription error: {e}")
    finally:
        # After everything completed we delete the temp file
        if temp_audio_path and os.path.exists(temp_audio_path):
            try:
                os.remove(temp_audio_path)
            except Exception as e:
                print(f"Error removing temp file: {e}")


SYSTEM_PROMPT = """
You are a smart home assistant. You analyze the user's voice command and extract the intent.
Output ONLY a JSON object with the following schema:
{
  "command": "klima_ac" | "klima_kapa" | "isik_ac" | "isik_kapa" | "kahve_ac" | "kahve_kapa" | "muzik_ac" | "muzik_kapa" | "televizyon_ac" | "televizyon_kapa" | "CHAT",
  "reply": "A short, natural Turkish response."
}

Rules:
- If the user wants to control a device, use the specific device_action command.
- If the user is just chatting or asking a general question, use "CHAT".
- The "reply" MUST be in Turkish.
- For "CHAT", reply naturally to the user's input.
- For commands, confirm the action in the reply.

Examples:
- "Klimayı aç" -> {"command": "klima_ac", "reply": "Tamam, klimayı açıyorum."}
- "Işıkları kapat" -> {"command": "isik_kapa", "reply": "Işıkları kapattım."}
- "Merhaba, nasılsın?" -> {"command": "CHAT", "reply": "Merhaba! İyiyim, teşekkürler. Size nasıl yardımcı olabilirim?"}
- "Bugün hava nasıl?" -> {"command": "CHAT", "reply": "Hava durumu hakkında bilgim yok ama sıcakladıysan klimayı ayarlayabilirim."}
- "Kahve yap" -> {"command": "kahve_ac", "reply": "Hemen kahvenizi hazırlıyorum."}

Output strictly JSON.
"""


MAX_HISTORY_LENGTH = 6 
conversation_history: list = []


def analyze_intent(text: str) -> dict:
    """
    Analyzes the text using Qwen 2.5 via Ollama to determine the intent.
    Maintains conversation history for context.
    """
    global conversation_history
    client = ollama.Client(host=OLLAMA_HOST)

    # Add user message to history
    conversation_history.append({"role": "user", "content": f"Command: {text}"})

    # keep only last N messages
    if len(conversation_history) > MAX_HISTORY_LENGTH:
        conversation_history = conversation_history[-MAX_HISTORY_LENGTH:]

    try:
        # Build messages with system prompt + conversation history
        messages = [{"role": "system", "content": SYSTEM_PROMPT}] + conversation_history

        response = client.chat(
            model=OLLAMA_MODEL,
            messages=messages,
            format="json",
        )

        content = response["message"]["content"]
        intent = json.loads(content)

        # Add response to history
        assistant_reply = intent.get("reply", "")
        conversation_history.append({"role": "assistant", "content": content})

        return intent
    except Exception as e:
        print(f"LLM Error: {e}")

        if conversation_history and conversation_history[-1]["role"] == "user":
            conversation_history.pop()
        return {"command": "UNKNOWN", "reply": "Bir hata oluştu.", "error": str(e)}


def generate_speech(text: str) -> bytes:
    """
    Generates speech from text using the local XTTS v2 model.
    """
    if not app_context.TTS_MODEL:
        print("Error: TTS model is not initialized.")
        return b""

    try:
        print(f"Generating speech for: '{text}' using XTTS v2")

        # Generate audio using XTTS v2
        wav_data = app_context.TTS_MODEL.tts(
            text=text,
            language=TTS_LANGUAGE,
            speaker_wav=TTS_SPEAKER_WAV,
        )

        # Convert to WAV bytes
        buffer = io.BytesIO()
        sample_rate = 24000
        wav_array = np.array(wav_data)
        wav_int16 = (wav_array * 32767).astype(np.int16)
        wav.write(buffer, sample_rate, wav_int16)
        buffer.seek(0)

        print(f"Speech generated successfully, size: {buffer.getbuffer().nbytes} bytes")
        return buffer.read()

    except Exception as e:
        print(f"TTS Error: {e}")
        import traceback

        traceback.print_exc()
        return b""
