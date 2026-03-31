# Contributing

## Overview

Thank you for your interest in contributing.

This project is a local-first transcription and speaker correction system focused on:

* accuracy
* usability
* deterministic behaviour
* human-in-the-loop workflows

Contributions should align with these principles.

---

## Contribution Principles

When contributing, aim to:

* Keep the system **local and deterministic**
* Prefer **simple, inspectable solutions** over complex abstractions
* Optimise for **human workflow efficiency**, not full automation
* Maintain clear separation between:

  * raw transcript data
  * correction overlay
  * projection/output

Avoid over-engineering unless there is clear long-term value.

---

## Workflow

### 1. Create an Issue

Before making changes:

* Create or reference an issue
* Clearly define scope and intent

---

### 2. Create a Branch

Use the following naming convention:

```bash
feature/<area>-<intent>
```

Examples:

* `feature/speaker-correction-workflow`
* `feature/review-navigation`

---

### 3. Implement Changes

* Keep changes **focused and minimal**
* Avoid unrelated refactoring
* Follow existing structure and naming conventions
* Prefer adding small services over large monolithic classes

---

### 4. Commit Changes

Use clear commit messages:

```text
Milestone X: short description of change
```

Example:

```text
Milestone 3: add CLI speaker correction workflow
```

---

### 5. Create a Pull Request

* Target the `dev` branch (not `main`)
* Fill in the PR template
* Clearly explain:

  * what changed
  * why it changed
  * any risks or assumptions

---

## Coding Guidelines

### General

* Use clear, descriptive names
* Keep methods small and focused
* Avoid unnecessary abstractions
* Prefer explicit logic over “clever” code

---

### Architecture

Maintain these boundaries:

* Raw data (transcription / diarization)
* Correction workspace (review model)
* Correction overlay (manual edits)
* Projection (final output)
* CLI interaction

Do not mix responsibilities across layers.

---

### Services

* Keep business logic in services, not CLI code
* Services should be:

  * focused
  * deterministic
  * testable (where practical)

---

## CLI Design Guidelines

* Keep interaction **simple and linear**
* Avoid complex menu systems
* Prioritise:

  * clarity
  * speed
  * minimal input friction

---

## Testing and Validation

Before submitting a PR:

* Ensure the solution builds:

```bash
dotnet build
```

* Run through the workflow manually:

  * transcription (or resume)
  * correction workflow
  * output generation

* Confirm:

  * no unintended behaviour changes
  * correction state behaves correctly
  * outputs remain consistent

---

## What to Avoid

* Large, unfocused changes
* Mixing refactoring with feature work
* Introducing heavy dependencies
* Changing output contracts without clear reason
* Mutating raw transcript data directly

---

## Discussion and Ideas

If you are unsure about an approach:

* open an issue
* describe the idea
* discuss before implementing

---

## Final Notes

This project values:

* clarity over cleverness
* usability over perfection
* incremental progress over big rewrites

Good contributions improve the workflow, not just the code.

---

## Code of Conduct

By participating in this project, you agree to follow the Code of Conduct.
