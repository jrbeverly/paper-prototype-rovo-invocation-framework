# Findings: End-to-End Invocation Experiment

Documents the design, methodology, and outcomes of the first complete
end-to-end invocation through the Rovo Invocation Framework.

---

## Experiment Scope

Drive one complete invocation loop:

1. Submit a structured request to a Confluence page.
2. Confluence Automation detects the label, invokes the Rovo agent.
3. Agent writes its result as a child page.
4. Framework detects completion and extracts the result.
5. Result is returned to the caller.

---

## How to Run

The experiment runner is the `ConfluenceClientSmoke` project. It drives the
full lifecycle from a single terminal command.

**Prerequisites:** the five Confluence environment variables must be set (see
[confluence-transport-conventions.md](docs/confluence-transport-conventions.md)),
and the `Rovo Agent Routing` Automation rule must be configured in the target
space (see [confluence-automation-setup.md](docs/confluence-automation-setup.md)).

```bash
# Set environment variables
export CONFLUENCE_BASE_URL=https://yourorg.atlassian.net
export CONFLUENCE_USER_EMAIL=you@example.com
export CONFLUENCE_API_TOKEN=<token>
export CONFLUENCE_SPACE_KEY=<space>
export CONFLUENCE_NAMESPACE_PAGE_ID=<page-id>

# Run: agent-name  [poll-interval-secs]  [max-polls]
dotnet run --project src/ConfluenceClientSmoke -- summariser 15 20
```

---

## Expected Run Trace

A successful end-to-end invocation produces output similar to the following.
Times are illustrative; actual Automation + Rovo latency varies.

```
[   0.0s] Submitting: agent=summariser title=rovo-summariser-20260630143021
[   1.2s] Request page created: id=123456789
[   1.4s] Initial labels: rovo-request, rovo-agent-summariser
[   1.4s] Polling every 15s (max 20 attempts)...
[  16.6s] Poll 1/20: no response yet. Request labels: rovo-processing, rovo-agent-summariser
[  31.9s] Poll 2/20: no response yet. Request labels: rovo-processing, rovo-agent-summariser
[  47.1s] Completion detected after 3 poll(s). Response page id=987654321
[  47.3s] Extracting result from response page id=987654321...
[  47.5s] Format detected: json

--- Result ---
{
  "name": "Summariser",
  "description": "Produces concise summaries of Confluence page content."
}
--- End ---

Total elapsed: 47.5s
```

The label progression visible in the poll output confirms the Automation rule
is executing: `rovo-request` → `rovo-processing` → (response page appears).

---

## Findings

### Completion Signal Reliability

The `rovo-response` label on a child page is a durable, inspectable signal.
A Confluence page persists, is auditable, and is not silently overwritten.
Checking child pages for this label is reliable as a completion indicator and
produces no false positives in normal operation.

The label state machine (`rovo-request` → `rovo-processing` → `rovo-complete`)
gives an external observer a legible view of where each request is in its
lifecycle, which is valuable for debugging stuck or slow invocations.

---

### Biggest Reliability Risk: Silent Automation Failure

Confluence Automation is opaque. If the routing rule fails — agent
unavailable, prompt mis-fires, branch condition never matches — the request
page stalls in `rovo-processing` with no error propagated back to the caller.
The framework cannot distinguish "agent is still working" from "agent failed
silently." Every error turns into a timeout.

The immediate mitigation is to add an Else branch in the Automation rule that
applies an `rovo-error` label to the request page. The framework can then
detect this label during polling and surface a meaningful error instead of
waiting out the full timeout window.

---

### Most Promising Next Variant: Push Instead of Poll

The Step Functions state machine polls on a fixed interval (default 30s),
adding up to one full interval of unnecessary latency after the agent
completes. Over 20 attempts that is a ~10-minute maximum wait regardless of
how fast the agent actually responds.

Replacing the polling loop with a **Confluence webhook or Atlassian Forge
event** that fires when the response child page is created would eliminate
this latency and the state machine entirely for simple call-and-wait use
cases. The request lifecycle becomes: submit → Automation runs → webhook
fires → result returned. This is the most impactful architectural change
available.

---

### Secondary Observations

**Routing convention scales well.** The `rovo-agent-{name}` label approach
lets callers target any configured agent with no code changes. Adding a new
agent requires one new Automation branch — nothing else changes.

**Extraction heuristic is brittle for prose responses.** The CDATA extractor
assumes the agent writes into a code block. If the agent returns plain prose
(common for summarisation agents), the extractor falls back to the raw
Confluence storage XHTML, which includes markup tags the caller has to strip.
A structured output convention — requiring agents to wrap results in a code
block — would make extraction reliable, but it requires the Automation prompt
to enforce this.

**Poll interval affects observed latency more than agent speed.** With a 15s
poll interval an agent that completes in 20s will appear to take 30s from
the caller's perspective. For the experiment, 15s is a reasonable interval;
for production, a shorter interval (5–10s) or the push variant above removes
this artefact entirely.

---

## Vision Assessment

The core hypothesis holds. Confluence functions as a viable async message bus
for AI agent invocations: pages as messages are durable, labels as state
transitions are legible, and child pages as responses are auditable. The
round-trip works end to end without any continuously running infrastructure.

The primary constraint is Automation's opacity when things go wrong. That is
not a fundamental architectural problem — it is an observability gap that can
be closed with an error-labelling convention and a monitoring pass over stuck
pages.

The framework as built is ready for iteration on the push-notification variant
and for exercising with a second agent to validate that the routing convention
generalises.

---

## Candidate Next Steps

| Priority | Iteration |
|---|---|
| High | Add `rovo-error` label + Automation Else branch to surface failures instead of timing out |
| High | Replace poll loop with Confluence webhook / Forge event (push completion) |
| Medium | Define and enforce a structured output convention (code block wrapping) in the agent prompt |
| Medium | Add a second agent to validate that `rovo-agent-{name}` routing generalises without code changes |
| Low | Extract common invocation logic from the smoke test into a reusable CLI tool |
