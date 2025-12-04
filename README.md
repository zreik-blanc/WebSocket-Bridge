# WebSocket Bridge

A real-time WebSocket communication bridge that connects LLM (Large Language Model) controllers with clients. Built with **FastAPI**, **Redis Pub/Sub**, and **Traefik** reverse proxy.

![Python](https://img.shields.io/badge/Python-3.11+-blue?logo=python&logoColor=white)
![FastAPI](https://img.shields.io/badge/FastAPI-0.122-009688?logo=fastapi&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7.1-DC382D?logo=redis&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)
![Terraform](https://img.shields.io/badge/Terraform-AWS-7B42BC?logo=terraform&logoColor=white)

---

## Overview

WebSocket Bridge enables seamless bidirectional communication between:
- **LLM Controller**: Sends commands to connected clients
- **Clients**: Receive commands and send responses back to the LLM

Messages are routed through **Redis Pub/Sub** for scalability and reliability.

```
┌──────────────┐          ┌─────────────────┐          ┌──────────────┐
│     LLM      │ ◄──────► │  WebSocket      │ ◄──────► │   Clients    │
│  Controller  │    WS    │  Bridge Server  │    WS    │              │
└──────────────┘          └────────┬────────┘          └──────────────┘
                                   │
                          ┌────────▼────────┐
                          │  Redis Pub/Sub  │
                          └─────────────────┘
```

---

## Features

- **WebSocket Communication** - Real-time bidirectional messaging
- **Redis Pub/Sub** - Scalable message routing between clients
- **Token Authentication** - Secure connections with auth tokens
- **Rate Limiting** - Built-in protection via Traefik middleware
- **Docker Compose** - Easy local development and deployment
- **AWS Infrastructure** - Production-ready Terraform configs
- **TLS/HTTPS** - Auto SSL certificates with Let's Encrypt
- **AWS SSM Integration** - Secure secrets management

---

## Project Structure

```
WebSocket-Bridge/
├── server/                    # FastAPI WebSocket Server
│   ├── app/
│   │   ├── main.py           # Application entry point
│   │   ├── config.py         # Configuration & secrets
│   │   ├── connection_manager.py  # WebSocket & Redis management
│   │   ├── dependencies.py   # Auth validation
│   │   └── routers/
│   │       ├── system.py     # Health check endpoints
│   │       └── websocket.py  # WebSocket endpoint logic
│   ├── tests/                # Test suite
│   ├── Dockerfile
│   └── requirements.txt
├── unity_project/            # Unity client scripts
│   └── Assets/Scripts/Network/
│       └── CommandListener.cs
├── terraform/                # AWS Infrastructure as Code
│   ├── main.tf              # EC2, VPC, Security Groups
│   ├── variables.tf
│   └── scripts/install.sh   # Server bootstrap script
├── docker-compose.yml        # Development environment
└── docker-compose.prod.yml   # Production environment
```

---

## Quick Start

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) & Docker Compose
- Python 3.12+ (for local development)

### 1. Clone the Repository

```bash
git https://github.com/zreik-blanc/WebSocket-Bridge.git
```

### 2. Configure Environment Variables

Create a `.env` file in the root directory:

```env
# Authentication Tokens
LLM_SECRET_TOKEN=your_llm_secret_token
UNITY_CLIENT_TOKEN=your_unity_client_token

# Redis Configuration
REDIS_PASSWORD=your_redis_password
REDIS_HOST=redis

# Server Configuration (Production)
WEB_SOCKET_PORT=8000
DOMAIN_NAME=your-domain.com
ACME_EMAIL=your-email@example.com

# Logging
LOG_LEVEL=INFO
```

### 3. Run with Docker Compose

**Development:**
```bash
docker compose up -d
```

**Production (with TLS):**
```bash
docker compose -f docker-compose.prod.yml up -d
```

### 4. Test the Connection

The WebSocket endpoint is available at:
- **Development:** `ws://localhost/ws/{client_id}`
- **Production:** `wss://your-domain.com/ws/{client_id}`

---

## WebSocket API

### Endpoint

```
ws(s)://{host}/ws/{client_id}
```

### Headers

| Header | Required | Description |
|--------|----------|-------------|
| `X-Auth-Token` | Yes | Authentication token (LLM or Client token) |

### Client ID Format

- Alphanumeric with underscores/hyphens
- 1-50 characters
- Pattern: `^[a-zA-Z0-9_-]{1,50}$`

### Message Formats

**LLM Controller → Client:**
```json
{
  "target": "unity_client_1",
  "message": "MoveForward 10"
}
```

**Client → LLM Controller:**
```json
{
  "sender": "unity_client_1",
  "message": "Movement completed"
}
```

---

## AWS Deployment

### Infrastructure with Terraform

```bash
cd terraform

# Initialize Terraform
terraform init

# Preview changes
terraform plan

# Deploy infrastructure
terraform apply
```

This provisions:
- VPC with public subnets
- EC2 instance (Ubuntu 24.04)
- Security groups (SSH, HTTP, HTTPS)
- Auto-installation via user data script

### AWS SSM Parameter Store

Store secrets securely in AWS Parameter Store:
```
/WebSocket/prod/LLM_SECRET_TOKEN
/WebSocket/prod/UNITY_CLIENT_TOKEN
/WebSocket/prod/REDIS_PASSWORD
```

---

## Development

### Local Server (without Docker)

```bash
cd server
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | FastAPI, Python 3.11+ |
| Message Broker | Redis Pub/Sub |
| Reverse Proxy | Traefik v3 |
| Containerization | Docker, Docker Compose |
| Infrastructure | Terraform, AWS |
| SSL/TLS | Let's Encrypt (ACME) |
| Game Engine | Unity (C#) |

---

## Roadmap

- [ ] Getting Secrets From AWS SSM Parameter Store
- [ ] Prometheus & Grafana monitoring

---

## License

This project is licensed under the terms specified in [LICENSE](LICENSE).

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/super-idea`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/super-idea`)
5. Open a Pull Request
