from typing import Dict
import asyncio
import redis.asyncio as redis

from redis.asyncio.client import PubSub
from fastapi import WebSocket
from .config import REDIS_URL, logger


class ConnectionManager:
    """
    Manages WebSocket connections and Redis Pub/Sub interactions.
    Each client has its own Pub/Sub connection to listen for messages.
    1. connect: Accepts a WebSocket connection, creates a Pub/Sub connection,
       and subscribes to the client's channel.
    2. disconnect: Cleans up the WebSocket and Pub/Sub connections.
    3. send_message: Publishes messages to a specific client's Redis channel.
    4. listen_to_redis: Listens for messages from Redis and forwards them to
       the appropriate WebSocket client.
    """

    def __init__(self) -> None:
        self.active_connections: Dict[str, WebSocket] = {}
        self.pubsub_connections: Dict[str, PubSub] = {}
        self.redis_client = redis.from_url(
            REDIS_URL, encoding="utf-8", decode_responses=True
        )

    async def connect(self, websocket: WebSocket, client_id: str) -> None:
        # Accept the WebSocket connection
        await websocket.accept()
        self.active_connections[client_id] = websocket

        # Create a PubSub connection for every unique client
        pubsub = self.redis_client.pubsub()
        self.pubsub_connections[client_id] = pubsub

        # Subscribe to the client's specific channel
        await pubsub.subscribe(client_id)

        # Start listening to Redis messages for this client
        asyncio.create_task(self.listen_to_redis(client_id))

    async def _cleanup_pubsub(self, client_id: str) -> None:
        # When disconnecting, unsubscribe and close the PubSub connection
        if client_id in self.pubsub_connections:
            pubsub = self.pubsub_connections[client_id]
            try:
                await pubsub.unsubscribe(client_id)
                await pubsub.close()
            except Exception as e:
                logger.error("Error cleaning up pubsub for %s: %s", client_id, e)
            finally:
                del self.pubsub_connections[client_id]

    def disconnect(self, client_id: str) -> None:
        if client_id in self.active_connections:
            del self.active_connections[client_id]
            asyncio.create_task(self._cleanup_pubsub(client_id))

    async def send_message(self, message: str, target_client_id: str) -> None:
        # Publishes a message to the specified client's Redis channel
        await self.redis_client.publish(target_client_id, message)

    async def listen_to_redis(self, client_id: str) -> None:
        # Starts listening to Redis messages for a specific client
        try:
            pubsub = self.pubsub_connections.get(client_id)
            if not pubsub:
                logger.error("No pubsub connection found for %s", client_id)
                return

            async for message in pubsub.listen():
                if message["type"] == "message":
                    payload = message["data"]

                    if client_id in self.active_connections:
                        socket = self.active_connections[client_id]
                        await socket.send_text(payload)
                    else:
                        break

        except asyncio.CancelledError:
            logger.info("Redis listener cancelled for %s", client_id)
        except Exception as e:
            logger.error("Redis listener error for %s: %s", client_id, e)


manager = ConnectionManager()
