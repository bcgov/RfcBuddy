# Project Constitution

## Core Principles

### I. Data Security (NON-NEGOTIABLE)

Sensitive data — including secrets, credentials, API keys, personally
identifiable information, and proprietary content — MUST NOT appear in
AI prompts, agent actions, logs, or version-controlled files.

- All secrets MUST be stored in environment variables or a dedicated
  secret manager; never hard-coded, committed to the repository, or
  passed as command-line arguments (they appear in process listings).
- AI-facing prompts and tool inputs MUST be reviewed for data leakage
  before execution. No raw user data may be interpolated into prompts
  without explicit sanitization.
- Logs MUST redact sensitive fields. Structured logging MUST mask any
  field that could contain secrets or PII.
- `.gitignore` and equivalent exclusion mechanisms MUST block secret
  files, environment files, and local configuration containing
  credentials.
- Any violation of this principle is a blocking defect regardless of
  feature priority.

### II. Simplicity First

Architecture MUST remain as simple as the current requirements demand.
Complexity is a cost; every abstraction MUST justify its existence.

- Apply YAGNI: do not build for hypothetical future requirements.
- Prefer flat structures over deep hierarchies.
- Minimize the number of projects, services, and abstractions. A new
  project or service requires explicit justification.
- Favor standard library and well-known dependencies over custom
  implementations.
- If a design choice can be explained in one sentence, it is
  appropriately simple.

### III. Adaptability Over Architecture

This project will evolve rapidly. Design decisions MUST favor ease of change
over structural permanence.

- Prefer loose coupling and narrow interfaces so modules can be
  replaced or restructured without cascading changes.
- Avoid deep dependency chains; keep the dependency graph shallow.
- Document decisions lightly — capture the *why*, not exhaustive
  *how*. Designs will shift; over-specified documentation becomes
  stale debt.
- Favor incremental delivery: small, shippable increments over
  large coordinated releases.
- When in doubt between a flexible approach and a "correct"
  architecture, choose the approach that is easier to change later.

### IV. Regression Safety

Every feature MUST be covered by sufficient tests to catch regressions
when the codebase changes.

- New features MUST include tests that exercise their acceptance
  criteria before the feature is considered complete.
- Bug fixes MUST include a regression test that reproduces the
  original defect.
- Test coverage MUST focus on behavior and contracts, not
  implementation details, so tests remain valid through refactors.
- Tests MUST run in CI on every change. A failing test is a blocking
  defect.
- Prefer fast, isolated unit tests. Use integration tests only
  where unit tests cannot verify cross-boundary behavior.

### V. Ease of Use

This project MUST be straightforward for its developer/user to operate and
extend.

- CLI interfaces MUST provide clear help text, meaningful error
  messages, and sensible defaults.
- Configuration MUST have safe defaults that work out of the box;
  override only when explicitly needed.
- Error messages MUST describe what went wrong, why, and suggest a
  corrective action when possible.
- Setup steps MUST be minimal: clone, install dependencies, run.
  Document any prerequisite in the project README.

## Security Requirements

These rules operationalize Principle I across all development
activities.

- **Dependency auditing**: Third-party packages MUST be pinned to
  known versions. Run dependency vulnerability scans in CI.
- **Access control**: Repository access, deployment credentials, and
  service accounts MUST follow least-privilege principles.
- **Compiler and analyzer warnings**: Treat all compiler warnings as
  errors. Static analysis findings MUST be resolved before merge.

## Development Workflow

- **Concise**: When executing commands, just execute them, don't narrate.
  When a decision requires human judgment, ask the user directly.
- **Branching**: One branch per feature or fix, named descriptively.
- **Commits**: Atomic commits with clear messages. Reference issue
  numbers where applicable.
- **Testing gate**: All tests MUST pass before a branch is merged.
  No `--no-verify` or equivalent bypasses.
- **Review**: Code changes SHOULD be reviewed for security
  implications, simplicity, and test coverage before merge.
- **CI**: A continuous-integration pipeline MUST run linting, build,
  and tests on every push. Failures block merge.
- **Documentation**: Update relevant docs (README, inline comments)
  alongside code changes (code-level docs; for architectural
  decisions, see Principle III). Do not create separate
  documentation tickets for trivial updates.

## Governance

This constitution is the highest-authority document for project
development practices. All design decisions, code reviews, and CI
gates MUST comply with the principles defined above.

- **Amendments**: Any change to this constitution MUST be documented
  with a version bump, a rationale, and an updated
  `LAST_AMENDED_DATE`. Use semantic versioning:
  - MAJOR: Principle removal or incompatible redefinition.
  - MINOR: New principle or materially expanded guidance.
  - PATCH: Clarifications, wording, or non-semantic refinements.
- **Compliance review**: Audit active features and CI configuration
  against these principles at each MAJOR version release. Log
  findings and track remediation.
- **Conflict resolution**: If a principle conflicts with another,
  Data Security (Principle I) takes precedence. Among remaining
  principles, favor the interpretation that keeps the solution
  simpler (Principle II).
- **Guidance file**: `.github/copilot-instructions.md` provides
  runtime development guidance (technology stack, security rules,
  shell commands, testing conventions) that supplements this
  constitution. The SpecKit plan loader is preserved at the top of
  that file.

**Version**: 1.0.1 | **Ratified**: 2026-05-13 | **Last Amended**: 2026-05-13
