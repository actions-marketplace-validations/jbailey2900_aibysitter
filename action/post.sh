#!/usr/bin/env bash
# Posts the lint report as a check run named "Aibysitter rules" (50 annotations per request).
# Falls back to workflow-command annotations when the token cannot create check runs.
# Inputs (env): REPORT (report.json), GH_TOKEN, GITHUB_REPOSITORY, GITHUB_SHA, GITHUB_EVENT_PATH.
set -uo pipefail

report="${REPORT:?}"
name="Aibysitter rules"
batch=50

head_sha="$GITHUB_SHA"
if [ -n "${GITHUB_EVENT_PATH:-}" ] && [ -f "$GITHUB_EVENT_PATH" ]; then
  pr_sha=$(jq -r '.pull_request.head.sha // empty' "$GITHUB_EVENT_PATH")
  [ -n "$pr_sha" ] && head_sha="$pr_sha"
fi

status=$(jq -r '.status' "$report")
count=$(jq '[.files[].findings | length] | add // 0' "$report")
files=$(jq '.files | length' "$report")
if [ "$status" = threshold ] || [ "$status" = error ]; then
  conclusion=failure
elif [ "$count" -gt 0 ]; then
  conclusion=neutral
else
  conclusion=success
fi

if [ "$files" -eq 0 ]; then
  title="No rules files found"
else
  title="$count finding$([ "$count" -eq 1 ] || echo s) in $files file$([ "$files" -eq 1 ] || echo s)"
fi
summary=$(jq -r '
  if (.files | length) == 0 then "No rules files found."
  else "| File | Score | Grade | Findings |\n|---|---|---|---|\n" + ([.files[] | "| `\(.file)` | \(.score) | \(.grade) | \(.findings | length) |"] | join("\n"))
  end' "$report")

# All annotations: Error -> failure, Warning -> warning, Info -> notice.
annotations=$(jq -c '[.files[] | .file as $f | .findings[] | {
    path: $f,
    start_line: ([.line, 1] | max),
    end_line: ([.line, 1] | max),
    annotation_level: ({"Error": "failure", "Warning": "warning"}[.severity] // "notice"),
    title: "\(.rule) (\(.severity))",
    message: "\(.message)\nFix: \(.fixHint)"
  }]' "$report")

output() {
  jq -n --arg title "$title" --arg summary "$summary" --argjson annotations "$1" \
    '{title: $title, summary: $summary, annotations: $annotations}'
}

fallback() {
  echo "Check run not created ($1); writing annotations to the log. GitHub shows up to 10 per type per step; the job summary has the full list."
  jq -r 'def data: gsub("%"; "%25") | gsub("\r"; "%0D") | gsub("\n"; "%0A");
    def prop: data | gsub(":"; "%3A") | gsub(","; "%2C");
    .[] | "::\({"failure": "error", "warning": "warning"}[.annotation_level] // "notice") file=\(.path | prop),line=\(.start_line),title=\(.title | prop)::\(.message | data)"' <<< "$annotations"
  exit 0
}

first=$(jq -c ".[0:$batch]" <<< "$annotations")
body=$(jq -n --arg name "$name" --arg sha "$head_sha" --arg conclusion "$conclusion" --argjson output "$(output "$first")" \
  '{name: $name, head_sha: $sha, status: "completed", conclusion: $conclusion, output: $output}')
if ! created=$(gh api --method POST "repos/$GITHUB_REPOSITORY/check-runs" --input - <<< "$body" 2>&1); then
  fallback "$(tr '\n' ' ' <<< "$created" | cut -c1-200)"
fi

id=$(jq -r '.id' <<< "$created")
total=$(jq 'length' <<< "$annotations")
for ((start = batch; start < total; start += batch)); do
  chunk=$(jq -c ".[$start:$((start + batch))]" <<< "$annotations")
  if ! gh api --method PATCH "repos/$GITHUB_REPOSITORY/check-runs/$id" --input - <<< "$(jq -n --argjson output "$(output "$chunk")" '{output: $output}')" > /dev/null; then
    echo "::warning title=aibysitter::Check run $id: annotations from $((start + 1)) on were not added."
    break
  fi
done

echo "Check run $id: $conclusion, $total annotation$([ "$total" -eq 1 ] || echo s)."
echo "check-run-id=$id" >> "${GITHUB_OUTPUT:-/dev/null}"
