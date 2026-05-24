# Contract Testing Playbook

## What Is Consumer-Driven Contract Testing?

Contract testing solves the **integration trust problem** in microservices: how can Service A be confident that Service B's API still behaves as expected after a change?

**Consumer-driven contract testing** (Pact) flips the script:
- The **consumer** writes tests that define exactly what it needs from the provider
- Running those tests generates a **pact file** (JSON) — the contract
- The **provider** runs those contract assertions against its own code, without the consumer being online

```
Consumer (OrderService)          Provider (UserService)
        │                                │
        │  defines expectations          │
        │  → pact file generated         │
        │                                │
        └───── pact JSON ──────────────► │
                                         │  PactVerifier runs
                                         │  against real code
                                         │  ✓ or ✗
```

---

## Current Contracts

| Consumer | Provider | Interaction | Pact file |
|---|---|---|---|
| order-service | user-service | `GET /users/{id}` — returns user with id, name, email | `tests/ContractTests/pacts/order-service-user-service.json` |

---

## How to Run Contract Tests

### Step 1 — Generate the pact file (consumer side)

```bash
dotnet test tests/ContractTests/ContractTests.csproj \
  --filter "FullyQualifiedName~Consumer"
```

This produces `tests/ContractTests/pacts/order-service-user-service.json`. Commit this file — it is the living contract.

### Step 2 — Verify the provider

```bash
dotnet test tests/ContractTests/ContractTests.csproj \
  --filter "FullyQualifiedName~Provider"
```

The provider test spins up UserService via `WebApplicationFactory`, seeds the required test data, and runs every interaction defined in the pact file.

---

## How to Add a New Contract

### Scenario: PaymentService needs to call OrderService's `GET /orders/{id}`

**1. Write the consumer test** (`tests/ContractTests/Consumer/PaymentConsumerTests.cs`):

```csharp
_pact
    .UponReceiving("a request to get an order by id")
    .WithRequest(HttpMethod.Get, $"/orders/{orderId}")
    .WillRespond()
    .WithStatus(HttpStatusCode.OK)
    .WithJsonBody(new {
        id     = Match.Type(orderId.ToString()),
        userId = Match.Type(Guid.Empty.ToString()),
        amount = Match.Type(100.00m)
    });
```

**2. Run the consumer test** — generates `pacts/payment-service-order-service.json`.

**3. Write the provider test** (`tests/ContractTests/Provider/OrderProviderTests.cs`):

```csharp
verifier
    .WithHttpEndpoint(server.BaseAddress)
    .WithFileSource(new FileInfo("pacts/payment-service-order-service.json"))
    .Verify();
```

**4. Commit the pact JSON** — the contract is now part of the repository.

---

## Rules

1. **Never edit a pact JSON file manually** — it is generated output. Change the consumer test instead.
2. **Consumer owns the contract** — if a consumer test changes, the provider must re-verify before the change ships.
3. **Provider must pass contract verification before deploy** — enforced by the CI quality gate.
4. **Pact files are committed to `tests/ContractTests/pacts/`** — they are living documentation, not build artifacts.

---

## Common Failures and Fixes

| Failure | Cause | Fix |
|---|---|---|
| `Pact file not found` | Consumer tests not run first | Run consumer tests before provider tests |
| `Body does not match` | Provider changed a response field | Update provider code or add the field back; update consumer if intentional |
| `Unexpected call` | Provider returned extra fields with `additionalProperties: false` | Relax the consumer matcher to allow extra fields |
| `404 on provider` | Seeded test data missing | Add the required ID to the provider test setup |
