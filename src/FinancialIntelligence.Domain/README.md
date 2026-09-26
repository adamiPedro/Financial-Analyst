# Domain

Entities, value objects and invariants, organized by module (`Companies`,
`FinancialData`, ...).

Nothing in this project may reference anything else — no EF Core, no
`HttpClient`, no `Microsoft.Extensions.*`. `DependencyRuleTests` enforces it.
