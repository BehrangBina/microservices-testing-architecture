# Architecture Diagram

## System Overview

```mermaid
graph TB
    subgraph "Client"
        C[HTTP Client / Test Runner]
    end

    subgraph "Microservices"
        US[UserService<br/>:5001<br/>GET/POST/DELETE /users]
        OS[OrderService<br/>:5002<br/>GET/POST /orders]
        PS[PaymentService<br/>:5003<br/>GET /payments]
        NS[NotificationService<br/>:5004<br/>GET /notifications]
    end

    subgraph "Kafka Topics"
        T1[order-created<br/>3 partitions]
        T2[payment-processed<br/>3 partitions]
    end

    subgraph "SQL Server"
        DB1[(UserDb)]
        DB2[(OrderDb)]
        DB3[(PaymentDb)]
        DB4[(NotificationDb)]
    end

    C -->|REST| US
    C -->|REST| OS
    C -->|REST| PS
    C -->|REST| NS

    US --> DB1
    OS --> DB2
    PS --> DB3
    NS --> DB4

    OS -->|produce| T1
    T1 -->|consume| PS
    T1 -->|consume| NS
    PS -->|produce| T2
    T2 -->|consume| NS
```

---

## Test Layer Overlay

```mermaid
graph LR
    subgraph "E2E Tests (Playwright)"
        direction LR
        E2E[FullOrderWorkflowTests<br/>creates user → order → polls payment → polls notifications]
    end

    subgraph "API Tests (RestSharp)"
        AT1[UserServiceTests]
        AT2[OrderServiceTests]
        AT3[PaymentServiceTests]
        AT4[NotificationServiceTests]
    end

    subgraph "Contract Tests (Pact)"
        CT1[OrderConsumerTests<br/>defines GET /users/{id} contract]
        CT2[UserProviderTests<br/>verifies UserService satisfies contract]
    end

    subgraph "Event Tests (Confluent.Kafka + NJsonSchema)"
        ET1[OrderCreatedEventTests<br/>produce → consume]
        ET2[PaymentProcessedEventTests<br/>produce → consume]
        ET3[SchemaValidationTests<br/>JSON Schema assertions]
    end

    subgraph "Integration Tests (Testcontainers)"
        IT1[UserServiceIntegrationTests<br/>WebApplicationFactory + SQL Server container]
        IT2[OrderServiceIntegrationTests<br/>no-op Kafka producer stub]
    end

    E2E -->|calls| AT1
    E2E -->|calls| AT2
    AT1 & AT2 & AT3 & AT4 -->|hit live services| AT1
    CT1 -->|generates pact JSON| CT2
```

---

## CI/CD Pipeline

```mermaid
flowchart LR
    PR[Push / PR] --> Build

    Build --> ContractTests
    Build --> ApiTests
    Build --> IntegrationTests
    Build --> EventTests

    ContractTests --> E2ETests
    ApiTests --> E2ETests
    IntegrationTests --> E2ETests
    EventTests --> E2ETests

    E2ETests --> Deploy[Deploy to Staging]
```

---

## Port Map

| Service | Internal Port | External Port |
|---|---|---|
| UserService | 8080 | 5001 |
| OrderService | 8080 | 5002 |
| PaymentService | 8080 | 5003 |
| NotificationService | 8080 | 5004 |
| SQL Server | 1433 | 1433 |
| Kafka (external) | 9094 | 9094 |
| Kafka (internal) | 9092 | — |
