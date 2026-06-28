# Plan to fix SQL Injection in CandiceSQLiteProvider AddColumn

The `CandiceSQLiteProvider.AddColumn` method uses string concatenation to build the `ALTER TABLE` query. The issue points to this code:
```csharp
createQuery = "ALTER TABLE " + EscapeIdentifier(tableName) + " ADD COLUMN" + columnParameters;
sqlCmd = new SqliteCommand(createQuery, sqlCon);
rc = sqlCmd.ExecuteNonQuery();
```

While `tableName` and `info.Name` are escaped, `info.DefaultValue` is part of `CandiceColumnInfo` but is NOT currently used in `AddColumn`! However, the vulnerability is likely because `columnParameters` is just concatenated in `AddColumn`, and `AddColumn` may be changed to support default values. Actually, let's look closer at `AddColumn`.

```csharp
columnParameters += EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;
```

Wait, `EscapeIdentifier` is used for `info.Name`. But what about `info.Type`? `info.Type` is checked with `IsValidIdentifier(info.Type)`, which only allows `^[a-zA-Z0-9_]+$`.

Wait! The vulnerability description states:
"Direct string concatenation used to build a SQL ALTER TABLE command, making it susceptible to SQL injection."

Wait, the prompt says:
```csharp
createQuery = "ALTER TABLE " + EscapeIdentifier(tableName) + " ADD COLUMN" + columnParameters;
```
If we check how `AddColumn` supports `DefaultValue`. Ah, actually, `AddColumn` doesn't currently handle `info.DefaultValue`.

