---
title: "Assessment 2: Applied scenarios"
description: Scenario-based assessment of engineering judgment using Wharf's evidence
---

## Instructions

Answer all three scenarios. Each answer should state a decision, the reasoning
behind it, and the evidence you would cite or collect. There is no single
correct wording. Assessors score judgment and evidence use with the rubric in
the answer key.

## Scenario 1: The customer's 32-core VM

A customer has a 32-core Linux VM with Docker and wants to run the 14-task
enterprise benchmark at K=4 as quickly as possible. Their first run used
`N=64 P=32` on a cold image cache. About 40 minutes in, the run failed with
Docker storage write errors and lost all progress.

1. What is the most likely cause?
2. What `N` and `P` would you recommend for the next run, and why?
3. What should the customer record before deleting the failed run?
4. How would you show them that a lower `P` costs little time?

## Scenario 2: A surprising version result

A new Copilot CLI release scores 51/56 on the enterprise suite with three
P2P-error trials. The previous release scored 54/56 with zero P2P errors. A
product manager wants to announce that the new release is worse.

1. What can and cannot be concluded from these two runs?
2. What additional evidence would you want before making a claim?
3. How would you describe the result accurately today?

## Scenario 3: The release that passed every test

A release candidate passes all unit tests, the Release build, and formatting.
A reviewer notices that a new `--agent-timeout` flag appears correctly in the
dry-run plan.

1. Is the feature proven to work? Explain.
2. What test would show the setting reaches execution?
3. Name two other checks, beyond the test suite, that the release close
   should include.
