import secrets

from fastapi import WebSocket, status
from .config import CONTROLLER_ID, LLM_SECRET_TOKEN, UNITY_CLIENT_TOKEN, logger


def _safe_compare(provided: str | None, expected: str | None) -> bool:
    if provided is None or expected is None:
        return False
    return secrets.compare_digest(provided.encode(), expected.encode())


async def validate_auth(
    websocket: WebSocket, client_id: str, token: str | None
) -> bool:
    is_authorized = False

    if client_id == CONTROLLER_ID:
        if _safe_compare(token, LLM_SECRET_TOKEN):
            is_authorized = True
    else:
        if _safe_compare(token, UNITY_CLIENT_TOKEN):
            is_authorized = True

    if not is_authorized:
        logger.warning("Unauthorized connection attempt: %s", client_id)
        await websocket.close(code=status.WS_1008_POLICY_VIOLATION)
        return False

    return True