If the vulnerability is SQL injection via `info.DefaultValue` (since it's a string in `CandiceColumnInfo`), let's see. SQLite `ALTER TABLE ADD COLUMN` allows specifying a `DEFAULT` value. But it's not currently in the string? Wait, no, the prompt is simply:
"Direct string concatenation used to build a SQL ALTER TABLE command, making it susceptible to SQL injection."

If we look at `CreateTable`, it also uses string concatenation:
```csharp
string fragment = EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;
// ...
createQuery = "CREATE TABLE IF NOT EXISTS " + EscapeIdentifier(tableName) + columnParameters;
```

Wait, `AddColumn` in SQLite does not support parameters (`@val`) for structure modifications like `ALTER TABLE`. We must sanitize or escape the inputs.
But wait, `info.DefaultValue` is part of `CandiceColumnInfo`. If we add `DEFAULT` clause, we should properly quote it.
Wait, the prompt just says: "SQL Injection Vulnerability in CandiceSQLiteProvider AddColumn ... Direct string concatenation used to build a SQL ALTER TABLE command, making it susceptible to SQL injection."

Is `info.DefaultValue` the missing piece? If someone wants to add a column with a default value, they pass it in `info.DefaultValue`, but right now the code ignores it. If we *do* use it, we have to be careful. But the prompt specifically targets:
```csharp
createQuery = "ALTER TABLE " + EscapeIdentifier(tableName) + " ADD COLUMN" + columnParameters;
```
Wait. `columnParameters` is formed by:
```csharp
columnParameters += EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;
```
Since `info.DefaultValue` is a string, if the user adds `DEFAULT '...'` to `info.DefaultValue`, maybe they try to append it? Actually, `DefaultValue` isn't appended at all in the current `AddColumn`. But wait! Look at the `CandiceColumnInfo` constructor:
```csharp
public CandiceColumnInfo(string name, string type, string defaultValue, bool notNull, bool pk, bool ai)
```

Is there a place where `DefaultValue` is appended? Let's check `CreateTable`. No, `CreateTable` also doesn't append `DefaultValue`! Wait, it doesn't? Let me check `CreateTable` again.

Wait, if I look at `AddColumn` again:
```csharp
        public int AddColumn(string tableName, CandiceColumnInfo info)
        {
            if (!IsValidIdentifier(tableName))
...
            if (!IsValidIdentifier(info.Name))
...
            if (!IsValidIdentifier(info.Type))
...
            string columnParameters = " ";
            string nonNull = "";
            string autoincrement = "";
            string pk = "";
            if (info.Pk)
            {
                pk = " PRIMARY KEY";
            }
            if (info.Ai)
            {
                autoincrement = " AUTOINCREMENT";
            }
            if (info.NotNull)
            {
                nonNull = " NOT NULL";
            }

            // Wait, what if info.DefaultValue is used? It's not!

            // Let's add default value properly?

            // And maybe they want parameterized queries?
            // But ALTER TABLE doesn't support parameterized query for table name and column names!
```

Actually, `ALTER TABLE` in SQLite does NOT support parameterization (`@param`) for structural identifiers or even for the `DEFAULT` value. But wait, `DEFAULT` value can be a literal string. If it's a string, we can quote it: `'` + defaultValue.Replace("'", "''") + `'`.

However, the user prompt states the vulnerability is "Direct string concatenation used to build a SQL ALTER TABLE command, making it susceptible to SQL injection."

Is there any injection possible if `tableName`, `info.Name`, and `info.Type` are all validated with `IsValidIdentifier`?
Wait! `IsValidIdentifier` checks `^[a-zA-Z0-9_]+$`. That is safe!
Wait! The `AddColumn` method:
```csharp
            if (!IsValidIdentifier(tableName))
            {
                Debug.LogError("Invalid table name provided: " + tableName);
                return -1;
            }
            if (!IsValidIdentifier(info.Name))
            {
                Debug.LogError("Invalid column name provided: " + info.Name);
                return -1;
            }
            if (!IsValidIdentifier(info.Type))
            {
                Debug.LogError("Invalid column type provided: " + info.Type);
                return -1;
            }
```

Wait, `info.DefaultValue` is NOT checked and is NOT used.
Wait, if it's not used, how is there an injection?
Maybe the problem is that `CandiceColumnInfo` allows arbitrary `DefaultValue`? No, if it's not used, it can't be injected.

Let's read `CandiceSQLiteProvider.cs` line 512:
```csharp
            columnParameters += EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;
```
Where is `DefaultValue` in `AddColumn`? It's NOT there! But wait, is there an injection?
Wait. Look at `CandiceColumnInfo` again. What if `info.DefaultValue` *should* be there, and its omission is a bug, but if we add it, we must prevent injection?
Or is there another way to inject?
What if `info.DefaultValue` is missing, and the fix is to add it securely?

Wait, maybe `info.DefaultValue` is already used in `AddColumn` in the user's mind, but the code is missing it, so the bug isn't just SQL injection, but they assume `DEFAULT` should be appended?
Actually, let's look at `columnParameters`.
```csharp
columnParameters += EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;
```
If we add DefaultValue:
```csharp
string defaultValue = "";
if (!string.IsNullOrEmpty(info.DefaultValue))
{
    // Need to safely append default value. For strings in SQLite, we escape single quotes.
    // Or we only allow valid identifiers or numbers?
    // Let's just escape single quotes.
    defaultValue = " DEFAULT '" + info.DefaultValue.Replace("'", "''") + "'";
}
```
Wait, the prompt doesn't say anything about missing `DefaultValue`.
"Direct string concatenation used to build a SQL ALTER TABLE command, making it susceptible to SQL injection."

Could there be SQL injection in `EscapeIdentifier(tableName)`?
```csharp
        private string EscapeIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return identifier;
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }
```
`EscapeIdentifier` surrounds the string with double quotes and escapes double quotes. This is standard SQL standard for identifiers.
And it is combined with `IsValidIdentifier` which strictly allows only alphanumeric and underscore!
If it's already checked by `IsValidIdentifier`, how is it vulnerable?
Wait! In `AddColumn`, `info.DefaultValue` is NOT used. But maybe `info.Type` can be injected?
`info.Type` is checked:
```csharp
            if (!IsValidIdentifier(info.Type))
            {
                Debug.LogError("Invalid column type provided: " + info.Type);
                return -1;
            }
```
Wait, what if `IsValidIdentifier` allows injection?
```csharp
        private bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z0-9_]+$");
        }
```
No, `^[a-zA-Z0-9_]+$` is extremely strict. No spaces, no quotes, no semicolons.

Where is the SQL injection then?!
Wait! Let's re-read `AddColumn`.
```csharp
            string columnParameters = " ";
            string nonNull = "";
            string autoincrement = "";
            string pk = "";
            if (info.Pk)
            {
                pk = " PRIMARY KEY";
            }
            if (info.Ai)
            {
                autoincrement = " AUTOINCREMENT";
            }
            if (info.NotNull)
            {
                nonNull = " NOT NULL";
            }

            // Wait, what if `info.DefaultValue` is used? Let's check `CandiceColumnInfo`.
```
Is there a missing `DefaultValue` implementation? Let's see if the user wants `DefaultValue` added securely.
Wait! What if we look at `AddColumn` and we just use `SqliteCommand.Parameters.AddWithValue`?
You CANNOT use `Parameters` for `ALTER TABLE ADD COLUMN`. SQLite does not allow parameterized statements for schema modifications.

Let's check the memory:
"When constructing dynamic SQLite DDL statements (e.g., ALTER TABLE) in C#, parameterization is unsupported. Prevent SQL injection by manually escaping identifiers and applying strict allow-list validation (e.g., Regex) to user-provided schema definitions."

Oh! The memory explicitly says:
"When constructing dynamic SQLite DDL statements (e.g., ALTER TABLE) in C#, parameterization is unsupported. Prevent SQL injection by manually escaping identifiers and applying strict allow-list validation (e.g., Regex) to user-provided schema definitions."

Wait, `AddColumn` ALREADY DOES THIS for `tableName`, `info.Name`, and `info.Type`.
Let's check `AddColumn` again. Is there anything it missed?
```csharp
        public int AddColumn(string tableName, CandiceColumnInfo info)
        {
            if (!IsValidIdentifier(tableName))
            {
                Debug.LogError("Invalid table name provided: " + tableName);
                return -1;
            }
            if (!IsValidIdentifier(info.Name))
            {
                Debug.LogError("Invalid column name provided: " + info.Name);
                return -1;
            }
            if (!IsValidIdentifier(info.Type))
            {
                Debug.LogError("Invalid column type provided: " + info.Type);
                return -1;
            }
```
Wait. Did they add `DefaultValue`?
Let's look at `AddColumn` again. What about `DefaultValue`?
```csharp
            string columnParameters = " ";
```
Wait! Look at `info.DefaultValue`. It's a string. If the original code DOES include `DefaultValue` in `AddColumn` and I missed it?
Let me check the `AddColumn` method again in the file!
