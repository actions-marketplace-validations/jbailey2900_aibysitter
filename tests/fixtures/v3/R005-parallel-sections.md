# Rules

## Research mode

**Output Format**:
Start with the research header line.
Use markdown syntax for formatting answers.

## Plan mode

**Output Format**:
Start with the plan header line and the checklist.
Use markdown syntax for formatting answers.

## Providers

- `generateText(params)`:
    - Call the text endpoint with the model.
    - Return the full result object.
    - Include basic validation and error handling.
- `generateObject(params)`:
    - Call the object endpoint with the schema.
    - Return only the parsed object.
    - Include basic validation and error handling.

## Frontend

- Keep components small and focused.
- Never commit secrets to the repository.

## Backend

- Never commit secrets to the repository.
- Keep handlers thin and testable.
