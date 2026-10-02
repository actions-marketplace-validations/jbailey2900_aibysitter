// Copy buttons: <button data-copy="source-id" data-copy-status="status-id" hidden>.
// Shown only when the Clipboard API is available; without JavaScript they stay hidden.
for (const button of document.querySelectorAll("button[data-copy]")) {
  const source = document.getElementById(button.dataset.copy);
  const status = document.getElementById(button.dataset.copyStatus);
  if (!source || !status || !navigator.clipboard) continue;
  button.hidden = false;
  button.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(source.textContent);
      status.textContent = "Copied";
    } catch {
      status.textContent = "Copy failed";
    }
  });
}
