---
name: sessions
description: 'Plan and ship work in focused sessions of about 90 to 120 minutes that each end in a tagged, tested release. Use when starting a work session, framing a goal, deciding what is out of scope, running the Research, Plan, Implement, Review loop, doing a fit check before implementing, or closing a session with tests, tag, repo memory, and a log entry. Also use when sessions run long, scope drifts, work piles up unmerged, or each session starts cold.'
---

# Sessions

Session-based planning for AI-native engineering. The unit of work is one focused session, roughly 90 to 120 minutes, that ends in a coherent shipped artifact rather than a partially finished ticket.

## Overview

The bottleneck in AI-native engineering is no longer implementation speed. It is framing quality and context. Agile planning primitives were built for a world where a feature took weeks, so stories are sized in points, bounded by a sprint calendar, and accepted by demo. When shipping takes hours, that overhead stays while the delivery time collapses.

Replace the story with the session.

| Story                        | Session                                    |
|------------------------------|--------------------------------------------|
| Sized in points              | Sized in focus minutes                     |
| Bounded by sprint calendar   | Bounded by cognitive context               |
| Accepted via demo            | Accepted via tests, tag, and repo memory   |
| Not replayable               | Replayable through chat, git, and memory   |
| Backlog grows                | Compounds, since each session paves the next |

A session is what one engineer and an AI assistant can design, build, test, and ship before context drift sets in. It always ends with a coherent artifact: green tests, a fast-forward merge, a tag, and updated repo memory.

Sessions are the outer loop. RPI is the inner loop that runs inside one session. This skill owns the session. It composes with RPI rather than replacing it.

## When to Use

| Situation                                              | Use this skill              |
|--------------------------------------------------------|-----------------------------|
| Starting a block of focused build work                  | Yes, frame the session      |
| Work keeps carrying over without shipping               | Yes, the frame is too big   |
| Sessions run long or scope drifts mid-flight            | Yes, frame and fit check    |
| Each session restarts cold with no continuity           | Yes, the close ritual       |
| Typo fix, log line, refactor under about 50 lines       | No, just do the work        |
| Open-ended exploration with no shippable outcome        | No, run a spike instead     |

## Prerequisites

* A git repository where you can merge and tag. Sessions end in a dot-release.
* A test command that gives a clear green or red signal.
* A repo memory file that your AI reads, such as `AGENTS.md`, `CLAUDE.md`, or `.github/copilot-instructions.md`.
* A session log. Copy [assets/session-log-template.md](assets/session-log-template.md) into the repo, commonly at `session-log.md`.
* A tracking directory for inner-loop artifacts, conventionally `.copilot-tracking/`.

## Quick Start

A session has three beats. Plan all of them and skip none.

```text
Frame    (2 min)   goal, out of scope, failure condition
  RPI    (bulk)    research -> plan -> FIT CHECK -> implement -> review
Close    (5 min)   green tests, FF-merge, tag, repo memory, log paragraph
```

One session is about one RPI cycle. Needing more than one cycle usually means the work is two sessions.

## The Frame

Before starting, write down answers to three questions. Two minutes.

1. What does done look like for this session?
2. What is explicitly out of scope?
3. What one thing would make this session a failure?

If you cannot answer all three, the session is not ready to start. Record the answers in the session log before any other work, and write the start time at that moment.

A good failure condition is specific and checkable. "Schema invented rather than inferred from the data files" is a failure condition. "Bad quality" is not.

### Who Frames the Session

A solo engineer frames their own session. When a product manager is involved, framing is joint work: the PM brings the outcome, the constraints, and what must not be built, while the engineer brings feasibility and names the cut. The value is in the conversation, much as it is in t-shirt sizing, not in the artifact it produces.

Keep joint framing to the three questions and the two minutes. Co-framing without reintroducing ticket overhead is an unresolved question in the source methodology, and a framing meeting that grows past a few minutes has become the ceremony this model replaces.

## The One Rule During a Session

When you feel the urge to pull a thread outside the frame, write it in the parking lot and stay in scope.

Context drift is the primary failure mode, and it is seductive because the tangent usually feels important in the moment. Capture drift moments in the session log as they happen. They are evidence about framing quality, not noise.

## The RPI Inner Loop

The bulk of a session runs the Research, Plan, Implement, Review loop. Each phase runs under a constraint that prevents the next one, and each ends in a durable artifact under `.copilot-tracking/`. Start a new chat between phases, so findings travel in files rather than in chat history.

Delegate the inner loop to the `rpi` skill, which owns the phase constraints, artifacts, entry points, and prompts. This skill owns what wraps it: the frame before Research, the fit check between Plan and Implement, and the close ritual after Review.

