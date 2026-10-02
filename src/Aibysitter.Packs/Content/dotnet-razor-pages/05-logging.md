## Logging
- Use `ILogger<T>` with message templates: `logger.LogInformation("Order {OrderId} shipped", order.Id)`.
- Do not log email addresses, names, or payment details.
