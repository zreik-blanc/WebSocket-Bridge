import os
import sys
import logging

from dotenv import load_dotenv

load_dotenv()

# --- Logging Configuration ---
LOG_LEVEL = os.environ.get("LOG_LEVEL", "INFO").upper()
logging.basicConfig(
    level=getattr(logging, LOG_LEVEL, logging.INFO),
    format="%(asctime)s - %(levelname)s - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger("app")

# --- Constants & Secrets ---
CONTROLLER_ID = "LLM"
LLM_SECRET_KEY = os.environ.get("LLM_SECRET_KEY")
UNITY_CLIENT_KEY = os.environ.get("UNITY_CLIENT_KEY")
REDIS_URL = os.environ.get("REDIS_URL", "redis://redis:6379")


# --- Startup Check ---
def check_keys():
    if not LLM_SECRET_KEY or not UNITY_CLIENT_KEY:
        logger.critical("Security keys are missing from environment variables.")
        sys.exit(1)
