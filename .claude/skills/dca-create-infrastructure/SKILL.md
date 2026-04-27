---
name: create-infrastructure
description: >-
  Guides creation of infrastructure services in APSYS .NET backend projects.
  Covers dependency injection registration, HTTP client services (IHttpClientFactory),
  caching (Redis IDistributedCache, IMemoryCache), and other cross-cutting
  infrastructure services. Use when user asks to "register services", "configure DI",
  "add an HTTP client", "set up caching", "create an external service",
  or "configure dependency injection" in Clean Architecture.
compatibility: >-
  Requires .NET backend projects using Clean Architecture with
  Microsoft.Extensions.DependencyInjection. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 1.0.0
---

# Infrastructure Services Skill

Guide for creating and maintaining infrastructure services in APSYS .NET backend projects:
dependency injection configuration, HTTP client services, caching, and external service integrations.

## Instructions

### Step 1: Identify the Service Type

Use the decision tree to determine what to create:

```
What do you need in the Infrastructure Layer?
├── External API call? → HTTP Client Service (see references/http-clients.md)
├── Distributed cache? → Redis Caching (see references/redis-caching.md)
├── In-memory cache? → Memory Cache (see references/memory-caching.md)
├── Service registration? → DI Configuration (see references/dependency-injection.md)
└── Other service? → Implement domain interface in Infrastructure layer
```

### Step 2: Check the Design Document

Read the Design document for architectural decisions:
- Which services are needed and why?
- What lifetime (Singleton/Scoped/Transient) was decided?
- Are there resilience requirements (retry, circuit breaker)?
- What caching strategy was chosen?

### Step 3: Implement the Service

1. Define the interface in **Domain** (if not already defined)
2. Create the implementation in **Infrastructure**
3. Register in DI via extension method in **WebApi**

Follow the patterns in the references for the specific service type.

### Step 4: Verify

- [ ] Interface defined in Domain layer
- [ ] Implementation in Infrastructure layer
- [ ] DI registration via extension method
- [ ] Correct lifetime (see references/dependency-injection.md)
- [ ] No captive dependencies (Singleton depending on Scoped)
- [ ] Error handling follows Result pattern (see create-use-case references)

## References

| Component | Reference |
|-----------|-----------|
| DI Configuration | [references/dependency-injection.md](references/dependency-injection.md) |
| HTTP Clients | [references/http-clients.md](references/http-clients.md) |
| Redis Caching | [references/redis-caching.md](references/redis-caching.md) |
| Memory Caching | [references/memory-caching.md](references/memory-caching.md) |
