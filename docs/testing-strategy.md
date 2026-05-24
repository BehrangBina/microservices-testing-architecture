# Testing Strategy

## Overview

This project demonstrates a **layered testing strategy** for a microservices architecture, owned and designed from a **Practice Lead / Test Architect** perspective. Each layer has a defined purpose, scope, and ownership model.

---

## Testing Pyramid

```
         ┌──────────────────────────┐
         │      E2E Tests           │  ← Playwright · Full stack · Slowest
         │   (5% of test suite)     │
         ├──────────────────────────┤
         │   Integration Tests      │  ← Testcontainers · Real DB · Medium speed
         │   (15% of test suite)    │
         ├──────────────────────────┤
         │   API Tests (Component)  │  ← RestSharp · Service boundaries
         │   (20% of test suite)    │
         ├──────────────────────────┤
         │   Contract Tests (Pact)  │  ← Fast · No external deps
         │   (10% of test suite)    │
         ├──────────────────────────┤
         │  Event-Driven Tests      │  ← Kafka produce/consume/schema
         │  (10% of test suite)     │
         ├──────────────────────────┤
         │    Unit Tests            │  ← Fastest · Most numerous
         │   (40% of test suite)    │
         └──────────────────────────┘
```

---

## Layer Definitions

### Unit Tests
- **Purpose**: Validate business logic in isolation
- **Scope**: Single class/method, all dependencies mocked
- **Location**: Within each service project (`*.Tests` co-located)
- **Speed**: < 1 ms per test
- **Quality Gate**: 100% must pass before PR merge

### Contract Tests (Pact)
- **Purpose**: Prevent integration breaks caused by API changes
- **Pattern**: Consumer-driven — the consumer defines expectations, the provider verifies
- **Tools**: PactNet 5.x, local file-based pacts
- **Key Contract**: `order-service → user-service` (GET /users/{id})
- **Speed**: Fast — uses mock server, no live services
- **Quality Gate**: Provider verification must pass before a service is deployed

### Event-Driven Tests
- **Purpose**: Validate Kafka event produce/consume behaviour and schema correctness
- **Scope**: Isolated Kafka topics (`test-{guid}` prefix) per test run
- **Tools**: Confluent.Kafka, NJsonSchema
- **Patterns covered**: Produce → Consume, Schema Validation, Idempotent consumer, Status enum validation
- **Quality Gate**: Schema changes must not break existing consumers

### API Tests (Component Tests)
- **Purpose**: Black-box test each service through its HTTP interface
- **Scope**: Single service + its database (no cross-service calls)
- **Tools**: RestSharp, xUnit, FluentAssertions
- **Covers**: Happy paths, 400/404 edge cases, query filters

### Integration Tests
- **Purpose**: Validate the full request → DB persistence path for each service
- **Tools**: Testcontainers.MsSql (real SQL Server in Docker), WebApplicationFactory
- **Key patterns**: Schema creation, CRUD round-trips, Kafka producer stubbed with no-op
- **Self-contained**: No external dependencies — Testcontainers spins its own SQL Server

### E2E Tests
- **Purpose**: Validate the entire business workflow across all services and events
- **Tools**: Playwright API context, PollingHelper (retry-with-timeout for async events)
- **Workflow**: Create User → Create Order → Assert Payment → Assert Notifications
- **Key technique**: Polling assertions with bounded timeout for eventual consistency

---

## Quality Gates (CI Pipeline)

| Stage | Gate | Blocks |
|---|---|---|
| PR raised | Contract tests pass | Merge |
| PR raised | Build pass | Merge |
| Merge to main | API + Integration + Event tests pass | Deploy |
| Deploy to staging | E2E tests pass | Deploy to production |

---

## Ownership Model

| Layer | Owner | Frequency |
|---|---|---|
| Unit | Feature developer | Every commit |
| Contract | QE (consumer side) + developer (provider) | Every API change |
| Event schema | QE + Platform team | Every event change |
| API tests | QE | Every service release |
| Integration | QE | Every sprint |
| E2E | QE lead | Every release |

---

## Technology Decisions

| Decision | Rationale |
|---|---|
| Confluent.Kafka directly | Shows low-level understanding; MassTransit hides important details |
| Testcontainers.MsSql | Fully self-contained; no shared test database; no flakiness from shared state |
| Pact (file-based) | No broker infrastructure cost; pacts committed to repo as living artifacts |
| Playwright API context | Demonstrates Playwright beyond browser automation; retry/wait built-in |
| NJsonSchema | Lightweight; easy to integrate; supports JSON Schema draft-07 |
| EnsureCreated() over Migrate() | Simpler for demo; production would use proper migration pipeline |
