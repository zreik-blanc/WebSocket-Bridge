# LLM to Unity Command Bridge

This project contains a web server that acts as a bridge between an LLM (Large Language Model) and a Unity application.

## Structure

- **server/**: A Python FastAPI server that handles LLM requests and communicates with Unity via WebSockets.
- **unity_project/**: A Unity project structure containing scripts to receive and execute commands.

## Setup

### Server
1. Navigate to `server/`
2. Install dependencies: `pip install -r requirements.txt`
3. Run the server: `uvicorn main:app --reload`

### Unity
1. Open `unity_project/` in Unity Hub.
2. Attach `CommandListener.cs` to a GameObject in your scene.

how it did went throuht brooo
ok i fixed it