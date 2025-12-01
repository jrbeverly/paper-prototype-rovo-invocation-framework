# Confluence Automation Setup: Rovo Agent Routing

Documents how to configure Confluence Automation so that a labelled request
page is detected and routed to the appropriate Rovo agent, which writes its
result back as a child page.

This file is the source of truth for recreating the rule, since Confluence
Automation rules cannot be stored in version control.

---

## Prerequisites

Before configuring the rule:

1. A Confluence Cloud space exists and the `CONFLUENCE_SPACE_KEY` is known.
2. The namespace parent page identified by `CONFLUENCE_NAMESPACE_PAGE_ID` is
   in that space.
3. At least one Rovo agent is configured and accessible in the Atlassian
   workspace (see [Atlassian Rovo documentation][rovo-docs]).
4. The account that will own the Automation rule has **Space Admin** permission
   for the target space.

---

## Automation Rule: Route Request to Rovo Agent

Navigate to the Confluence space → **Space Settings** → **Automation** →
**Create rule**.

Name the rule: `Rovo Agent Routing`.

### Step 1 — Trigger: page labelled with `rovo-request`

| Setting | Value |
|---|---|
| Trigger type | **Page labeled** |
| Label | `rovo-request` |

This fires once each time the label is added to a page, including on initial
page creation.

---

### Step 2 — Claim the page (prevent duplicate processing)

Add action: **Edit page labels**.

| Setting | Value |
|---|---|
| Remove labels | `rovo-request` |
| Add labels | `rovo-processing` |

Removing `rovo-request` immediately prevents a second trigger from firing
if the rule is slow or retried.

---

### Step 3 — Route by agent label

Add action: **Branch rule / condition**.

Create one branch per supported Rovo agent. Each branch checks whether the
page carries the agent's routing label, then invokes that specific agent.

#### Branch: `rovo-agent-{name}` → agent invocation

Repeat the following pattern for each agent. Replace `{name}` with the
agent's identifier and `{Agent Display Name}` with the name shown in the
Rovo agent picker.

**Branch condition**

| Setting | Value |
|---|---|
| Condition type | **Page has label** |
| Label | `rovo-agent-{name}` |

**Branch action — Invoke Rovo agent**

Add action: **Ask Rovo** (or **Invoke Rovo Agent**, depending on your
Confluence version).

| Setting | Value |
|---|---|
| Agent | `{Agent Display Name}` |
| Prompt | See [Prompt Template](#prompt-template) below |

**Branch action — Create response child page**

Add action: **Create page**.

| Setting | Value |
|---|---|
| Title | `response` |
| Parent page | `{{page.title}}` (the request page — keeps the response as a child) |
| Space | `{{page.space.key}}` |
| Body | `{{rovo.response}}` |

> **Note:** The exact smart value for the Rovo agent output (`{{rovo.response}}`
> or similar) is confirmed in the action's smart value picker. Consult the
> picker when setting this field.

**Branch action — Label the response page**

Add action: **Edit page labels** (targeting the page created in the previous
step).

| Setting | Value |
|---|---|
| Target page | The page created above (use the smart value from the Create action) |
| Add labels | `rovo-response` |

#### Else branch (no matching agent label)

Add a final **Else** branch with no agent invocation. No action is needed
here; the branch exists to make unroutable requests visible. Optionally add
a **Log action** or update a label (`rovo-error`) for observability.

---

### Step 4 — Mark completion on the request page

After the branch rule, add action: **Edit page labels** (targeting the
original request page).

| Setting | Value |
|---|---|
| Remove labels | `rovo-processing` |
| Add labels | `rovo-complete` |

This runs regardless of which branch was taken.

---

### Step 5 — Enable and save

Set the rule scope to the space containing the namespace page. Enable the
rule.

---

## Prompt Template

Use the following prompt in the **Ask Rovo** action. It extracts the JSON
payload embedded in the request page body and instructs the agent to respond
in a structured format.

```
You are processing an automated request submitted through the Rovo invocation
framework.

The request page is titled: {{page.title}}

The page body contains:
{{page.body}}

The body includes a JSON payload inside a code block. Extract that JSON and
process the request according to its contents.

Respond with your output in a clear, structured format. Your response will be
written directly to a Confluence child page, so use plain text or Markdown —
avoid Confluence-specific markup.
```

Adjust the prompt per agent to reflect its specific capabilities or expected
output format.

---

## Adding a New Agent

To route to a new Rovo agent without changing the rule's core logic:

1. Open the `Rovo Agent Routing` rule.
2. Inside the branch rule (Step 3), add a new branch **before** the Else
   branch.
3. Set the condition: **Page has label** = `rovo-agent-{new-name}`.
4. Configure **Ask Rovo** with the new agent and a tailored prompt.
5. Add the **Create page** and **Edit labels** actions as described above.
6. Save and re-enable the rule.

Callers begin routing to the new agent by including `rovo-agent-{new-name}`
in the request page's labels. No other changes are required.

---

## Verification

After saving the rule, verify end-to-end behaviour:

1. **Create a test request page manually** in the Confluence space, as a
   child of the namespace page. Set the following labels:
   - `rovo-request`
   - `rovo-agent-{name}` (one of the configured agents)
   
   Set the page body to any valid JSON payload inside a code block (see
   [transport conventions](confluence-transport-conventions.md) for the
   exact format).

2. **Watch the labels** on the request page update from `rovo-request` →
   `rovo-processing` → `rovo-complete`. This confirms the rule fired and
   completed.

3. **Check for a child page** titled `response` under the request page. It
   should carry the `rovo-response` label and contain the agent's output.

4. **Run the smoke test** against the live environment to exercise the
   automated path end to end:

   ```bash
   # Set required env vars first (see confluence-transport-conventions.md)
   dotnet run --project src/ConfluenceClientSmoke -- {agent-name}
   ```

   The smoke test creates a request page and prints its labels and children.
   After the Automation rule runs, re-running `GetChildPagesAsync` for the
   same page ID should show the `response` child page.

---

## Label State Transitions

```
[created]
    │ rovo-request, rovo-agent-{name}
    ▼
[claimed by rule]
    │ rovo-processing, rovo-agent-{name}
    ▼
[complete]
    │ rovo-complete, rovo-agent-{name}
    ▼
[response child page exists]
    └─ title: response
       labels: rovo-response
```

---

## Notes

- **Rule scope**: Scope the rule to the specific space rather than site-wide
  to limit blast radius during development.
- **Duplicate processing**: Removing `rovo-request` in Step 2 before invoking
  the agent is the primary guard. The rule trigger fires on label addition, so
  removing the label prevents re-entry if the page is edited while the rule
  runs.
- **Agent availability**: If the Rovo agent is unavailable, the **Ask Rovo**
  action will fail and the rule will halt at that step. The `rovo-processing`
  label will remain on the page; monitor for stuck pages in that state.
- **Content format**: The `{{page.body}}` smart value returns the page content
  as plain text. The JSON payload in the code block is included verbatim. The
  prompt instructs the agent to extract it; adjust the prompt if the agent has
  difficulty parsing the surrounding markup.

[rovo-docs]: https://support.atlassian.com/rovo/
