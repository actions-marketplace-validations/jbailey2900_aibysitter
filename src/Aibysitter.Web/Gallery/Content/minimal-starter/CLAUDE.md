# Project rules

Fill in the bracketed parts. Delete any line that does not apply. Keep this file under 60 lines.

## Stack
- Language and version: [C# 13 on .NET 10]
- Frameworks: [ASP.NET Core, EF Core]
- Package manager: [NuGet]

## Commands
- Build: `[build command]`
- Test: `[test command]`
- Format or lint: `[format command]`

## Rules
- Run the test command before reporting a task complete.
- Change only the files the task names, plus their tests.
- Match the code style of the file being edited.
- Ask before adding a dependency.
- Ask before deleting a file.
- Do not commit secrets, keys, or `.env` files.

## Done
- Build and tests pass.
- New behavior has a test that asserts the result.
- The PR description lists what changed and how it was tested.
