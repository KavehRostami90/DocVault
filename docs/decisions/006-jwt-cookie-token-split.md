# ADR-006 — JWT Access Token in sessionStorage + Refresh Token in httpOnly Cookie

**Status:** Accepted

---

## Context

Auth tokens must be stored somewhere accessible to the client. The three common options are:

1. **Both tokens in `localStorage`** — persists across tabs and browser restarts
2. **Both tokens in httpOnly cookies** — never accessible to JavaScript
3. **Access token in `sessionStorage` + refresh token in httpOnly cookie** — hybrid

## Decision

Store the **access token** (15-minute JWT) in `sessionStorage`. Store the **refresh token** (7-day opaque GUID) in an **httpOnly, SameSite=None, Secure cookie**.

## Reasoning

### Access token in sessionStorage

- `sessionStorage` is tab-isolated and cleared when the tab closes — the session ends naturally
- Not accessible to other tabs or windows (unlike `localStorage`) — limits blast radius of compromised content in another tab
- Short-lived (15 minutes) — even if stolen via XSS, the window is small
- Must be readable by JavaScript to attach as `Authorization: Bearer <token>` — httpOnly would prevent this

### Refresh token in httpOnly cookie

- Long-lived (7 days) — must be protected from XSS
- httpOnly means JavaScript cannot read it at all — XSS cannot exfiltrate the refresh token
- The browser sends it automatically on requests to the API — `client.ts` does not need to manage it explicitly
- `SameSite=None; Secure` is required for cross-origin requests (API on port 8081, UI on port 3000 in dev)

### Silent refresh flow

`AuthContext` schedules a silent refresh 30 seconds before token expiry. On any 401, `client.ts` calls `POST /auth/refresh` (cookie sent automatically), stores the new access token in `sessionStorage`, and retries the original request once — transparent to the user.

## Trade-offs

| Pro | Con |
|---|---|
| Refresh token fully protected from XSS | Access token in `sessionStorage` is readable by JS — XSS can steal it within the 15-min window |
| Session ends naturally when tab closes | CSRF on the refresh endpoint is possible (mitigated by `SameSite=None` + JSON body requirement) |
| No server-side session state for access tokens | Two token stores add complexity |
| Short TTL limits stolen token usefulness | Requires CORS `AllowCredentials()` for the cookie cross-origin |

## Refresh Token Rotation

Every `POST /auth/refresh` issues a **new refresh token** and immediately invalidates the old one. If a stolen refresh token is used, the legitimate user's next refresh fails — forcing re-login. The invalidated token is deleted from the `RefreshTokens` table.

## Alternatives Considered

| Alternative | Rejected because |
|---|---|
| **Both in localStorage** | Readable by any JS on the page — XSS permanently steals both tokens |
| **Both in httpOnly cookies** | Access token cannot be read by JS — cannot attach `Authorization` header without a BFF layer |
| **External IdP (Auth0, Azure AD B2C)** | Hard external dependency; increases cost and complexity for a self-hosted app |
| **Server-side sessions** | Requires sticky sessions or distributed session store; complicates horizontal scaling |
