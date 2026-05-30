# Microservices Testing Architecture

A .NET 8 microservices reference project focused on end-to-end quality engineering:

- Event-driven services with Kafka
- Service-owned SQL databases
- Multi-layer automated test strategy
- CI quality gates in GitHub Actions

---

## Architecture at a glance

| Service | Port | Role |
|---|---:|---|
| UserService | 5001 | User APIs |
| OrderService | 5002 | Order APIs + publishes `order-created` |
| PaymentService | 5003 | Consumes `order-created`, publishes `payment-processed` |
| NotificationService | 5004 | Consumes both topics, stores notification log |

Event flow:

`POST /orders` → `order-created` → PaymentService → `payment-processed` → NotificationService

---

## Testing coverage

| Layer | Purpose | Location |
|---|---|---|
| Contract Tests | Consumer/provider contract validation | `tests/ContractTests/` |
| API Tests | Endpoint behavior and response assertions | `tests/ApiTests/` |
| Integration Tests | Service + DB integration with Testcontainers | `tests/IntegrationTests/` |
| Event Tests | Kafka event shape, keys, schema checks | `tests/EventTests/` |
| E2E Tests | Full workflow verification across services | `tests/E2eTests/` |

---

## CI quality gates

Pipeline file: `.github/workflows/ci.yml`

```text
build
 ├─ contract-tests
 ├─ api-tests
 ├─ integration-tests
 └─ event-tests
      └─ e2e-tests
```

`e2e-tests` runs only after all earlier layers pass.

---

## Quick start

### Prerequisites

- .NET 8 SDK
- Docker + Docker Compose v2

### Run full stack

```bash
docker compose -f infra/docker-compose.yml up --build
```

Swagger:

- http://localhost:5001/swagger
- http://localhost:5002/swagger
- http://localhost:5003/swagger
- http://localhost:5004/swagger

### Run tests

```bash
dotnet test tests/ContractTests/ContractTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj
dotnet test tests/ApiTests/ApiTests.csproj

docker compose -f infra/docker-compose.yml up -d kafka kafka-init
KAFKA_BOOTSTRAP_SERVERS=localhost:9094 dotnet test tests/EventTests/EventTests.csproj

docker compose -f infra/docker-compose.yml up -d
dotnet test tests/E2eTests/E2eTests.csproj
```

---

## Repository map

| Path | Description |
|---|---|
| `services/` | Microservice source code |
| `tests/` | Automated tests by layer |
| `infra/` | Compose stack, Kafka topic init, SQL init |
| `.github/workflows/` | CI pipelines and quality gates |
| `docs/` | Detailed architecture and testing guides |

---

## Documentation index

- [Testing Strategy](docs/testing-strategy.md)
- [Contract Testing Playbook](docs/contract-testing-playbook.md)
- [Event-Driven Testing Guide](docs/event-driven-testing.md)
- [Architecture Diagram](docs/architecture-diagram.md)
