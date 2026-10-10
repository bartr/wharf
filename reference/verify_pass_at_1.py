#!/usr/bin/env python3
"""Establish which aggregation reproduces DeepSWE's published PASS@1.

The leaderboard prints a single percentage per configuration and never states the
formula, so it has to be recovered by reconciliation. reference/ holds per-trial
outcomes (P/F/E) for claude-opus-5 [max], whose published figure is 74% +/- 4%.

Run: python3 reference/verify_pass_at_1.py
"""
import json
import os
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
REF = os.path.join(os.path.dirname(HERE), "reference",
                   "deepswe_v1_1_opus5_max_costs.json")
PUBLISHED = "74% +/- 4%   (claude-opus-5 [max], leaderboard column 'PASS@1')"

outc = json.load(open(REF))["outcomes"]
c = Counter(o for v in outc.values() for o in v)
n = sum(c.values())
P, F = c.get("P", 0), c.get("F", 0)

print(f"published : {PUBLISHED}")
print(f"data      : {len(outc)} tasks, {n} trials, outcomes {dict(c)}")
print()
print("candidate aggregations")
print(f"  all trials      P/(P+F+E) = {P}/{n} = {100.0*P/n:5.2f}%")
print(f"  errors excluded P/(P+F)   = {P}/{P+F} = {100.0*P/(P+F):5.2f}%")
macro = sum(v.count("P") / len(v) for v in outc.values()) / len(outc)
kept = [[o for o in v if o != "E"] for v in outc.values()]
kept = [v for v in kept if v]
macro_ne = sum(v.count("P") / len(v) for v in kept) / len(kept)
print(f"  macro, all trials         = {100.0*macro:5.2f}%")
print(f"  macro, errors excluded    = {100.0*macro_ne:5.2f}%")
print()
print("rejected candidates (task-level metrics -- none match the published figure)")
allpass = sum(1 for v in outc.values() if v.count("P") == len(v))
maj = sum(1 for v in outc.values() if v.count("P") * 2 > len(v))
any_ = sum(1 for v in outc.values() if v.count("P") > 0)
print(f"  every trial passed        = {allpass}/{len(outc)} = "
      f"{100.0*allpass/len(outc):5.2f}%")
print(f"  majority of trials passed = {maj}/{len(outc)} = {100.0*maj/len(outc):5.2f}%")
print(f"  any trial passed          = {any_}/{len(outc)} = {100.0*any_/len(outc):5.2f}%")
