import json

from fastapi import APIRouter, WebSocket, WebSocketDisconnect, Header, status
from ..config import logger, CONTROLLER_ID
from ..connection_manager import manager
from ..dependencies import validate_auth

router = APIRouter()


@router.websocket("/ws/{client_id}")
async def websocket_endpoint(
    websocket: WebSocket,
    client_id: str,
    x_auth_token: str | None = Header(default=None),
):
    # 1. Authentication
    if not await validate_auth(websocket, client_id, x_auth_token):
        return

    # 2. Duplicate Connection Check
    if client_id in manager.active_connections:
        logger.warning(f"Client ID '{client_id}' is already connected.")
        await websocket.close(code=status.WS_1000_NORMAL_CLOSURE)
        return

    # 3. Connection Accepted
    await manager.connect(websocket, client_id)
    logger.info(f"Client connected: {client_id}")

    try:
        while True:
            data = await websocket.receive_text()

            # --- Logic for Controller (LLM) ---
            if client_id == CONTROLLER_ID:
                try:
                    message_data = json.loads(data)
                    target = message_data.get("target")
                    command = message_data.get("message")

                    if target and command:
                        logger.debug(f"LLM -> Redis Pub/Sub -> ({target}): {command}")
                        await manager.send_message(command, target)
                    else:
                        await manager.send_message(
                            "Invalid JSON. Need 'target' and 'message'", CONTROLLER_ID
                        )
                except json.JSONDecodeError:
                    await manager.send_message("Invalid JSON format.", CONTROLLER_ID)

            # --- Logic for Clients (Unity/Devices) ---
            else:
                logger.debug(
                    f"{client_id} -> Redis Pub/Sub -> ({CONTROLLER_ID}): {data}"
                )
                response = json.dumps({"sender": client_id, "message": data})
                await manager.send_message(response, CONTROLLER_ID)

    except WebSocketDisconnect:
        manager.disconnect(client_id)
        logger.info(f"Client disconnected: {client_id}")
