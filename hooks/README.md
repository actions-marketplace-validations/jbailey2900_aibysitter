# Hooks

- `pre-commit`: Git pre-commit hook. Lints the staged content of rules files with `aibysitter lint`; blocks the commit on a failed threshold (default `--fail-on-error`, override with `AIBYSITTER_HOOK_ARGS`).
- `claude-code-settings.json`: Claude Code `PostToolUse` hook. Merge into `.claude/settings.json`. Runs `aibysitter hook claude-code` after each edit; Error and Warning findings in a rules file go back to Claude.

Install and options: https://aibysitting.net/Hooks
