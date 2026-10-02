## Pages
- One page per file pair: `Pages/<Area>/<Name>.cshtml` and `<Name>.cshtml.cs`.
- Page models take dependencies through the primary constructor.
- Bind form input with `[BindProperty]` on a dedicated input record. Entities are never bound directly.
- Validate with data annotations. Return `Page()` when `ModelState` is invalid.
- A successful POST handler ends with `RedirectToPage(...)`.
- No JavaScript in pages unless the task asks for it.
