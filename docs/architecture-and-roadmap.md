# Mediatoid Architecture and Roadmap (v0.4.0)

This document summarizes the architectural layers of the Mediatoid library, the behavior contracts that are guaranteed today, and the improvements shipped in the v0.4.0 series.

---

## 1. Architectural Layers

Mediatoid is designed with a layered architecture. Each layer has clear responsibilities, and upper layers depend on lower layers, never the other way around.

### 1.1. `Mediatoid.Core`

- **Purpose:**
  - Provides core contracts and abstract types.
  - Has no dependency on DI frameworks, Source Generators or a specific runtime.
- **Contents (examples):**
  - `IRequest<TResponse>`, `INotification`, `IStreamRequest<TItem>`
  - `IRequestHandler<,>`, `INotificationHandler<>`, `IStreamRequestHandler<,>`
  - `IPipelineBehavior<TRequest, TResponse>`, `RequestHandlerContinuation<TResponse>`
  - `MediatoidRootAttribute` (root marker for SourceGen)
- **Principles:**
  - Minimal and stable API surface.
  - Backwards compatibility is a priority.

### 1.2. `Mediatoid`

- **Purpose:**
  - Provides the runtime implementation of `ISender`.
  - Manages resolving handlers and behaviors via DI and composing the pipeline.
- **Contents (examples):**
  - `Mediator` class (`ISender` implementation)
  - `ServiceCollectionExtensions.AddMediatoid(...)`
  - Reflection-based handler and pipeline discovery
  - Deterministic pipeline compose (see `pipeline-semantics.md`)
- **Role:**
  - Always-present **reference behavior**.
  - Defines the expected semantics regardless of whether SourceGen is enabled.

### 1.3. `Mediatoid.Behaviors` (Optional)

- **Purpose:**
  - Provides commonly used out-of-the-box pipeline behaviors.
- **Contents (today):**
  - Logging behavior
  - Validation behavior (with `FluentValidation` integration)
- **Usage:**
  - Small projects may only reference `Mediatoid`.
  - When needed, add via `AddMediatoidBehaviors()`.

### 1.4. `Mediatoid.SourceGen` (Optional)

- **Purpose:**
  - Reduce runtime cost by moving handler discovery and selected pipeline pieces to build time.
- **v0.4.0 Status:**
  - Optimizes the handler terminal and integrates with the full pipeline semantics (Send/Publish/Stream).
  - The pipeline behavior chain is still composed at runtime; full compile-time pipeline generation remains a roadmap item.
- **Core Principle:**
  - Functional behavior must be **equivalent** to the `Mediatoid` runtime path.
  - SourceGen provides performance optimizations only; it must not change semantics.

---

## 2. Behavior Contracts (Current State)

This section summarizes the behavior that is guaranteed to users today. Details are described in the related documents (especially `pipeline-semantics.md`).

### 2.1. General Principles

- Single entry point: `ISender` (`Send`, `Publish`, `Stream`).
- `ValueTask`-based asynchronous flows (aiming to reduce allocations).
- Deterministic pipeline ordering.
- Exceptions are not wrapped; the original thrown type is preserved.

### 2.2. `Send` (Request/Response)

- For a `Send` call:
  - Exactly **one handler** registered for the given request type is invoked.
  - Configured pipeline behaviors are composed outer-to-inner.
- Pipeline scope, ordering and lifetime rules are defined in `pipeline-semantics.md`.

### 2.3. `Publish` (Notification)

- All registered notification handlers are invoked.
- Handlers are executed sequentially.
- In earlier versions there was no pipeline behavior for `Publish`; as of v0.4.0, `INotificationBehavior<TNotification>` is available and follows the same pipeline semantics as `Send`.

### 2.4. `Stream` (IAsyncEnumerable)

- A single stream handler is executed for `IStreamRequest<TItem>`.
- Cancellation and error semantics are controlled by the handler implementation.
- In earlier versions there was no pipeline behavior for `Stream`; as of v0.4.0, `IStreamBehavior<TRequest,TItem>` is available and follows the same pipeline semantics as `Send`.

---

## 3. Goals for the Source Generator (v0.4.x+)

### 3.1. Behavioral Equivalence

- **Goal:**
  - Behavior observed for a given request must be identical with or without SourceGen.
  - The same request must:
    - Produce the same handler call counts,
    - Use the same pipeline behavior order,
    - Produce the same exception/cancellation behavior.
- **Principle:**
  - SourceGen is only an accelerator; semantic differences are not acceptable.

### 3.2. Single Pipeline Guarantee (Send)

- **Goal:**
  - For a `Send` call, **either** the generated pipeline **or** the runtime pipeline executes.
  - It is not acceptable for both pipelines (generated + runtime) to run for the same request.
- **Outcome:**
  - When generated dispatch succeeds, the runtime compose path must not be executed for that call.

### 3.3. Full Pipeline Chain Generation

- Target for the v0.4.x+ series:
  - Generate the full handler + pipeline behavior chain at compile time.
- **Constraint:**
  - The generated pipeline chain must obey the **same order** and **same deduplication rules** as the runtime compose path.

---

## 4. Diagnostics and Observability

### 4.1. Pipeline Step Events

- The goal is to publish lightweight, opt-in events for pipeline steps via `MediatoidDiagnostics`.
- Use-cases:
  - Path validation in tests (generated vs runtime).
  - Benchmarking and performance analysis.
  - Advanced logging/metrics integration.

