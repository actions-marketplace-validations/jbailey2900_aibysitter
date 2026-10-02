// Share links: lint input carried in the URL fragment, which browsers do not send to the server.
// Fragment: "s=1." + base64url(deflate-raw(JSON {v, r, f, d, t})). Results are recomputed on load, never stored.
export const KEY = "s=";
export const VERSION = 1;

// Fragments longer than this are rejected before decoding.
const MAX_FRAGMENT = 400000;

function toBase64Url(bytes) {
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function fromBase64Url(text) {
  if (!/^[A-Za-z0-9_-]*$/.test(text)) throw new Error("base64");
  const binary = atob(text.replace(/-/g, "+").replace(/_/g, "/"));
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

async function collect(stream, maxBytes) {
  const reader = stream.getReader();
  const chunks = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.length;
    if (total > maxBytes) {
      await reader.cancel();
      throw new Error("too-large");
    }
    chunks.push(value);
  }
  const out = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    out.set(chunk, offset);
    offset += chunk.length;
  }
  return out;
}

const streamOf = (bytes) => new Blob([bytes]).stream();

// Length as the server counts it (line breaks as CRLF).
export const postedLength = (text) => text.replace(/\r?\n/g, "\r\n").length;

/** {rulesetVersion, format, disabled, text} -> "s=1.<payload>" */
export async function encode({ rulesetVersion, format, disabled, text }) {
  const json = JSON.stringify({ v: VERSION, r: rulesetVersion, f: format, d: disabled, t: text });
  const compressed = await collect(streamOf(new TextEncoder().encode(json)).pipeThrough(new CompressionStream("deflate-raw")), Infinity);
  return `${KEY}${VERSION}.${toBase64Url(compressed)}`;
}

/**
 * Fragment (with or without "#") -> {ok: true, state} or {ok: false, reason}.
 * options: {maxLength, formats: [names], ruleIds: [ids]}. Decompression stops once output could exceed maxLength.
 */
export async function decode(fragment, { maxLength, formats, ruleIds }) {
  const hash = fragment.startsWith("#") ? fragment.slice(1) : fragment;
  if (!hash.startsWith(KEY)) return { ok: false, reason: "absent" };
  if (hash.length > MAX_FRAGMENT) return { ok: false, reason: "too-large" };
  const body = hash.slice(KEY.length);
  const dot = body.indexOf(".");
  if (body.slice(0, dot) !== String(VERSION)) return { ok: false, reason: "version" };

  let data;
  try {
    // JSON escaping can grow a character to six bytes; anything beyond that cannot hold a valid text.
    const bytes = await collect(streamOf(fromBase64Url(body.slice(dot + 1))).pipeThrough(new DecompressionStream("deflate-raw")), maxLength * 6 + 1024);
    data = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes));
  } catch (error) {
    return { ok: false, reason: error.message === "too-large" ? "too-large" : "corrupt" };
  }

  const valid = data !== null && typeof data === "object"
    && data.v === VERSION
    && Number.isInteger(data.r)
    && typeof data.f === "string" && formats.includes(data.f)
    && Array.isArray(data.d) && data.d.every((id) => typeof id === "string" && ruleIds.includes(id))
    && typeof data.t === "string" && data.t.trim().length > 0;
  if (!valid) return { ok: false, reason: "invalid" };
  if (postedLength(data.t) > maxLength) return { ok: false, reason: "too-large" };
  return { ok: true, state: { rulesetVersion: data.r, format: data.f, disabled: [...new Set(data.d)].sort(), text: data.t } };
}
