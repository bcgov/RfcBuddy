# REST API Contract: RFC Retrieval

**Feature**: 002-rest-api-pat-auth | **Date**: 2026-07-02

This is the public, machine-facing contract for downstream systems. Interactive
token management and administration are UI flows (not part of this API contract) and
are covered by [service-contracts.md](service-contracts.md).

## Authentication

- **Scheme**: Personal Access Token presented as a bearer credential.
- **Header**: `Authorization: Bearer <token>`
- A request MUST be rejected with `401 Unauthorized` if the token is missing,
  malformed, expired, or revoked. No RFC data is returned on failure. (FR-009)
- The token identifies the owning user; per-user API change-tracking applies.
- All traffic is served over HTTPS (existing proxy/TLS setup).

## Endpoint: Search RFCs

`POST /api/v1/rfcs/search`

Returns RFCs relevant to the supplied keywords, drawn from the current-and-upcoming
schedule plus the last 5 weeks of completed RFCs, annotated with change status
relative to the caller's API baseline.

### Request

`Content-Type: application/json`

```json
{
  "includeKeywords": ["payments", "identity"],
  "ignoreKeywords": ["sandbox"]
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `includeKeywords` | string[] | yes | Keywords to match. Empty/absent ⇒ empty result. |
| `ignoreKeywords` | string[] | no | Keywords that exclude an RFC; takes precedence over include. |

Matching reuses the application's whole-word, case-insensitive matching over the RFC
fields (asset tags, description, risk, etc.). Ignore is evaluated first: any RFC
matching an ignore keyword is excluded even if it also matches an include keyword
(FR-002, FR-003).

### Response `200 OK`

`Content-Type: application/json`

```json
{
  "generatedAtUtc": "2026-07-02T15:04:05Z",
  "totalMatched": 2,
  "rfcs": [
    {
      "rfcNumber": "RFC0012345",
      "approvalStatus": "Approved",
      "platform": "OpenShift",
      "assetTags": "PAYMENTS-API",
      "startDateUtc": "2026-07-05T02:00:00Z",
      "endDateUtc": "2026-07-05T04:00:00Z",
      "description": "Deploy payments API v2",
      "riskAssessment": "Low",
      "changeStatus": "New"
    },
    {
      "rfcNumber": "RFC0012300",
      "approvalStatus": "Approved",
      "platform": "AWS",
      "assetTags": "IDENTITY-BROKER",
      "startDateUtc": "2026-06-20T01:00:00Z",
      "endDateUtc": "2026-06-20T03:00:00Z",
      "description": "Patch identity broker",
      "riskAssessment": "Medium",
      "changeStatus": "Changed"
    }
  ]
}
```

| Field | Type | Notes |
|-------|------|-------|
| `generatedAtUtc` | string (ISO-8601 UTC) | When the response was produced. |
| `totalMatched` | integer | Count of returned RFCs. |
| `rfcs` | array | Matching RFCs (may be empty). |
| `rfcs[].rfcNumber` | string | RFC identifier. |
| `rfcs[].approvalStatus` | string | Approval status. |
| `rfcs[].platform` | string | Platform. |
| `rfcs[].assetTags` | string | Affected asset tags. |
| `rfcs[].startDateUtc` | string (ISO-8601) | Change window start. |
| `rfcs[].endDateUtc` | string (ISO-8601) | Change window end. |
| `rfcs[].description` | string | Description. |
| `rfcs[].riskAssessment` | string | Risk assessment. |
| `rfcs[].changeStatus` | string enum | `New`, `Changed`, or `Unchanged` vs. the caller's API baseline. |

**Side effect**: A successful `200` advances the caller's API change-tracking
baseline (separate from the web UI baseline). Subsequent calls report `New`/`Changed`
only for RFCs that appeared or changed since this call (FR-006, FR-007).

### Error responses

| Status | When | Body |
|--------|------|------|
| `400 Bad Request` | Malformed JSON or wrong types. | Problem details (`application/problem+json`). |
| `401 Unauthorized` | Missing, malformed, expired, or revoked token. | Problem details; `WWW-Authenticate: Bearer`. No RFC data. |
| `415 Unsupported Media Type` | Non-JSON content type. | Problem details. |
| `500 Internal Server Error` | Unexpected failure (e.g., source unavailable). | Problem details; no sensitive info. |

Error bodies MUST NOT leak internal details, secrets, or PII.

### Edge-case behavior (from spec)

- `includeKeywords` empty/absent ⇒ `200` with `rfcs: []`, `totalMatched: 0`.
- A keyword appearing in both lists ⇒ matching RFCs excluded (ignore wins).
- Expired-but-not-revoked token ⇒ `401` (treated as invalid).
- Caller with no prior API baseline ⇒ all returned RFCs are `New`.

## Versioning & stability

- The path is versioned (`/api/v1/...`). Additive fields may be introduced without a
  version bump; breaking changes require a new version segment.
- The JSON shape above is the stable contract downstream systems may depend on.

## Non-goals

- No pagination (result sets are small at current scale); may be added additively
  later if needed.
- No rate limiting specified in this iteration (may be added at the proxy layer).
- Token creation/management is a UI concern, not exposed via this API.
