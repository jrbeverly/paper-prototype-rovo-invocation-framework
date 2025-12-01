# Confluence Transport Conventions

Defines how Confluence pages act as the asynchronous message bus for Rovo agent invocations.

---

## Namespace

All request pages are created as children of a single designated parent page called the _request namespace_, identified by the `CONFLUENCE_NAMESPACE_PAGE_ID` environment variable.

---

## Request Page

When the caller submits an invocation, a new child page is created under the namespace.

| Property | Value |
|---|---|
| Title | Caller-supplied (e.g. `rovo-smoke-20260630120000`) |
| Labels | `rovo-request`, `rovo-agent-{agent-name}` |
| Body | Confluence storage format — agent name header + JSON payload in a code block |

### Body Format (Confluence storage XHTML)

```xml
<p><strong>Agent:</strong> {agent-name}</p>
<ac:structured-macro ac:name="code" ac:schema-version="1">
  <ac:parameter ac:name="language">json</ac:parameter>
  <ac:plain-text-body><![CDATA[{ ...payload... }]]></ac:plain-text-body>
</ac:structured-macro>
```

---

## Routing

Confluence Automation watches for pages carrying the `rovo-request` label. The `rovo-agent-{name}` label identifies the target Rovo agent. Automation invokes the agent and should update the label (e.g. replace `rovo-request` with `rovo-processing`) to prevent duplicate processing.

---

## Response

When the agent completes, a child page is created under the request page.

| Property | Value |
|---|---|
| Title | Automation-generated (e.g. `response`) |
| Labels | `rovo-response` |
| Body | Agent output (Markdown, JSON, or plain text) |

---

## Completion Detection

Poll `GET /wiki/rest/api/content/{requestPageId}/child/page` and check whether any child page carries the `rovo-response` label. The request is complete when such a child page exists.

---

## Environment Variables

| Variable | Description |
|---|---|
| `CONFLUENCE_BASE_URL` | Atlassian site base URL (e.g. `https://yourorg.atlassian.net`) |
| `CONFLUENCE_USER_EMAIL` | Email address used for basic auth |
| `CONFLUENCE_API_TOKEN` | Atlassian API token — **never commit** |
| `CONFLUENCE_SPACE_KEY` | Space key where request pages are created |
| `CONFLUENCE_NAMESPACE_PAGE_ID` | Page ID of the parent namespace page |
