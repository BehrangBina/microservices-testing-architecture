# Microservices Testing Architecture

A production-grade QE portfolio project demonstrating **Practice Lead / Test Architect** capabilities across every layer of the testing pyramid — built on C# .NET 8, Kafka, SQL Server, and GitHub Actions CI.

---

## What's Inside

| Layer | Technology | Location |
|---|---|---|
| Microservices (4) | ASP.NET Core 8, EF Core, SQL Server | `services/` |
| API Tests | RestSharp + xUnit + FluentAssertions | `tests/ApiTests/` |
| Contract Tests | Pact.NET 5.x (consumer-driven, file-based) | `tests/ContractTests/` |
| Event-Driven Tests | Confluent.Kafka + NJsonSchema | `tests/EventTests/` |
| Integration Tests | Testcontainers.MsSql + WebApplicationFactory | `tests/IntegrationTests/` |
| E2E Tests | Playwright API context + polling assertions | `tests/E2eTests/` |
| Infrastructure | Docker Compose, Kafka KRaft, SQL Server 2022 | `infra/` |
| CI/CD | GitHub Actions (6-job pipeline with quality gates) | `.github/workflows/ci.yml` |
| Documentation | Strategy, playbooks, architecture diagrams | `docs/` |

---

## Services

```
UserService        :5001   CRUD /users
OrderService       :5002   CRUD /orders + publishes OrderCreated → Kafka
PaymentService     :5003   GET /payments + consumes OrderCreated → publishes PaymentProcessed
NotificationService :5004  GET /notifications + consumes both Kafka topics
```

Event flow: `POST /orders` → `order-created` topic → PaymentService → `payment-processed` topic → NotificationService

---

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Run the full stack

```bash
docker compose -f infra/docker-compose.yml up --build
```

Services available at:
- UserService → http://localhost:5001/swagger
- OrderService → http://localhost:5002/swagger
- PaymentService → http://localhost:5003/swagger
- NotificationService → http://localhost:5004/swagger

### Run tests

```bash
# Contract tests (no external dependencies)
dotnet test tests/ContractTests/ContractTests.csproj

# Integration tests (Testcontainers spins SQL Server automatically)
dotnet test tests/IntegrationTests/IntegrationTests.csproj

# API tests (requires docker-compose stack running)
dotnet test tests/ApiTests/ApiTests.csproj

# Event tests (requires Kafka — start with docker compose)
docker compose -f infra/docker-compose.yml up -d kafka kafka-init
KAFKA_BOOTSTRAP_SERVERS=localhost:9094 dotnet test tests/EventTests/EventTests.csproj

# E2E tests (requires full docker-compose stack)
docker compose -f infra/docker-compose.yml up -d
dotnet test tests/E2eTests/E2eTests.csproj
```

---

## CI/CD Pipeline

The GitHub Actions pipeline enforces a strict quality gate order:

```
build ──► contract-tests ──┐
      ──► api-tests        ├──► e2e-tests ──► (deploy)
      ──► integration-tests┤
      ──► event-tests ─────┘
```

E2E tests only run after **all** preceding test layers pass.

---

## Documentation

| Document | Description |
|---|---|
| [Testing Strategy](docs/testing-strategy.md) | Testing pyramid, layer ownership, quality gates, technology decisions |
| [Contract Testing Playbook](docs/contract-testing-playbook.md) | How to run, add, and maintain Pact contracts |
| [Event-Driven Testing Guide](docs/event-driven-testing.md) | Kafka patterns, schema validation, consumer isolation, anti-patterns |
| [Architecture Diagram](docs/architecture-diagram.md) | Mermaid diagrams — services, topics, DBs, test layer overlay, CI flow |

---

## Key Design Decisions

- **Confluent.Kafka directly** (not MassTransit) — demonstrates low-level Kafka understanding
- **Pact file-based** (no broker) — pact JSONs committed to repo as living contract artifacts
- **Testcontainers** — integration tests are fully self-contained; no shared database
- **Playwright API context** — showcases Playwright for non-browser async workflow assertions
- **EnsureCreated()** over migrations — pragmatic for demo; production would use a proper migration pipeline

---

## Requirements

| Tool | Version |
|---|---|
| .NET SDK | 8.0 |
| Docker | 24+ |
| Docker Compose | v2 |
