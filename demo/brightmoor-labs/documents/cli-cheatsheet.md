# Brightmoor CLI cheat sheet

## Everyday commands

Check what the agent is doing and when it last finished a round:

```bash
brightmoor status --json
```

See what the next round would change, without uploading or deleting anything:

```bash
brightmoor sync --dry-run
```

Read the log of the last two hours:

```bash
brightmoor logs --since 2h
```

## Pausing and resuming

Pause the agent for a fixed time, for example during a backup:

```bash
brightmoor agent pause --for 3h
brightmoor agent resume
```

A pause without `--for` lasts until you resume the agent yourself. While paused, the agent keeps recording changes and catches up when it resumes.

## Exit codes

```text
0  success
3  the configuration file has an error
4  the API key was rejected
```
