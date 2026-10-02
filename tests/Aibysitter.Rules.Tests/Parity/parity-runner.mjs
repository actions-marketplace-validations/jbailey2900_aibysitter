// Runs the browser lint engine for RulesParityTests.
// Usage: node parity-runner.mjs <wwwroot/js directory> <lint|patterns>
// lint:     stdin [{name, text, format}]    -> stdout [{name, format, findings, suppressed, score}]
// patterns: stdin {patterns:[key], lines:[]} -> stdout {key: [[index, length], ...] per line, or {error}}
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { join } from "node:path";

const [jsDir, mode] = process.argv.slice(2);
const input = JSON.parse(readFileSync(0, "utf8"));
const engine = await import(pathToFileURL(join(jsDir, "lint-engine.mjs")).href);
const generated = (await import(pathToFileURL(join(jsDir, "generated", "rules-patterns.mjs")).href)).default;

function lint(items) {
  return items.map(({ name, text, format }) => {
    const result = engine.analyze(text, format);
    return { name, format: result.format, findings: result.findings, suppressed: result.suppressed, score: engine.score(result.findings) };
  });
}

function matchPositions(key, lines) {
  const p = generated.patterns[key];
  let re;
  try {
    re = new RegExp(p.source, p.flags + "g");
  } catch (e) {
    return { error: e.message };
  }
  return lines.map((line) => [...line.matchAll(re)].map((m) => [m.index, m[0].length]));
}

const output = mode === "patterns"
  ? Object.fromEntries(input.patterns.map((key) => [key, matchPositions(key, input.lines)]))
  : lint(input);
process.stdout.write(JSON.stringify(output));
