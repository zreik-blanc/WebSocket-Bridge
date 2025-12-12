import os
import tempfile
import requests

from fastapi import HTTPException, UploadFile

from server.app import app_context
import json
import ollama
from server.app.config import OLLAMA_MODEL, OLLAMA_HOST, FISH_SPEECH_API_URL


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


def analyze_intent(text: str) -> dict:
    """
    Analyzes the text using Llama 3.2 via Ollama to determine the intent.
    """
    client = ollama.Client(host=OLLAMA_HOST)

    try:
        response = client.chat(
            model=OLLAMA_MODEL,
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": f"Command: {text}"},
            ],
            format="json",
        )

        content = response["message"]["content"]
        intent = json.loads(content)
        return intent
    except Exception as e:
        print(f"LLM Error: {e}")
        return {"command": "UNKNOWN", "reply": "Bir hata oluştu.", "error": str(e)}


def generate_speech(text: str) -> bytes:
    """
    Generates speech from text using the Fish Speech from the Docker container.
    """
    try:

        payload = {"text": text, "format": "wav", "reference_id": "voice"}

        print(f"Generating speech for: '{text}' at {FISH_SPEECH_API_URL}")
        response = requests.post(FISH_SPEECH_API_URL, json=payload, timeout=10)

        if response.status_code == 200:
            return response.content
        else:
            print(f"Fish Speech API Error: {response.status_code} - {response.text}")
            return b""
    except Exception as e:
        print(f"TTS Error: {e}")
        return b""
