# AI implementation guardrails

## Architecture boundaries
- Keep Clean Architecture dependency direction strict:
  - `Domain` references nothing.
  - `Application` references `Domain` and `Contracts` only.
  - `Infrastructure` implements `Application` abstractions.
  - API/Worker compose dependencies via DI only.
- Never place SQL/Cosmos SDK calls inside `Domain` or controller actions.

## Data synchronization rules
- Preserve deterministic document IDs (`trade:{sourceId}`).
- Preserve idempotency: ignore stale/replayed CDC changes via LSN comparison.
- Use continuation-token pagination; do not introduce skip/offset pagination.
- Keep delete semantics as tombstones unless explicitly changed by stakeholder decision.

## API and security rules
- All read endpoints must require JWT auth and policy-based authorization.
- Enforce account scoping from claims for every query.
- Return DTOs only; never expose persistence documents.

## Operational rules
- Always pass `CancellationToken` through async operations.
- Use structured logging; do not log sensitive trading payloads.
- Keep infrastructure/IaC changes reviewed separately from domain logic changes.
- Use managed identity or secure secret stores; never commit credentials.

## Testing rules
- Add/adjust tests for every change in:
  - mapping logic,
  - synchronization/checkpoint behavior,
  - API contracts and validation.
- Keep solution build and relevant tests green before completion.
