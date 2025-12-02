from fastapi import WebSocket, status
from .config import CONTROLLER_ID, LLM_SECRET_KEY, UNITY_CLIENT_KEY, logger


async def validate_auth(
    websocket: WebSocket, client_id: str, token: str | None
) -> bool:
    is_authorized = False

    if client_id == CONTROLLER_ID:
        if token == LLM_SECRET_KEY:
            is_authorized = True
    else:
        if token == UNITY_CLIENT_KEY:
            is_authorized = True

    if not is_authorized:
        logger.warning("Unauthorized connection attempt: %s", client_id)
        await websocket.close(code=status.WS_1008_POLICY_VIOLATION)
        return False

    return True