One session is about one RPI cycle. Needing more than one cycle usually means the work is two sessions.

Skip RPI for trivial work. The overhead exceeds the value on a typo fix.

## The Fit Check

Between Plan and Implement, spend two minutes:

1. Will this plan fit in 90 to 120 minutes of focused work? Honest gut, not optimistic.
2. If no, what is the smallest cut that keeps the session release-able? Name the items.
3. Decide: proceed, cut, or re-frame. Record the decision in the session log.

Never skip the fit check. It is the highest-leverage beat in the workflow.

A pair moves faster than one engineer, but not twice as fast. For a scope framed at 90 to 120 minutes, expect a pair to land closer to 60 to 90. Finishing early is a healthy result, not evidence the frame was too small, so close the session properly rather than pulling in more work.

This is the cheapest moment to cut scope. A deleted bullet costs nothing, while abandoned implementation work costs the rest of the session. It is also the only moment with real data: the frame was a guess, the plan is evidence. Over time, if the fit check keeps cutting the same item, that item stops appearing in frames, which makes the check a calibration loop on framing quality.

## The Close Ritual

Every session ends the same way:

1. Green tests.
2. Fast-forward merge, for example `gh pr merge --rebase --delete-branch`.
3. Tag the dot-release, for example `git tag 0.2.0 && git push origin 0.2.0`.
4. Update repo memory with decisions the next session needs.
5. Write one paragraph in the session log: what you built, what you decided, where the next session starts.
6. Append one bullet to `RETRO.md` covering what surprised you and what you would do differently.
7. Write the end time now, not later.

That paragraph is the compounding mechanism. Without it, every session starts cold.

Write the start time when the frame is finished and the end time during the close, both in the moment rather than from memory. As a cross-check during the close, compare against git: the first commit on the session branch is roughly the start, and the merge or tag timestamp is roughly the end. If they disagree by more than a few minutes, fix the log while you still remember.

Even a small tag counts. A session that scaffolds a build and tags `0.0.1` closed correctly.

## The Session Log

The log is the feedback loop that makes the practice intentional rather than accidental. Each entry records the frame, drift moments, close checklist, summary paragraph, and three health signals:

* Framing quality, rated 1 to 5. Did the frame hold?
* Drift, yes or no. Did you leave scope?
* Close complete, yes or no. Did you finish the ritual?

By session five there is enough data to see personal patterns: where framing is weak, where drift happens, where the close gets skipped. Use [assets/session-log-template.md](assets/session-log-template.md) and run a mid-arc reflection at session five.

## Arcs

Work larger than one session becomes an arc, not an epic. An arc is roughly 10 to 12 sessions with explicit memory checkpoints between them. Frame it, time-box it, ship something real, then write up what happened.

Estimate in sessions to first demo and sessions to ship rather than in points. A mis-framed session costs one session, while a mis-estimated story used to cost a sprint. The blast radius is smaller and the correction is faster.

## When to Skip

* Skip RPI for trivial tasks such as typo fixes, log statements, and refactors under about 50 lines.
* Skip the formal frame for spike work where the goal is genuinely to see what is possible in 30 minutes. Spikes are exploration, not sessions. Convert spike output into a frame for the next session.
* Never skip the fit check once a plan exists.

## Troubleshooting

| Symptom                                          | Probable cause                                          | Fix                                                    |
|--------------------------------------------------|---------------------------------------------------------|--------------------------------------------------------|
| Session ran past 120 minutes                     | Frame was wrong, or the fit check was skipped           | Close short next time and re-frame                     |
| Session ended with no tag                        | Goal was too big and no release-able cut existed        | Smaller frame next session                             |
| Next session starts cold                         | Close paragraph was skipped or too vague                | Be specific in the next-session starter                |
| AI wrote code against APIs that do not exist     | Research was skipped or rushed                          | Force a research artifact before planning              |
| Plan drifts during implementation                | Plan was not specific enough, or the fit check said proceed when it should have said cut | Cut harder at the next fit check       |
| Work piles up unmerged across days               | Close ritual is not running                             | Treat the tag as the definition of done                |
| A phase produces vague or sloppy output          | The prompt was too broad                                | Constrain the phase and ask the assistant to do less   |
| Stuck mid-session                                | Drift, usually                                          | Re-read the frame, re-run the fit check, or close early |

## Credits

The session model and the context-first methodology are the work of [context-first](https://github.com/context-first/core), MIT licensed. The RPI inner loop is from Microsoft [HVE Core](https://github.com/microsoft/hve-core), also MIT licensed. Credit Microsoft when referencing RPI.

> Brought to you by bartr/skills
