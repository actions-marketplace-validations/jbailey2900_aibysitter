// Lint page: runs the browser engine on submit and renders the same markup as Lint.cshtml.
// Invalid input, or any engine error, falls through to the server post.
import { analyze, score, ruleInfo, formatName } from "./lint-engine.mjs";

const MAX_LENGTH = 100000;

function el(tag, attrs, ...children) {
  const node = document.createElement(tag);
  for (const [name, value] of Object.entries(attrs ?? {})) node.setAttribute(name, value);
  for (const child of children) node.append(child);
  return node;
}

const ruleLink = (id) => el("a", { href: "/Notes/" + encodeURIComponent(id) }, id);
const row = (...cells) => el("tr", null, ...cells.map((c) => el("td", null, ...[].concat(c))));
const head = (...names) => el("thead", null, el("tr", null, ...names.map((n) => el("th", null, n))));

function scorePanel(s) {
  const section = el("section", { class: "score grade-" + s.grade.toLowerCase() }, el("h2", null, "Score"),
    el("p", { class: "score-value" }, `${s.value} / 100 — ${s.grade}`));
  if (s.deductionsByRule.length === 0) {
    section.append(el("p", null, "No deductions."));
    return section;
  }
  const table = el("table", null, head("Rule", "Deduction"),
    el("tbody", null, ...s.deductionsByRule.map((d) => row(ruleLink(d.ruleId), "−" + d.points))));
  const capped = s.deductionsBySeverity.filter((d) => d.applied < d.total);
  if (capped.length > 0) {
    table.append(el("tfoot", null, ...capped.map((d) => row(d.severity + " cap", `−${d.applied} (of −${d.total})`))));
  }
  section.append(table);
  return section;
}

function findingsSection(findings) {
  const nodes = [el("h2", null, `Findings (${findings.length})`)];
  if (findings.length === 0) return [...nodes, el("p", null, "No findings.")];
  const rows = findings.map((f) => {
    const rule = ruleInfo(f.ruleId);
    const tr = row(String(f.line), [ruleLink(f.ruleId), " " + rule.title], rule.severity, f.message, f.fixHint);
    tr.setAttribute("class", "sev-" + rule.severity.toLowerCase());
    return tr;
  });
  return [...nodes, el("table", { class: "findings stack" }, head("Line", "Rule", "Severity", "Message", "Fix"), el("tbody", null, ...rows))];
}

function suppressedSection(suppressed) {
  if (suppressed.length === 0) return [];
  const note = el("p", { class: "note" }, "Matched by an ", el("code", null, "aibysitter-disable"), " comment. Not scored.");
  const rows = suppressed.map((f) => row(String(f.line), [ruleLink(f.ruleId), " " + ruleInfo(f.ruleId).title], f.message));
  return [el("h2", null, `Suppressed (${suppressed.length})`), note,
    el("table", { class: "findings stack" }, head("Line", "Rule", "Message"), el("tbody", null, ...rows))];
}

function render(container, selected, result) {
  const detected = selected === "Auto" ? " (detected)" : "";
  container.replaceChildren(
    el("p", { class: "note" }, `Linted as ${formatName(result.format)}${detected}.`),
    el("p", { class: "note" }, "Linted in your browser. The text was not sent."),
    scorePanel(score(result.findings)),
    ...findingsSection(result.findings),
    ...suppressedSection(result.suppressed));
}

function onSubmit(event, form, container) {
  const text = form.elements.RulesText.value;
  // The browser posts line breaks as CRLF; count them that way so the limit matches the server.
  if (text.trim().length === 0 || text.replace(/\r?\n/g, "\r\n").length > MAX_LENGTH) return;
  try {
    const selected = form.elements.Format.value;
    const result = analyze(text, selected);
    event.preventDefault();
    render(container, selected, result);
  } catch (error) {
    console.error("Browser lint failed; using the server.", error);
  }
}

const form = document.getElementById("lint-form");
const container = document.getElementById("lint-results");
if (form && container) form.addEventListener("submit", (event) => onSubmit(event, form, container));
