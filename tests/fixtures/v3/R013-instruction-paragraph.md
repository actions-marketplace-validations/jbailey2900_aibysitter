# Overview

This repository holds the gateway service and its command-line client. The gateway owns the engine, the local index and the vault; the client talks to it over a local socket. Connectors, the SDK and the editor extension live in their own repositories and release on their own schedule. The word monorepo described this repository until the connectors moved out, and older notes still use it in that sense, which is why some links point at folders that no longer exist here.

## Testing

Run the unit tests before every commit and keep the build green. Integration tests need a local database, so start it with the compose file first and stop it afterwards. Snapshot tests compare rendered output against stored files; when a snapshot changes on purpose, regenerate it with the update flag and commit the new file in the same change. Flaky tests are quarantined with the skip attribute and a linked issue, never deleted. Keep test names in the method_condition_result form so failures read as sentences in the log.
