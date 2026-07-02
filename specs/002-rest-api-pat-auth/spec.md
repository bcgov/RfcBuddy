# Feature Specification: RFC Retrieval REST API with Personal Access Tokens & User Administration

**Feature Branch**: `002-rest-api-pat-auth`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "I need to add a REST API to this project, so that downstream systems can submit keywords and get a list of relevant RFCs back as a JSON result. This API should be authenticated via PATs that logged-in users create first. PATs should have a max lifetime of 90 days. There's no need to separate the keywords into ministry/generic, but API consumers should be able to include keywords to ignore, which take precedence over ones to include. Since the PAT can identify a user, the existing change tracking should be part of the API results. Users and administrators need to be able to revoke PATs. The first user to log in after this change will automatically become an admin, and can then designate other users as admins. This will require a list of users - in that list, also display the last time this user was active (based on the existing change tracking mechanism). User directories with no activity for 400 days or longer should be deleted."

## Clarifications

### Session 2026-07-02

- Q: When a downstream system calls the API, should the response update the caller's change-tracking baseline (the record of which RFCs that user has already seen), and is that baseline shared with the web UI or kept separate? → A: Separate baseline for the API; each successful API call advances the API baseline only, leaving the web UI's change tracking untouched.
- Q: Which set of RFCs should the API filter against? → A: The same current-and-upcoming schedule the web app uses, plus the recently-completed RFCs archive (last 5 weeks), so callers see the same universe of changes a reviewer sees.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve relevant RFCs via authenticated API (Priority: P1)

A downstream system needs to know which upcoming and recently-completed changes
are relevant to it. It sends a request to the RFC retrieval API, authenticating
with a Personal Access Token (PAT) that a person created earlier. In the request
it supplies a list of keywords to include and, optionally, a list of keywords to
ignore. The API returns a JSON document listing every RFC that matches at least
one include keyword and matches none of the ignore keywords (ignore always wins).
Because the PAT identifies the person who created it, the response also carries
the same change-tracking information that person would see in the web app —
indicating which returned RFCs are new or have changed since that identity last
retrieved results through the API.

**Why this priority**: This is the core purpose of the feature. Without it, no
downstream integration is possible. It delivers standalone value the moment a
single PAT exists.

**Independent Test**: Create a PAT for a test user, call the API with a mix of
include and ignore keywords against a known RFC data set, and confirm the JSON
result contains exactly the RFCs that match an include keyword and no ignore
keyword, each annotated with its change-tracking status.

**Acceptance Scenarios**:

1. **Given** a valid, unexpired PAT and a request with include keywords, **When**
   the API is called, **Then** the response is a JSON list of RFCs matching at
   least one include keyword.
2. **Given** a request that includes both include and ignore keywords, **When**
   an RFC matches both an include and an ignore keyword, **Then** that RFC is
   excluded from the results (ignore takes precedence).
3. **Given** an RFC that has changed since the PAT's identity last called the API,
   **When** the API is called again, **Then** that RFC is annotated as changed in
   the response.
4. **Given** a missing, malformed, expired, or revoked PAT, **When** the API is
   called, **Then** the request is rejected with an authentication error and no
   RFC data is returned.
5. **Given** a request with no include keywords, **When** the API is called,
   **Then** the response is an empty RFC list (nothing matches).

---

### User Story 2 - Create and manage Personal Access Tokens (Priority: P1)

A logged-in user opens the application and creates a Personal Access Token so a
downstream system can call the API on their behalf. They give the token a
recognizable label and choose an expiry no further than 90 days out. The raw
token value is shown once, at creation time, for them to copy. They can see a
list of their existing tokens (label, creation date, expiry, last used) and
revoke any of them immediately.

**Why this priority**: The API in User Story 1 cannot be used until a token
exists. Token self-service is a prerequisite for any integration.

**Independent Test**: As a logged-in user, create a token, confirm the raw value
is displayed exactly once, confirm it then appears in the token list without the
raw value, use it against the API, revoke it, and confirm the API rejects it
afterward.

**Acceptance Scenarios**:

1. **Given** a logged-in user, **When** they create a token with a label and an
   expiry within 90 days, **Then** the token is created and its raw value is
   displayed exactly once.
2. **Given** a token creation request with an expiry beyond 90 days (or none),
   **When** it is submitted, **Then** the expiry is rejected or capped at the
   90-day maximum and the user is informed.
3. **Given** a user with existing tokens, **When** they view their token list,
   **Then** each token shows its label, creation date, expiry, and last-used
   time, but never the raw token value.
4. **Given** a user with an active token, **When** they revoke it, **Then**
   subsequent API calls using that token are rejected.
5. **Given** a token that has reached its expiry, **When** it is used, **Then**
   the API rejects it as expired.

---

### User Story 3 - Administer users and roles (Priority: P2)

The first person to log in after this feature is deployed automatically becomes an
administrator. As an administrator, they can open a user administration view that
lists every known user together with the last time that user was active (derived
from the existing change-tracking mechanism). From this list the administrator can
promote other users to administrator, and can revoke any Personal Access Token
belonging to any user.

