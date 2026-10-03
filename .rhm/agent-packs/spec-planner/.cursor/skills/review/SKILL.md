---
name: review
description: Critic + entropy + quality gate (Plan Mode). Stack-agnostic 10/10.
---

# /review

For the given `workspace/<task-id>/`:

1. Run `critic` (soft_shape, instructional_pin, reference_trace, constraint_risk)  
2. Produce/update `review/decision-entropy.yaml`  
3. On critic FAIL, run `gap-loop` (root-cause re-research; do not soft-patch)  
4. Run `quality-gate` and `scripts/check-handoff.py` + `validate-replit-bar.py` when critic PASS  

Never edit the target repository. Never assume a stack.
