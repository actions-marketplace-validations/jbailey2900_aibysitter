#!/usr/bin/env bash
# Lints rules files with the aibysitter CLI and merges the per-file JSON into one report.
# Inputs (env): INPUT_FILES, INPUT_FAIL_ON_ERROR, INPUT_FAIL_BELOW, AIBYSITTER_CLI (command), AIBYSITTER_OUT (folder).
# Writes: $AIBYSITTER_OUT/report.json, step outputs (grade, score, findings, report, status), job summary.
# status: ok | threshold | error. This script exits 0; the action's last step fails on threshold or error.
set -uo pipefail

out="${AIBYSITTER_OUT:?}"
read -r -a cli <<< "${AIBYSITTER_CLI:?}"
mkdir -p "$out/files"
: "${GITHUB_OUTPUT:=/dev/null}"
: "${GITHUB_STEP_SUMMARY:=/dev/null}"

# Supported rules files, as RulesFormats.FromFileName recognises them (repository-relative paths).
discover() {
  git ls-files -z --cached --others --exclude-standard | tr '\0' '\n' | grep -E \
    -e '(^|/)(CLAUDE|AGENTS|GEMINI)\.md$' \
    -e '^\.cursorrules$' \
    -e '^\.windsurfrules$' \
    -e '^\.github/copilot-instructions\.md$' \
    -e '(^|/)\.cursor/rules/([^/]+/)*[A-Za-z0-9][A-Za-z0-9_.-]*\.mdc$' || true
}

# Explicit paths and globs, whitespace-separated. A token that matches nothing is passed through so the CLI reports it.
expand() {
  shopt -s globstar nullglob
  local token matches
  for token in $INPUT_FILES; do
    # shellcheck disable=SC2206
    matches=( $token )
    if [ ${#matches[@]} -eq 0 ]; then
      if [[ "$token" == *[\*\?\[]* ]]; then continue; fi
      printf '%s\n' "$token"
    else
      printf '%s\n' "${matches[@]}"
    fi
  done
  shopt -u globstar nullglob
}

normalize() {
  local path="$1"
  path="${path#"${GITHUB_WORKSPACE:-$PWD}"/}"
  path="${path#./}"
  printf '%s' "$path"
}

if [ -n "${INPUT_FILES//[[:space:]]/}" ]; then
  mapfile -t files < <(expand | while IFS= read -r f; do normalize "$f"; printf '\n'; done | awk 'NF && !seen[$0]++')
else
  mapfile -t files < <(discover | sort -u)
fi

args=()
[ "${INPUT_FAIL_ON_ERROR:-false}" = "true" ] && args+=(--fail-on-error)
[ -n "${INPUT_FAIL_BELOW:-}" ] && args+=(--fail-below "$INPUT_FAIL_BELOW")

status=ok
errors=()
i=0
for file in "${files[@]}"; do
  i=$((i + 1))
  json="$out/files/$i.json"
  "${cli[@]}" lint "$file" --json "${args[@]}" > "$json" 2> "$out/files/$i.err"
  code=$?
  case $code in
    0) ;;
    1) [ "$status" = ok ] && status=threshold ;;
    *) status=error; errors+=("$file: $(tr '\n' ' ' < "$out/files/$i.err")"); rm -f "$json" ;;
  esac
done

if compgen -G "$out/files/*.json" > /dev/null; then
  jq -s --arg status "$status" '{status: $status, files: .}' "$out"/files/*.json > "$out/report.json"
else
  jq -n --arg status "$status" '{status: $status, files: []}' > "$out/report.json"
fi

grade=$(jq -r '[.files[].grade] | max // ""' "$out/report.json")
score=$(jq -r '[.files[].score] | min // ""' "$out/report.json")
findings=$(jq -r '[.files[].findings | length] | add // 0' "$out/report.json")
{
  echo "grade=$grade"
  echo "score=$score"
  echo "findings=$findings"
  echo "report=$out/report.json"
  echo "status=$status"
} >> "$GITHUB_OUTPUT"

{
  echo "## Aibysitter rules lint"
  echo
  if [ ${#files[@]} -eq 0 ]; then
    echo "No rules files found."
  elif [ "$(jq '.files | length' "$out/report.json")" -gt 0 ]; then
    echo "| File | Score | Grade | Findings |"
    echo "|---|---|---|---|"
    jq -r '.files[] | "| `\(.file)` | \(.score) | \(.grade) | \(.findings | length) |"' "$out/report.json"
    if [ "$findings" -gt 0 ]; then
      echo
      echo "| Location | Severity | Rule | Message | Fix |"
      echo "|---|---|---|---|---|"
      jq -r '.files[] | .file as $f | .findings[] | "| `\($f):\(.line)` | \(.severity) | \(.rule) | \(.message | gsub("\\|"; "\\|")) | \(.fixHint | gsub("\\|"; "\\|")) |"' "$out/report.json"
    fi
  fi
  for e in "${errors[@]}"; do
    echo
    echo "Error: $e"
  done
} >> "$GITHUB_STEP_SUMMARY"

for e in "${errors[@]}"; do
  echo "::error title=aibysitter::$e"
done
exit 0