**Why this priority**: Administration enables governance and off-boarding, but the
core API and token self-service (P1) can operate for an initial admin without it.
It is essential for real-world operation but not for the first working slice.

**Independent Test**: On a fresh deployment, log in as the first user and confirm
they are an administrator; open the user list and confirm each user shows a
last-active time; promote a second user and confirm they gain admin capabilities;
revoke another user's token and confirm that token stops working.

**Acceptance Scenarios**:

1. **Given** a deployment where no administrator yet exists, **When** the first
   user logs in, **Then** that user is granted administrator status.
2. **Given** an administrator viewing the user list, **When** the list renders,
   **Then** each user entry shows the user's identity and their last-active time.
3. **Given** an administrator, **When** they promote another user to
   administrator, **Then** that user gains administrator capabilities.
4. **Given** an administrator, **When** they revoke a Personal Access Token
   belonging to another user, **Then** that token is immediately rejected by the
   API.
5. **Given** a non-administrator user, **When** they attempt to access the user
   administration view or revoke another user's token, **Then** the action is
   denied.

---

### User Story 4 - Automatically remove inactive user data (Priority: P3)

To keep stored data tidy and minimize retained personal data, the system
automatically removes the stored data for any user who has shown no activity for
400 days or longer, using the existing change-tracking timestamp as the measure of
activity.

**Why this priority**: This is housekeeping and data-minimization. It improves the
system over time but is not required for the API or administration to function.

**Independent Test**: Seed a user directory whose last activity is older than 400
days and another that is recent, run the cleanup, and confirm only the stale
directory is removed.

**Acceptance Scenarios**:

1. **Given** a user whose last activity is 400 days ago or older, **When** the
   cleanup runs, **Then** that user's stored data is deleted.
2. **Given** a user whose last activity is more recent than 400 days, **When** the
   cleanup runs, **Then** that user's stored data is retained.
3. **Given** a user whose data is deleted, **When** any Personal Access Tokens
   they owned are consulted, **Then** those tokens no longer authenticate.

---

### Edge Cases

- **Ignore-only request**: A request with ignore keywords but no include keywords
  returns an empty list (nothing is included to begin with).
- **Overlapping keywords**: When the same keyword appears in both include and
  ignore lists, ignore precedence means matching RFCs are excluded.
- **Expired-but-not-revoked token**: A token past its expiry is treated the same
  as an invalid token and rejected.
- **Concurrent revocation**: A token revoked while a request is in flight is
  rejected on the next call; in-flight calls complete or fail cleanly without data
  leakage.
- **Last administrator**: The system must not allow the environment to reach a
  state with zero administrators (e.g., an administrator cannot remove their own
  admin status if they are the only one). *(See FR-024.)*
- **First-user race**: If two users log in near-simultaneously on a fresh
  deployment, exactly one becomes the first administrator.
- **Deleted user with active token**: When a user's data is removed by the 400-day
  cleanup, their tokens stop authenticating.
- **Cleanup vs. active token**: A user with a valid, unexpired token but no
  change-tracking activity for 400+ days is still subject to cleanup; expiry of
  their tokens follows from the data removal.
- **No change-tracking history**: A user or PAT identity that has never generated
  change-tracking data is treated as "everything is new" on first API retrieval.

## Requirements *(mandatory)*

### Functional Requirements

#### API retrieval

- **FR-001**: The system MUST expose a REST API endpoint that accepts a request
  containing a list of include keywords and an optional list of ignore keywords,
  and returns matching RFCs as a JSON result.
- **FR-002**: The API MUST include an RFC in the result when it matches at least
  one include keyword.
- **FR-003**: The API MUST exclude an RFC from the result when it matches any
  ignore keyword, and this exclusion MUST take precedence over inclusion.
- **FR-004**: The API MUST NOT require callers to separate keywords into
  ministry/general categories; a single flat include list and a single flat ignore
  list are used.
- **FR-005**: The API MUST filter against the same current-and-upcoming RFC
  schedule used by the web application, plus the recently-completed RFC archive
  (last 5 weeks), so API results reflect the same universe of changes a reviewer
  sees.
- **FR-006**: The API response MUST annotate each returned RFC with change-tracking
  information (e.g., new or changed) relative to the calling identity's API
  change-tracking baseline.
- **FR-007**: Each successful API retrieval MUST advance the calling identity's
  API change-tracking baseline, and this baseline MUST be kept separate from the
  web application's change-tracking baseline so the two do not interfere.
- **FR-008**: The API MUST return results in a documented, stable JSON structure
  suitable for machine consumption.

#### Authentication via PATs

- **FR-009**: The API MUST reject any request that does not present a valid,
  unexpired, non-revoked Personal Access Token, returning an authentication error
  without any RFC data.
- **FR-010**: A Personal Access Token MUST identify the user who created it, so
  that per-user change tracking applies to API calls made with that token.
- **FR-011**: Only authenticated, logged-in users MUST be able to create Personal
  Access Tokens.
- **FR-012**: The system MUST allow a user to set a token label and an expiry date
  at creation time.
- **FR-013**: The system MUST enforce a maximum token lifetime of 90 days;
  requests for a longer lifetime MUST be rejected or capped at 90 days, with the
  user informed of the applied expiry.
