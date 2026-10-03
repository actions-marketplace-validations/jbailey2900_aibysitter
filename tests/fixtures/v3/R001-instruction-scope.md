# Rules

- Never commit secrets. This ensures the history stays clean.
- `noImplicitAny` is off, which means implicit `any` passes. Review for it.
- This helps find blocked tasks quickly.
- The worker entry wraps the handler because the platform needs it.
- Do not add helper layers just because files look alike.
- Never change code because of a breaking change elsewhere.
- Write stories in the As a / I want / So that format.
- Report a failure with the reason.
- Keep the changelog honest; that section is the reason people read it.
- Validate input
  because callers pass raw strings.

<!-- this comment quotes phrases in order to ban them -->

In this tutorial, you will extend the app so that it can read files.

Always pin versions. This ensures reproducible builds. The cache is rebuilt nightly because the source changes daily.

Always run tests before pushing,
because CI is slow.
