// Lint page: runs the browser engine on submit and renders the same markup as Lint.cshtml.
// Invalid input, or any engine error, falls through to the server post.
// A share link (#s=...) is decoded and linted here on load; the server never sees the fragment.
import { analyze, score, ruleInfo, formatName, rulesetVersion } from "./lint-engine.mjs";
import { encode, decode, postedLength, KEY } from "./share-link.mjs";

const MAX_LENGTH = 100000;
const LONG_LINK = 8000;

function el(tag, attrs, ...children) {
  const node = document.createElement(tag);
  for (const [name, value] of Object.entries(attrs ?? {})) node.setAttribute(name, value);
  for (const child of children) node.append(child);
  return node;
}

const ruleLink = (id) => el("a", { href: "/Rules/" + encodeURIComponent(id) }, id);
const row = (...cells) => el("tr", null, ...cells.map((c) => el("td", null, ...[].concat(c))));
const head = (...names) => el("thead", null, el("tr", null, ...names.map((n) => el("th", null, n))));

function scorePanel(s) {
  const section = el("section", { class: "score grade-" + s.grade.toLowerCase() }, el("h2", null, "Aibysitter lint score"),
    el("p", { class: "score-value" }, `${s.value} / 100 — ${s.grade}`),
    el("p", { class: "note" }, "Ruleset ", el("a", { href: "/Rules/Changelog" }, "v" + rulesetVersion)));
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

function shareSection(state, findings) {
  const section = el("section", { class: "share", "aria-label": "Share link" });
  if (findings.some((f) => f.ruleId === "R009")) {
    section.append(el("p", { class: "note" }, "Share links are off while R009 finds a credential."));
    return section;
  }
  const button = el("button", { type: "button", class: "button" }, "Share link");
  button.addEventListener("click", async () => {
    const url = location.origin + location.pathname + "#" + await encode(state);
    const input = el("input", { type: "text", id: "share-url", readonly: "", value: url, "aria-label": "Share link", spellcheck: "false" });
    const status = el("span", { class: "note", "aria-live": "polite" });
    const copy = el("button", { type: "button", class: "button" }, "Copy");
    copy.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(url);
        status.textContent = "Copied";
      } catch {
        status.textContent = "Copy failed";
      }
    });
    copy.hidden = !navigator.clipboard;
    const length = `${url.length.toLocaleString("en-US")} characters.` + (url.length > LONG_LINK ? " Some apps cut links this long short." : "");
    section.replaceChildren(input,
      el("p", { class: "share-actions" }, copy, status),
      el("p", { class: "note" }, length, " The text is in the link after #; anyone with the link can read it."));
    input.addEventListener("focus", () => input.select());
  });
  section.append(button);
  return section;
}

function render(container, state, result, shared) {
  const detected = state.format === "Auto" ? " (detected)" : "";
  const rulesOff = state.disabled.length > 0 ? [el("p", { class: "note" }, `Rules off: ${state.disabled.join(", ")}.`)] : [];
  const source = shared
    ? [el("p", { class: "note" }, "Loaded from a share link. Linted in your browser. The text was not sent.")]
    : [el("p", { class: "note" }, "Linted in your browser. The text was not sent.")];
  const drift = shared && shared.rulesetVersion !== rulesetVersion
    ? [el("p", { class: "note" }, `Shared with ruleset v${shared.rulesetVersion}; results use v${rulesetVersion}.`)]
    : [];
  container.replaceChildren(
    el("p", { class: "note" }, `Linted as ${formatName(result.format)}${detected}.`),
    ...rulesOff,
    ...source,
    ...drift,
    scorePanel(score(result.findings)),
    ...findingsSection(result.findings),
    ...suppressedSection(result.suppressed),
    shareSection({ rulesetVersion, format: state.format, disabled: state.disabled, text: state.text }, result.findings));
}

const ruleBoxes = (form) => [...form.querySelectorAll('input[name="Enabled"]')];

function onSubmit(event, form, container) {
  const text = form.elements.RulesText.value;
  // The browser posts line breaks as CRLF; count them that way so the limit matches the server.
  if (text.trim().length === 0 || postedLength(text) > MAX_LENGTH) return;
  try {
    const format = form.elements.Format.value;
    const disabled = ruleBoxes(form).filter((box) => !box.checked).map((box) => box.value).sort();
    const result = analyze(text, format, disabled);
    event.preventDefault();
    render(container, { format, disabled, text }, result, null);
  } catch (error) {
    console.error("Browser lint failed; using the server.", error);
  }
}

async function loadShared(form, container) {
  if (!location.hash.startsWith("#" + KEY)) return;
  const decoded = await decode(location.hash, {
    maxLength: MAX_LENGTH,
    formats: [...form.elements.Format.options].map((o) => o.value),
    ruleIds: ruleBoxes(form).map((box) => box.value),
  });
  if (!decoded.ok) {
    container.replaceChildren(el("p", { class: "error" }, "This share link could not be read."));
    return;
  }
  const state = decoded.state;
  form.elements.RulesText.value = state.text;
  form.elements.Format.value = state.format;
  for (const box of ruleBoxes(form)) box.checked = !state.disabled.includes(box.value);
  if (state.disabled.length > 0) form.querySelector(".rules-picker")?.setAttribute("open", "");
  try {
    render(container, state, analyze(state.text, state.format, state.disabled), state);
  } catch (error) {
    console.error("Browser lint failed for the share link.", error);
    container.replaceChildren(el("p", { class: "error" }, "This share link could not be read."));
  }
}

// Editing the input makes a share link in the address bar stale; drop it.
function clearShared() {
  if (location.hash.startsWith("#" + KEY)) history.replaceState(null, "", location.pathname + location.search);
}

const form = document.getElementById("lint-form");
const container = document.getElementById("lint-results");
if (form && container) {
  form.addEventListener("submit", (event) => onSubmit(event, form, container));
  form.addEventListener("input", clearShared);
  form.addEventListener("change", clearShared);
  addEventListener("hashchange", () => loadShared(form, container));
  loadShared(form, container);
}