- **FR-014**: The raw token value MUST be shown to the creating user exactly once,
  at creation time, and MUST NOT be retrievable again afterward.
- **FR-015**: The stored representation of a token MUST NOT allow recovery of the
  raw token value (tokens are stored only in a non-reversible form).
- **FR-016**: A user MUST be able to view a list of their own tokens showing label,
  creation date, expiry, and last-used time, without exposing the raw value.
- **FR-017**: The system MUST record and display the last time each token was used
  to authenticate an API call.

#### Revocation

- **FR-018**: A user MUST be able to revoke any of their own tokens, after which
  the API MUST reject that token.
- **FR-019**: An administrator MUST be able to revoke any token belonging to any
  user, after which the API MUST reject that token.
- **FR-020**: Revocation MUST take effect for all subsequent API calls
  immediately.

#### Users and roles

- **FR-021**: The first user to log in after this feature is deployed MUST
  automatically be granted administrator status.
- **FR-022**: On a fresh deployment, exactly one user MUST become the first
  administrator even if multiple users log in near-simultaneously.
- **FR-023**: An administrator MUST be able to designate other users as
  administrators.
- **FR-024**: The system MUST prevent the environment from reaching a state with
  zero administrators.
- **FR-025**: The system MUST provide an administrator-only view listing all known
  users, each with the last time that user was active, derived from the existing
  change-tracking mechanism.
- **FR-026**: Access to user administration and cross-user token revocation MUST be
  restricted to administrators; non-administrators MUST be denied.

#### Data lifecycle

- **FR-027**: The system MUST automatically delete the stored data for any user
  that has had no activity for 400 days or longer, using the change-tracking
  timestamp as the measure of activity.
- **FR-028**: When a user's stored data is deleted, any Personal Access Tokens that
  user owned MUST no longer authenticate.

### Key Entities *(include if feature involves data)*

- **Personal Access Token (PAT)**: A credential a user creates to let a downstream
  system call the API on their behalf. Attributes: label, owning user identity,
  creation date, expiry date (max 90 days out), last-used time, revoked/active
  status, and a non-reversible stored form of the secret. The raw secret exists in
  cleartext only momentarily at creation.
- **User**: A person known to the system, identified via the existing
  authentication identity. Attributes: identity, administrator status, last-active
  time (from change tracking), and associated stored data (keywords, change-
  tracking history, tokens).
- **RFC (existing)**: A change record with keyword-relevant fields (asset tags,
  description, risk assessment, dates). Reused as the unit returned by the API.
- **API change-tracking baseline (per user)**: A record, separate from the web
  application's baseline, of which RFCs an identity has already seen via the API,
  used to annotate new/changed RFCs on subsequent calls.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A downstream system with a valid token can retrieve relevant RFCs in
  a single request and receive a well-formed JSON result.
- **SC-002**: 100% of API requests presenting an ignore keyword that also matches
  an include keyword exclude the affected RFCs (ignore precedence holds in every
  case).
- **SC-003**: 100% of API requests with a missing, malformed, expired, or revoked
  token are rejected with no RFC data returned.
- **SC-004**: A user can create a usable token and make a successful API call in
  under 5 minutes without assistance.
- **SC-005**: No token can be created with an effective lifetime exceeding 90 days.
- **SC-006**: A revoked token stops authenticating on the very next API call.
- **SC-007**: On a fresh deployment, exactly one administrator exists after the
  first login.
- **SC-008**: An administrator can view every user and each user's last-active time
  in one place.
- **SC-009**: Every user directory with 400+ days of inactivity is removed on the
  next cleanup run, and no directory with more recent activity is removed.
- **SC-010**: The raw value of a token is displayed at most once and is never
  retrievable afterward.

## Assumptions

- **Keyword source**: The include and ignore keywords used by the API come from the
  API request itself, not from the user's saved web-app keywords. This matches the
  intent that downstream systems supply their own keywords.
- **Keyword matching semantics**: The API reuses the application's existing keyword
  matching behavior (case-insensitive substring matching over the same RFC fields)
  so API and web results are consistent.
- **Multiple tokens**: A user may hold more than one active token at a time (e.g.,
  one per downstream integration).
- **Last-active source**: "Last active" for both the user list and the 400-day
  cleanup is derived from the existing change-tracking timestamp; API retrievals
  also count as activity for the API baseline.
- **Cleanup cadence**: The 400-day cleanup runs on a recurring schedule reusing the
  application's existing background update mechanism rather than requiring manual
  invocation.
- **Authentication reuse**: Interactive login for creating/managing tokens and for
  administration reuses the existing OIDC/Keycloak cookie-based authentication; the
  API itself is authenticated solely by PAT, not by interactive login.
- **Transport security**: API traffic is served over the same HTTPS/proxy setup as
  the existing web application.
- **Admin designation is additive**: Administrators can promote others; whether an
  administrator can also demote another administrator is allowed provided FR-024
  (at least one admin remains) is satisfied.
- **Storage approach**: User data, tokens, and change-tracking baselines continue
  to use the existing file-system-per-user storage model under the configured data
  folder.
