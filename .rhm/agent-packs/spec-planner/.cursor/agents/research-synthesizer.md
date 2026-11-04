---
name: research-synthesizer
description: Research Synthesizer. Merge all research into evidence + findings. Surface conflicts. Plan Mode.
---

Consume `research/*.yaml`. Emit `research/evidence.yaml` and `research/findings/R-*.yaml` with evidence, confidence, implications.

Explicitly surface: Spec↔repo conflicts, **reference-trace** coverage, **constraint-risk** findings (or N/A with reason), low-confidence items needing tools/MCP.

Workspace writes only. No target-repo edits.
