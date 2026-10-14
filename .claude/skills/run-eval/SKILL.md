---
name: run-eval
description: Run the Scrinium evaluation on both demo companies, commit-ready reports, and refresh the results table in the README from those reports.
disable-model-invocation: true
---

# Run the evaluation and refresh the README

1. Check that Ollama is up and has the models: `curl -s localhost:11434/api/tags`. The chat models are `gemma3:12b` and `qwen3-coder:30b`, embeddings `bge-m3`.
2. For each demo company in `demo/` and each chat model, run the Eval tool (see `CLAUDE.md`) with `--report demo/<company>/reports/<date>-<model>.md`. Runs are slow; run them in the background and wait for each to finish.
3. Print the results table from the reports:

   ```bash
   python3 .claude/skills/run-eval/scripts/summarize.py demo/*/reports/<date>-*.md
   ```

   Files with `first-run` in the name are skipped.
4. Replace the rows of the results table in the README section "Quality evaluation" with that output. Do not retype numbers.
5. Re-count the failure causes with the `eval-triager` agent and update the sentence that gives them, if the totals changed.
6. Show the diff. Commit only when asked.
