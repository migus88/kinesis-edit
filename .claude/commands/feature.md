---
description: Drive a feature end-to-end from a GitHub issue ID or description — research, clarify, update/create the issue, implement in an isolated git worktree, open a PR
argument-hint: <issue-number | issue-url | feature description>
---

# /feature — issue to PR, end to end

Input: `$ARGUMENTS` — either a GitHub issue reference (`123`, `#123`, or an issue URL) or a free-text feature description. If empty, ask the user what to build before doing anything else.

## Operating principles

- **Worktree isolation.** All implementation happens in a dedicated git worktree under `.claude/worktrees/`, branched from `origin/main` (phase 5). The shared checkout is never switched or dirtied, so multiple `/feature` sessions can run concurrently.
- **Stay lean.** You are the orchestrator: hold decisions, plans, and diffs under review — not file contents. Delegate reading, exploring, and code-writing to subagents (Agent tool). Do not pull whole source files into your own context when a subagent summary will do.
- **Docs before source.** This repo is documented agent-first: subagents should read `docs/app/` (and `specs/` for domain questions) before opening source files. See CLAUDE.md.
- **Ultracode.** If a system-reminder says ultracode is enabled for this session, orchestrate research and implementation with the Workflow tool as described in phase 6. Otherwise use individual subagents and skip the heavy machinery.
- **Documentation is part of the feature, not an afterthought.** Per CLAUDE.md, every change ships with its documentation: the module's agent-first doc in `docs/app/` (create it if the module is new), CLAUDE.md (new modules, changed commands), and README.md when user-facing behavior changes. A feature without its doc updates is incomplete and must not reach the PR phase.
- **Unit tests are part of the feature, not an afterthought.** Per CLAUDE.md's testing rules, every change ships with unit tests for the behavior it adds and maintains the tests of the code it touches. The full suite must pass before the PR phase — a feature with failing or missing tests must not reach it.

## Phase 1 — Sync

`git fetch --prune origin`. Do **not** check out or pull `main`, and do not require a clean shared checkout — the shared checkout is never touched; the feature branches from `origin/main` inside its own worktree in phase 5.

## Phase 2 — Resolve the input

- Issue reference → `gh issue view <n> --json number,title,body,labels,comments`. Its content is the starting spec. If the lookup fails, tell the user and ask whether the input was meant as a description instead.
- Free text → that is the starting spec; the issue gets created in phase 4.

## Phase 3 — Research and clarify

1. Delegate codebase research to an Explore agent: which modules/files the feature touches, existing patterns to follow, relevant constraints from `specs/`. You receive a summary, not file dumps.
2. Ask the user clarifying questions with AskUserQuestion — one batched call, max 4 questions, and only questions whose answers change the implementation (scope, behavior, UX trade-offs). Skip entirely if the issue is unambiguous.
3. Write a short spec: problem, approach, acceptance criteria, affected modules, required unit tests (what new behavior gets tested, which existing tests need updating), required doc updates (`docs/app/`, CLAUDE.md, README per repo policy).

## Phase 4 — GitHub issue

- Existing issue: post the refined spec as a comment (`gh issue comment <n> --body …`); use `gh issue edit` only if the original body is empty or wrong.
- No issue yet: `gh issue create --title "…" --body "…"` with the spec, then capture the new issue number.

Either way you now have an issue number `N` for the branch and PR.

## Phase 5 — Worktree

1. Determine the primary checkout's root: the first `worktree` line of `git worktree list --porcelain`. Do not use `--show-toplevel` — the session may already be inside another worktree.
2. Create the worktree and its branch from `origin/main`:
   `git worktree add "<root>/.claude/worktrees/feature-N-short-slug" -b feature/N-short-slug origin/main`
   If the branch or worktree already exists (a previous run for the same issue), don't fight it: ask the user whether to resume in the existing worktree as-is or delete and recreate it.
3. Switch the session into it: EnterWorktree with `path: "<root>/.claude/worktrees/feature-N-short-slug"`. Every subsequent phase — implementing, testing, committing, pushing — runs inside the worktree.

## Phase 6 — Implement

**Standard mode:** delegate to one or more general-purpose agents, giving each the spec plus the relevant research summary. Their instructions must include: follow `docs/guides/Coding Conventions.md`, keep classes small (SOLID), write unit tests for everything they implement and maintain the existing tests they touch, and update `docs/app/`, CLAUDE.md, and README as part of the change. Review their work through the diff — `git add -A && git diff HEAD` so newly created (untracked) files are included; iterate until the diff matches the spec.

**Ultracode mode:** orchestrate with the Workflow tool:

1. **Design** — only if the solution space is genuinely wide: 2–3 independent design attempts from different angles, a judge scores them, synthesize from the winner.
2. **Implement** — one agent per independent module; use worktree isolation only if agents would edit the same files concurrently.
3. **Adversarial review** — finder agents sweep the diff along separate dimensions (correctness vs. the spec, edge cases, conventions/doc compliance); every finding goes to refuting verifiers and only confirmed findings get fixed. Loop until a round produces nothing new.

## Phase 7 — Verify

1. **Unit-test gate (hard requirement).** Build and run the full unit-test suite (see CLAUDE.md for the current commands; `dotnet build` / `dotnet test` once the solution exists). Every test must pass before a PR is created — fix failures and rerun until the suite is green. Never open a PR with a failing suite; if a failure genuinely cannot be resolved, stop and report it to the user instead of proceeding.
2. **Test-coverage gate.** Confirm the diff includes the unit tests identified in phase 3: new tests for the behavior the feature adds, and updates to the existing tests of any code it touched. If tests are missing, go back and write them before opening the PR.
3. Documentation gate: confirm the diff includes the doc updates identified in phase 3 — `docs/app/` for every touched/created module, CLAUDE.md, and README.md where applicable. If any are missing, go back and write them before opening the PR.

## Phase 8 — Pull request

1. Commit with a clear imperative message and push: `git push -u origin HEAD`.
2. `gh pr create --title "…" --body "…"` (always pass the flags — the bare command drops into interactive mode). Body contains: what/why summary, notable decisions, test evidence, and `Closes #N`.

Finish by reporting to the user: the issue link, the PR link, the worktree path, and a 3–5 sentence summary of what was built and any open questions. Remind the user that the worktree stays until the PR is merged: they can tell you when it merges and you'll remove it (phase 9), or run `/clean-worktree` periodically to sweep up merged worktrees.

## Phase 9 — Post-merge cleanup (deferred)

The worktree outlives the PR creation. When the user reports the merge — possibly much later in the session:

1. Verify: `gh pr view feature/N-short-slug --json state` — proceed only if the state is `MERGED`; otherwise report the actual state and stop.
2. Leave the worktree first (a worktree cannot be removed while the session is inside it): ExitWorktree with `action: "keep"`.
3. `git worktree remove <path>` — if removal is blocked only by disposable untracked files (build output and the like), retry with `--force`.
4. `git branch -D feature/N-short-slug` (`-D` because squash/rebase merges are not ancestors of `main`; the merge was already verified in step 1), then `git fetch --prune`.

If the session ends before the merge, that's fine — `/clean-worktree` removes merged worktrees on its next run.
