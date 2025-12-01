# Vision: Rovo Invocation Framework

## Purpose

The purpose of this project is to provide a programmatic interface for invoking AI agents operating within Atlassian Rovo by treating Confluence as an asynchronous message bus.

While Rovo is primarily designed for interactive use within the Atlassian ecosystem, many engineering workflows require AI capabilities that can be incorporated into automated systems and larger orchestration pipelines.

This project explores how existing Confluence APIs, automation, and Rovo agents can be combined to expose a reusable asynchronous invocation model.

The objective is not to replace Rovo.

The objective is to integrate Rovo into programmable engineering workflows.

---

# Vision

The platform exposes a simple invocation interface.

A caller submits a request.

The framework creates a structured Confluence page representing that request.

Confluence Automation detects the request and routes it to the appropriate Rovo agent.

The agent performs its work.

Results are written back into Confluence.

The framework detects completion, retrieves the results, and returns them to the caller.

The entire interaction behaves like an asynchronous remote procedure call.

---

# Core Principles

## Confluence as Transport

Rather than treating Confluence purely as documentation, the project treats it as a durable communication medium.

Pages become messages.

Child pages become responses.

Labels and metadata become routing information.

Automation becomes the execution engine.

The platform demonstrates that existing collaboration tools can also function as lightweight orchestration infrastructure.

---

## Asynchronous Execution

AI tasks should be assumed to take an unpredictable amount of time.

The invocation model should therefore embrace asynchronous processing.

Requests are submitted.

Execution proceeds independently.

Results become available when processing completes.

The caller should not assume immediate completion.

---

## Agent Routing

Each request should identify the agent responsible for performing the work.

Routing should remain data-driven rather than hardcoded.

The framework should support introducing new agents without modifying the core orchestration model.

---

## Structured Results

The framework should define conventions for returning structured output.

Possible formats include:

* Markdown
* JSON
* YAML
* code blocks
* structured metadata

The invocation framework should be capable of determining the expected output format and extracting results automatically.

---

# Workflow Model

The lifecycle of an invocation should remain straightforward.

Typical stages include:

* request creation
* routing
* AI execution
* response generation
* completion detection
* result extraction
* caller notification

Each stage should remain independently replaceable.

---

# Polling and Completion

The platform should support reliable completion detection.

Possible mechanisms include:

* child page creation
* page labels
* page metadata
* status markers
* automation-generated indicators

The implementation should remain resilient to delayed execution.

---

# Conventions Over Configuration

The framework should rely on predictable naming conventions wherever practical.

Examples include:

* request namespaces
* page hierarchies
* routing prefixes
* output locations
* content formats

Well-defined conventions reduce the amount of configuration required to introduce new agent workflows.

---

# Integration

Although the immediate target is Rovo, the framework should remain loosely coupled.

Future integrations could include:

* additional AI systems
* external automation
* workflow engines
* documentation pipelines
* engineering tooling

Confluence should remain the transport mechanism rather than the processing engine.

---

# Experimentation

This project is intended to answer several practical questions.

Examples include:

* Can Confluence support asynchronous AI workflows?
* What conventions produce reliable execution?
* How should structured results be represented?
* How effectively can existing collaboration infrastructure serve as lightweight orchestration?

The implementation should encourage experimentation with different execution patterns.

---

# Technology Goals

The implementation should leverage:

* AWS Step Functions
* AWS Lambda
* Atlassian REST APIs
* Confluence Automation
* Atlassian Rovo

The framework should avoid maintaining continuously running infrastructure.

Execution should be event-driven wherever practical.

---

# Design Philosophy

The project embraces an unconventional architectural approach.

Rather than introducing another orchestration service, it demonstrates that existing collaboration platforms can be repurposed as programmable workflow infrastructure.

The framework should remain intentionally lightweight, relying on conventions and existing platform capabilities rather than custom services.

---

# Success Criteria

The project is successful if it demonstrates that:

* AI agents can be invoked programmatically through Confluence
* asynchronous request and response workflows remain reliable
* structured outputs can be extracted automatically
* new agents can be introduced with minimal additional infrastructure
* existing Atlassian capabilities can support reusable engineering workflows
* Confluence can function as a durable transport layer for AI orchestration

The final outcome should serve as a reference implementation for integrating Atlassian Rovo into automated engineering systems and demonstrate a novel approach to AI workflow orchestration using existing collaboration infrastructure.
