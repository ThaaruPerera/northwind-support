# Approach

How I worked through each task. The README has the details of what was built and how to run it;
this file is about the order I did things in and why.

## How I worked

I planned each task before touching code: what the root cause or requirement was, which files it
touched, and how I would know it was done. Then I worked in small steps and committed each one on
its own, so a wrong turn could be undone without losing the rest. Tests came with the change they
cover, and I checked the result against the real API and database, not only the test run.

Where the spec left a choice open, I picked one, wrote it down in the README, and moved on.

## Task 1: defect and filtering

I started from the symptom (changing a filter did nothing) and followed it to the hook that fetches
tickets. Its effect only watched `search`, `page` and `pageSize`, so the other filters never
triggered a request. I wrote a failing test first, then fixed the dependencies, so the test proves
the fix. The first version of the fix failed the lint rule about setting state inside an effect.
Silencing the warning would have hidden the problem, so I restructured the hook instead.

The second half was the server. Filters were being ignored in the database query, so counts and
pages did not match the filtered list. I applied every filter in one place, before the count and
paging, so both queries share the same `WHERE`. I kept the defect fix and the filtering as separate
commits, as asked.

## Task 2: triage, escalation and SLA

I wanted the business rules to be easy to test, so I made them small pure classes (SLA windows,
priority, agent choice, triage, SLA evaluation) with no database access. The handlers only load
data, call the rules, and save. That let me test the boundaries (exactly at the limit, exactly 25%
left) against a fixed clock.

The main decisions:
- A separate `SlaStartedAtUtc` column, because counting from creation would mark a just escalated
  ticket as At risk immediately.
- The reason a ticket is unassigned is stored in its own column, so the screen shows what happened
  at the time even if the team changes later.
- Escalations are immutable history rows.
- The SLA filter runs in SQL, with a test that it agrees with the C# evaluator, so the list and the
  badge cannot drift apart.

One migration I wrote first gave old rows a wrong default date. I rewrote it to add the column as
nullable, fill it from `CreatedAtUtc`, then make it required.

## Task 3: login and authentication

I kept it as simple as the requirements allowed: a password hash on the existing `Agents` table
(there are no roles, so a separate users table adds nothing), ASP.NET Identity's hasher, and a
short lived signed token. Login gives the same answer and takes about the same time whether the
email is unknown, the password is wrong, or the agent is inactive.

Protection is the default. A fallback policy requires a signed in user everywhere, and a test walks
every route to confirm that only login and health are open. That way a new endpoint is protected
unless someone deliberately opens it. `escalatedBy` now comes from the token instead of the request.

On the frontend, the session is kept in `localStorage`, checked for expiry on load, and a 401 from
the API signs the user out and returns them to where they were after login. I chose `localStorage`
over an httpOnly cookie for simplicity and noted the trade-off in the README.

## Task 4: Docker

The goal was `docker compose up` giving a working, migrated, seeded API without a secret in the
image. I made migration and demo seeding switches that are on only in Development, built the image
in stages so the runtime image only holds the published app, and ran it as a non root user.
`appsettings.Development.json` is excluded from the image, and I listed the image contents to check.

The health endpoint runs a real `SELECT 1`, because EF's `CanConnect` returned true for an in memory
database. I ran the stack end to end, including starting it in Production mode without a signing key
to confirm that it refuses to start.

## What I would do next

Add locking so two people cannot take the same agent or escalate the same ticket at once, revoke
tokens for deactivated agents, and add login rate limiting. These are listed in the README under
Known limitations.
