I'm currently in **Plan mode**, which restricts me to memory-only writes — I can't create files in the workspace directly. The plan is already saved at `/memories/session/plan.md` for persistence across this session.

To get `plan.md` into your workspace, you have two options:

**Option A — Switch to Agent mode**: Switch to the default Copilot agent, then ask "Create a plan.md file in the workspace root with this content" and paste what I showed.

**Option B — Copy manually**: Here's the exact content ready to paste into a new `plan.md` at `c:\Projects\microservices-testing-architecture\plan.md`:

<details>
<summary>plan.md content (click to expand)</summary>

```markdown
# Plan: QE Portfolio — Microservices Testing Architecture

## Decisions
- .NET 8 (LTS), C# throughout
- All 4 services have SQL Server via EF Core
- Pact: local file-based (no broker service); pact JSONs committed to repo
- E2E: Playwright API request context (no UI/browser)
- Kafka schema: JSON Schema validation (NJsonSchema)
- Kafka infra: bitnami/kafka in KRaft mode (no Zookeeper)
- Confluent.Kafka directly (not MassTransit) — shows low-level understanding
- xUnit for all test projects

---

## Phase 1 — Solution Scaffold & Microservices

1. Create `microservices-testing-architecture.sln` at repo root
2. `services/UserService` — CRUD `/users`, EF Core + SQL Server, Dockerfile
3. `services/OrderService` — REST API + Confluent.Kafka producer → topic `order-created`
4. `services/PaymentService` — REST API + Kafka consumer (`order-created`) + Kafka producer (`payment-processed`)
5. `services/NotificationService` — Kafka consumers for both topics + `GET /notifications` endpoint

## Phase 2 — Infrastructure

1. `infra/docker-compose.yml` — all 4 services + bitnami/kafka (KRaft) + SQL Server 2022
2. `infra/kafka/create-topics.sh` — creates `order-created`, `payment-processed`
3. `infra/sqlserver/init-db.sql` — creates UserDb, OrderDb, PaymentDb, NotificationDb

## Phase 3 — API Tests (RestSharp + xUnit)

- `tests/ApiTests/` — happy paths + edge cases for all 4 services
- `ApiTestBase.cs` — RestClient factory, base URLs from env vars

## Phase 4 — Contract Tests (Pact.NET 5.x)

- `tests/ContractTests/Consumer/OrderConsumerTests.cs` — defines pact against UserService GET /users/{id}, generates `pacts/order-service-user-service.json`
- `tests/ContractTests/Provider/UserProviderTests.cs` — verifies UserService via WebApplicationFactory

## Phase 5 — Event-Driven Tests (Confluent.Kafka + NJsonSchema)

- `tests/EventTests/` — produce/consume tests for OrderCreated and PaymentProcessed
- `SchemaValidationTests.cs` — validates payloads against `Schemas/*.schema.json`
- `KafkaTestFixture.cs` — isolated `test-{guid}` prefixed topics per run

## Phase 6 — Integration Tests (Testcontainers.MsSql)

- `tests/IntegrationTests/Fixtures/SqlServerFixture.cs` — IAsyncLifetime, spins SQL Server container, runs EF Core migrations
- `UserServiceIntegrationTests.cs` + `OrderServiceIntegrationTests.cs` — WebApplicationFactory with overridden connection string

## Phase 7 — E2E Tests (Playwright API context)

Full workflow against running docker-compose stack:
1. POST /users → 201
2. POST /orders → 201, capture orderId
3. Poll GET /payments?orderId={id} (retry ≤10s) → assert payment created by event consumer
4. Poll GET /notifications?orderId={id} (retry ≤10s) → assert notification logged

## Phase 8 — CI/CD (GitHub Actions)

`.github/workflows/ci.yml` — 6 jobs:
- `build` → `api-tests` + `contract-tests` (parallel) → `event-tests` + `integration-tests` (parallel) → `e2e-tests`
- TRX test results → GitHub Actions test summary as quality gate

## Phase 9 — Documentation

- `docs/testing-strategy.md` — testing pyramid, layer ownership, quality gates
- `docs/contract-testing-playbook.md` — how to add/run pact contracts
- `docs/event-driven-testing.md` — Kafka patterns, schema validation, consumer isolation
- `docs/architecture-diagram.md` — Mermaid diagram: services, topics, DBs, test layer overlay

---

## Verification

1. `dotnet build microservices-testing-architecture.sln` — zero errors
2. `dotnet test tests/ContractTests` — pact JSON generated + provider passes (no external deps)
3. `dotnet test tests/IntegrationTests` — Testcontainers auto-provisions SQL Server
4. `docker compose -f infra/docker-compose.yml up -d` — all services + Kafka + SQL Server healthy
5. `dotnet test tests/EventTests` — Kafka produce/consume + schema tests pass
6. `dotnet test tests/E2eTests` — full order workflow passes end-to-end
7. GitHub Actions CI green across all 6 jobs
```

</details>

Once you approve the plan and switch to Agent mode, I can implement the entire project end-to-end.