# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Signing in:** `alex.turner@northwind-support.example` with `Dev_Only_Password1` (every seeded agent
uses that password; it is a local fixture). See [Task 3](#task-3-login-and-authentication).

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` · `GET /api/auth/me` | Sign in for a token / the signed in agent. Login is the only endpoint open without a token. |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. All filters are applied in the database. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket. Triage decides priority, due date and owner, and the response explains each in `triage`. Always `201`, even when unassigned. |
| `POST /api/tickets/{id}/escalate` · `GET /api/tickets/{id}/escalations` | Escalate one level with a reason / the history, newest first |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Every other endpoint needs `Authorization: Bearer <token>`.
Errors are `ProblemDetails`: `400` validation, `401` no valid token, `404` not found, `409` conflict or
business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Task 1: defect and filtering

**DEFECT-117, root cause.** The effect in `useTicketList` that fetches tickets only depended on
`search`, `page` and `pageSize`, so changing any other filter never refetched, the next search
then sent the old filters along with it. The request now depends on every filter, and only the
search text is debounced (300 ms). To verify: `npm test --workspace apps/web` (the regression
tests fail on the original hook), or watch the Network tab while changing Status.

**Filtering.** `TicketQueries.ApplyFilters` adds one `.Where` per supplied filter, before the
count and paging, so `totalCount` and `totalPages` match the filtered set. Search matches title,
reference or customer name with `LIKE` (wildcards escaped, trimmed, blank ignored). I checked the
EF command log: the same `WHERE` is in the count and the page query. A new filter is one more
`if` in `ApplyFilters`. Assumptions: `assignedAgentId` plus `unassignedOnly` returns nothing (AND);
no index added, since the filtered columns already have one and a leading `%` search can't use one.

## Task 2: triage, escalation and SLA

**Rules as understood.** One `TriageService` runs the same three rules for create and escalate:
priority (`PriorityPolicy`), window and due date (`SlaPolicy`), owner (`AgentSelector`). Priority is
the one requested, Medium by default, and Critical when the category's `ForcesCriticalPriority`
flag is set (no category name is in the code). Windows come from the validated `Sla` config, and
Premium gets half, never under 1 hour. An owner is active, specialised where required, strictly
under their limit, with the fewest open tickets (lowest id on a tie). Nobody eligible gives `201`,
unassigned, with the reason. Escalation raises one level (BR-7), restarts the window from now and
keeps the agent if still eligible. At risk means 25% or less of the window left, inclusive.

**Decisions.**
- `SlaStartedAtUtc`, set on create and on escalate, is where the window starts. Rejected: counting
  from creation, which marks a just escalated ticket At risk straight away.
- The unassigned reason has its own column and is cleared when an agent is assigned. Rejected:
  working it out again on read, when the team has changed.
- An agent at their limit keeps a ticket they already hold, as it is already in their count.
- Rules are small pure classes, with interfaces only at the repositories and the clock. Rejected:
  rules inside the handlers, which would be written twice.
- `slaStatus` is filtered in SQL, never in C#. A test requires it to agree with `SlaEvaluator`.
- `escalatedBy` is a typed name until Task 3 takes it from the token.

**Patterns.** Typed settings validated at startup (windows change without code); small policy
classes composed by one service (each tested alone, shared by create and escalate); one shared
predicate for the SQL SLA filter.

**Database.** Migration `AddEscalationsAndSlaClock` adds `TicketEscalations`, `Tickets.SlaStartedAtUtc`
(backfilled from `CreatedAtUtc`) and `Tickets.UnassignedReason`. One deliberate index, on
`(TicketId, EscalatedAtUtc desc)`: history is read per ticket, newest first, and it covers the
foreign key. EF also added two agent foreign key indexes that no query uses. `db/*.sql` carry the
new columns.

**Testing.** `npm test` runs xUnit (rules on a fixed clock with boundaries, queries on SQLite) and
Vitest. Not covered: SQL Server itself in the test run (the real `DATEDIFF` was checked by hand)
and browser end to end.

**Known limitations.** No concurrency control: two creates can pick the same agent past their
limit, and two escalations can both succeed. The SQL filter uses whole seconds, so a ticket within
a second of the 25% line may differ from its badge. Triage reasons come back on create only.

## Task 3: login and authentication

**Development login.** Alex Turner, `alex.turner@northwind-support.example`, password
`Dev_Only_Password1`. All six agents share that password, which is a local fixture and published on
purpose. Yuki Tanaka (`yuki.tanaka@northwind-support.example`) is inactive: she gets the same
"Invalid email or password" as a wrong password.

**How it works.** Credentials are a nullable `PasswordHash` column on `Agents`, not a users table:
agents are the only users and there are no roles, so a table would be a join for nothing. Hashing is
ASP.NET Identity's `PasswordHasher` (PBKDF2, HMAC-SHA512, a random salt per hash, 210,000 iterations)
behind an `IPasswordHasher` interface. The dev password is hashed at seed time, and a repeatable step
also gives a hash to agents seeded before sign in existed (the `db/*.sql` route cannot create hashes).
`POST /api/auth/login` returns a signed JWT (HS256), its expiry and the agent. The lifetime is 8 hours,
set by `Jwt:LifetimeMinutes` (1 to 1440). The key is `Jwt:SigningKey` or `Jwt__SigningKey`, at least 32
bytes and checked at startup. Development uses a fake placeholder in `appsettings.Development.json`,
`.env.example` lists the settings, and outside Development the API will not start without a real key.
A fallback policy requires a signed in agent on every endpoint, and only login is `[AllowAnonymous]`,
so an endpoint added later is protected by default. A test starts the real API, lists its endpoints and
checks each one. `escalatedBy` comes from the token and not the request. In the web app the token and
its expiry are kept in `localStorage` and attached by `api/client.ts`; an expired one is cleared on page
load, and any 401 clears the session and goes to `/login`, which returns to the page that was wanted.

**Decisions.**
- A wrong email, a wrong password and an inactive agent get the same 401 and message, and an unknown
  email is still checked against a dummy hash so the three take about the same time. Emails match
  regardless of case.
- `localStorage` over an `httpOnly` cookie, because the brief has the client attach the token and a
  cookie would need CSRF protection. The cost is that any script running on the page can read the
  token (XSS). An `httpOnly` cookie would be the production choice.
- `me` and escalation look the agent up again instead of trusting the token's name, so the history
  records the current name and a deactivated agent is refused.

**For production:** an `httpOnly` cookie or a backend for the frontend, the key in a secret store and
rotated, HTTPS only, lockout and rate limiting on login, and an agent check on every request.

**Known limitations.** A deactivated agent's token still opens ordinary endpoints until it expires
(`me`, escalation and new sign ins refuse them). No lockout or rate limiting, refresh tokens,
revocation or roles: every signed in agent can do everything.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |