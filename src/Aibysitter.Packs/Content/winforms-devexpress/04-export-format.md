## Export format
- Money is `decimal`, rounded to 2 places with `MidpointRounding.AwayFromZero` at output time only.
- Dates use `yyyy-MM-dd`. Times use 24-hour `HH:mm`.
- Output is UTF-8 without BOM with CRLF line endings. Fields are quoted only when they contain a comma, a quote, or a line break.
- Column order is defined once, in `ExportColumns.cs`.
