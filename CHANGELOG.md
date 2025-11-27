# Changelog
All notable changes to this project will be documented in this file. The format is based on SemVer.

## [0.4.0-preview.2] - 2025-11-27
### Added
- New documentation: `docs/architecture-and-roadmap.md` (architecture layers, behavior contracts and the v0.4.x roadmap).
- Contract-focused scenarios for SourceGen tests:
	- Smoke tests that verify the existence of generated types and the dispatch API.
	- Observation of pipeline behavior (before/after, short-circuit, ordering) across SourceGen + runtime paths.
	- New pipeline behavior contracts for Publish and Stream:
		- `INotificationBehavior<TNotification>` + `NotificationHandlerContinuation` (Publish).
		- `IStreamBehavior<TRequest,TItem>` + `StreamHandlerContinuation<TItem>` (Stream).
	- Runtime compose implementation for Publish and Stream (same semantics as Send):
		- 0-behavior fast path is preserved.
		- Inline compose for 1 and 2 behaviors, `Next()` pattern for more.
		- Short-circuit and cancellation semantics aligned with Send.

### Changed
- Reworked SourceGen fast-path inside `Mediator` (GeneratedDispatchCache):
	- If the generated type is not yet loaded in the AppDomain on the first call, the `_initialized` flag is not latched; subsequent `Send` calls retry.
	- Once `Mediatoid.Generated.MediatoidGeneratedDispatch.TryInvoke` is successfully located, the fast-path is permanently enabled.
- Simplified SourceGen tests by removing strict call-count expectations and validating via behavior log prefixes and diagnostic steps instead.
	- Added targeted runtime tests for Publish/Stream (before/after, short-circuit, fan-out and truncated stream scenarios).

### Notes
- This release is **preview**; `Mediatoid.SourceGen` will continue to evolve throughout v0.4.x.
- Upcoming items (summary):
	- Integrate `MediatoidDiagnostics` into the SourceGen-generated pipeline chain.
	- Add further contract tests to verify step-by-step behavioral equivalence between generated and runtime paths.
	- Stabilize full pipeline-chain generation (handler + behaviors).

## [0.4.0-preview.1] - 2025-11-27
### Added
- First 0.4.0 preview release; groundwork for Send pipeline semantics and SourceGen fast-path.

## [0.3.1] - 2025-11-22
### Changed
- Performance (Send): replaced nested closure pipeline compose with a single `Next()` continuation and single/two-behavior fast paths; reduced allocations for deeper pipelines.
- Performance (Invokers): removed `Expression.Compile`; switched to generic static-lambda-based handler/behavior/notification/stream invoker generation to reduce cold-start time and allocations.
- Publish: materialize handler enumeration into an array once and short-circuit early when empty.

### Benchmark Summary (before → after)
- Send Baseline Alloc: ~1832 B → ~1448 B
- Send Depth8 Alloc: ~2.85 KB → ~2.16 KB
- Send Cold Start: ~25.68 µs / 26.6 KB → ~23.96 µs / 25.48 KB

### Notes
- Public API did not change.
- Full pipeline-chain generation at build time (for further cold-start reduction) remains a v0.4.0 goal.
- See `docs/benchmarks.md` for detailed benchmarks.

## [0.3.0] - 2025-11-18
### Added
- Package: Mediatoid.Behaviors (LoggingBehavior, ValidationBehavior).
- Package: Mediatoid.SourceGen (source generator for handler discovery; significantly reduces runtime reflection for handler registrations).
- New tests: ValidationAggregateTests, ExceptionPassthroughTests, LoggingNoLoggerTests, StreamBasicTests.
- Validation: multiple validator errors are aggregated and thrown as `Mediatoid.Behaviors.ValidationException`.

### Changed
- LoggingBehavior: operates silently when no ILogger is available (parameterless ctor).
- DI registrations: behaviors are added as open-generic `IPipelineBehavior<,>` with transient lifetime.
- Handler invocation: uses the SourceGen registry when available; otherwise falls back to the existing delegate cache.

### Notes
- The pipeline chain (behavior compose) remains deterministic at runtime (assembly parameter order + FullName ordinal). In this version, SourceGen only optimizes handler invocation.
- Public API (Core) did not change.
- Next step (v0.4.0): generate the full pipeline invoker chain via the generator, design a manifest, and document benchmarks.

## [0.2.1] - 2025-11-16
### Changed
- Performance: cached reflection `MethodInfo` lookups for Send/Publish/Stream.
- Pipeline compose: removed LINQ `Reverse()` to reduce allocations; added a fast path when there are no behaviors.
- DI scanning: de-duplicated assemblies passed multiple times; on `ReflectionTypeLoadException`, continues with loadable types.

### Notes
- Public API did not change (backwards compatible).

## [0.2.0] - 2025-11-15
### Added
- Documented and locked Send pipeline semantics (v0.2.0).
- New documentation: `docs/pipeline-semantics.md`.
- Updated README "Pipeline Behaviors (Send)" section.

### Changed
- Explicitly documented pipeline order/compose rules and scope (Send only).
- Clarified in README that there was no pipeline for Publish/Stream at that time.

### Notes
- No public API changes in this release (backwards compatible).
- Performance improvements were deferred to subsequent patch/minor versions.

## [0.1.0] - 2025-11-12
### Added
- Initial release.
- Core contracts: IRequest, IRequestHandler, INotification, INotificationHandler, ISender, IPipelineBehavior, IStreamRequest, IStreamRequestHandler.
- Support for Send (Request/Response), Publish (Notification), Stream (IAsyncEnumerable).
- Pipeline behaviors (open generic) with deterministic execution order.
- DI integration: AddMediatoid(params Assembly[]).
- Basic reflection-based handler discovery.
- xUnit tests (Send/Publish/Stream/Pipeline).
