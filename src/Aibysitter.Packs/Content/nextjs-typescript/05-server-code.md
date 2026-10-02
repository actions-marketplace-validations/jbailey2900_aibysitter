## Server code
- Modules that read secrets or the database import `server-only` on their first line.
- Secrets are read from `process.env` in server code only. Secret names never start with `NEXT_PUBLIC_`.
