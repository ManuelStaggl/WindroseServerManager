# Phase 6: Application Registration - Context

**Gathered:** 2026-04-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Register Windrose Server Manager as an official Nexus Mods application and archive the returned metadata (slug, display name, logo, description) in the repo so that Phase 7 (SSO) has a stable slug to target and future maintainers can retrace the registration. The phase has an external-wait component — the Nexus moderation response — but all repo-side preparation (file scaffolding, metadata template, correspondence archive) can land before the response arrives.

Out of scope: any SSO code, any auth flow, any user-facing UI. That is Phase 7.

</domain>

<decisions>
## Implementation Decisions

### Archive location & format
- **Single source of truth:** `.planning/nexus-registration.md`
- Markdown file with **YAML frontmatter** for machine-readable fields (slug, status, dates, URLs) and a prose body for rationale and description copy
- Lives under `.planning/` (not `docs/`) — it is a project-meta artefact, not user-facing documentation
- Commit the file **pre-approval** with `status: submitted` and `slug: null` so Phase 7 planners see the placeholder and know where to look

### Status tracking
- Status lives in the **frontmatter of `nexus-registration.md`** — field name `status`, values `submitted | approved | rejected`
- `STATE.md` keeps only a one-line pointer: `Nexus registration: <status> — see .planning/nexus-registration.md`
- No separate status log file — frontmatter is enough; git history provides the audit trail
- When status changes to `approved`, also fill `slug`, `approved_date`, and any Nexus-provided URLs in the frontmatter

### Archived artefacts
- **Committed to repo** under `.planning/nexus-correspondence/`:
  - `2026-04-19-submit.md` — plain-text copy of the submission email that was sent to Nexus today (already sent; reconstruct from memory / sent-folder)
  - `YYYY-MM-DD-response.md` — Nexus moderator reply when it arrives (paste text; strip email headers except From/Date/Subject)
- **No screenshots, no `.eml` files** — plain markdown is grep-able, diff-able, and survives format changes
- **No PII scrubbing needed** — correspondence is between project author and Nexus staff; no third-party data

### Logo / icon asset
- Authoritative icon: `src/WindroseServerManager.App/Assets/app-256.png` (already in repo)
- If Nexus requires a larger size (commonly 512×512) during registration: upscale or re-export from source; document which file was submitted in `nexus-registration.md` under a `logo:` frontmatter field with the repo-relative path
- No new icon design — keep v1.0 brand continuity

### Claude's Discretion
- Exact YAML field schema for `nexus-registration.md` frontmatter (within the named fields above)
- Prose sections inside `nexus-registration.md` (description copy, rationale, how to update on re-approval)
- Folder layout details under `.planning/nexus-correspondence/` if more than two files accumulate
- Whether the submit-mail reconstruction happens as one task or folds into the metadata task

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & requirements
- `.planning/ROADMAP.md` §Phase 6 — phase goal, success criteria, dependency on Phase 7
- `.planning/REQUIREMENTS.md` §Application Registration — APP-REG-01, APP-REG-02 acceptance criteria
- `.planning/STATE.md` — current blocker note; will be updated by this phase

### Existing project artefacts
- `src/WindroseServerManager.App/Assets/app-256.png` — authoritative app icon for Nexus submission
- `README.md` — source for Nexus app description copy (already written in English for the public release)

No external specs — Nexus does not publish a formal application-registration schema. Fields are driven by what Nexus moderators request in the submit email thread.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/WindroseServerManager.App/Assets/app-256.png` — 256×256 PNG icon, already the app brand asset
- `README.md` — English product description exists and can be distilled into Nexus "application description" copy without rewriting

### Established Patterns
- `.planning/` directory is the project-meta home (STATE, ROADMAP, REQUIREMENTS, PROJECT all live here); new file joins that convention
- YAML frontmatter + Markdown body is not used elsewhere in `.planning/` yet, but is the natural fit for machine+human data

### Integration Points
- Phase 7 (SSO) will read `slug` from `.planning/nexus-registration.md` (or CONTEXT.md for Phase 7 will reference it) — that is the handoff surface
- STATE.md pointer line is the only cross-file coupling

</code_context>

<specifics>
## Specific Ideas

- The submission email was sent 2026-04-19 (today). Its content should be reconstructed into `.planning/nexus-correspondence/2026-04-19-submit.md` as part of this phase, not left in the author's mailbox.
- Frontmatter status is the same tri-state Nexus uses in their own moderation UI (submitted / approved / rejected) — match their vocabulary so future maintainers recognise the states.

</specifics>

<deferred>
## Deferred Ideas

- Refresh-token rotation / token-expiry handling — Out of scope (see REQUIREMENTS.md "Out of Scope")
- Any SSO code (button, listener, DPAPI storage) — Phase 7
- Any user-facing UI referencing the Nexus app — Phase 7 and later
- Automated re-submission workflow if rejected — handle ad-hoc if it happens; not worth designing upfront

</deferred>

---

*Phase: 06-application-registration*
*Context gathered: 2026-04-19*