### 4.2. Stable Contract

- With v0.4.0, the core shape of the diagnostic API is defined and documented.
- Because this API has a high breaking impact, changes will be versioned carefully.

---

## 5. Test Strategy

### 5.1. Runtime Reference Tests

- Project: `Mediatoid.Tests` (and related sub-projects)
- Purpose:
  - Contract tests that treat runtime compose behavior as the reference.
  - Validate `Send` / `Publish` / `Stream` + pipeline semantics.

### 5.2. SourceGen Contract Tests

- Project: `Mediatoid.SourceGen.Tests`
- Purpose:
  - Ensure **behavioral equivalence** between the SourceGen and runtime paths.
- Approach:
  - Run the same scenario through both the generated path and the runtime compose path, then compare results.
  - Add diagnostic-based tests that validate the single-pipeline guarantee (for example, checking that behavior logs appear only once for a given request).

---

## 6. Summary Goals for v0.4.0 and Beyond

This section summarizes the main areas of focus for the v0.4.0 release and follow-up 0.4.x+ work.

1. **SourceGen Behavioral Equivalence**
  - Ensure functionally identical behavior between generated and runtime paths.
  - In particular for `Send`, align handler/pipeline call counts and ordering.

2. **Single Pipeline Guarantee**
  - Only one pipeline chain (generated or runtime) executes for a given `Send` call.

3. **Full Pipeline Chain Generation (First Version)**
  - First stable version of compile-time pipeline generation for handlers + behaviors.

4. **Diagnostic API Stabilization**
  - Lightweight and stable diagnostic contract for tracking pipeline steps.

5. **Contract-Based Test Suite**
  - Clear separation of runtime reference tests and SourceGen contract tests.
  - Base backwards compatibility and regression checks on these tests.

6. **Diagnostics-Backed Behavioral Equivalence (v0.4.x+)**
  - Publish behavior-step events through `MediatoidDiagnostics` for the SourceGen-generated pipeline chain as well.
  - Validate not only end results but step-by-step behavior equivalence (before/after/handler) between generated and runtime paths using diagnostic data.

This document will be updated in future releases and used as the single reference for architectural and roadmap decisions.

---

## 7. Developer Notes / Past Issues and Decisions

This section records issues and decisions encountered during the v0.3.x → v0.4.x transition, primarily as a reminder for maintainers.

### 7.1. SourceGen Fast-Path Initialization Bug (GeneratedDispatchCache)

- **Problem (v0.3.x):**
  - `GeneratedDispatchCache.EnsureInitialized()` was setting `_initialized = true` even when it failed to locate `Mediatoid.Generated.MediatoidGeneratedDispatch` on the first call.
  - In some test conditions (especially with multiple test projects and discovery phases), this meant the first call would arrive before the generated type was loaded into the AppDomain, effectively disabling the fast-path permanently.
- **Decision:**
  - `_initialized` is now set to `true` only when `MediatoidGeneratedDispatch.TryInvoke` is successfully located.
  - If the type is not loaded on the first attempt, `EnsureInitialized` is re-run on subsequent `Send` calls; once the type is found, the fast-path becomes active.
- **Impact:**
  - If the SourceGen package is not referenced or the generator does not emit any type, a "not found" check is performed on every `Send` call before falling back to the runtime compose path.
  - This is functionally correct, but its performance impact is worth measuring (especially in scenarios where SourceGen is never used).

### 7.2. Single Pipeline Guarantee and Test Rewrite

- **Problem:**
  - Some SourceGen tests appeared to exercise both the generated pipeline and the runtime pipeline for the same request (for example, seeing behavior logs twice).
  - Older tests contained expectations that no longer matched todays design goals (such as strict call-count assumptions).
- **Decision:**
  - A single-pipeline guarantee is required for `Send`: for a given call, either the generated pipeline or the runtime compose path runs, but not both.
  - During the 0.4.x series, SourceGen tests will be rewritten according to this contract. Legacy tests may be removed entirely and replaced with a new contract-test suite.
- **Action Note:**
  - Once SourceGen behavior is stabilized, in `Mediatoid.SourceGen.Tests`:
    - Add tests confirming behavioral equivalence (generated vs runtime).
    - Add diagnostic/pipeline-step-based tests for the single-pipeline guarantee.
    These tests should be redesigned from the ground up.

### 7.3. Evaluating Retry Strategy When SourceGen Is Absent

- **Status:**
  - `GeneratedDispatchCache.EnsureInitialized()` currently re-checks for the generated type on every `Send` call, and quietly falls back to the runtime compose path when it cannot be found.
  - This is functionally correct, but in applications that never reference the SourceGen package it may introduce unnecessary runtime overhead (repeated assembly scanning).
- **Planned Steps:**
  - Benchmark and compare two scenarios:
    1. SourceGen is **present** and the type exists (fast-path always available).
    2. SourceGen is **absent** or the generator does not emit any type (retry on every call + runtime compose fallback).
  - Based on the results, consider optimizations such as:
    - Caching a "no generated type" result after a certain number of failed attempts, stopping further retries.
    - Allowing applications to opt out of SourceGen lookups via a configuration switch.
- **Note:**
  - These optimizations must not change semantics; they should only improve performance characteristics.

