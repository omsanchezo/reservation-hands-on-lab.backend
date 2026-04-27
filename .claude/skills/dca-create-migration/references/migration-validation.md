# Migration Validation

How to validate migrations before committing: the Up/Down/Up cycle, local testing, and CI/CD integration.

---

## The Up/Down/Up Cycle

Every migration must pass the three-step validation cycle before being committed:

```bash
# Step 1: Run migration Up
dotnet run --project src/{ProjectName}.migrations -- /cnn:"Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass"

# Step 2: Verify the change in the database
psql -h localhost -U postgres -d mydb -c "\dt public.*"

# Step 3: Run rollback (Down)
dotnet run --project src/{ProjectName}.migrations -- /cnn:"Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass" /action:rollback

# Step 4: Verify the change was reverted
psql -h localhost -U postgres -d mydb -c "\dt public.*"

# Step 5: Run migration Up again (confirms idempotent re-application)
dotnet run --project src/{ProjectName}.migrations -- /cnn:"Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass"
```

This cycle validates:
- `Up()` applies correctly
- `Down()` is the exact inverse of `Up()`
- `Up()` can be reapplied after a rollback without errors

---

## Verification Queries

After running migrations, verify the state in the database:

### Check Applied Migrations

```sql
SELECT * FROM public.versioninfo ORDER BY version DESC LIMIT 10;
```

### Check Table Structure

```sql
-- PostgreSQL: list tables
\dt public.*

-- PostgreSQL: describe a specific table
\d public.users

-- Check column details
SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'users'
ORDER BY ordinal_position;
```

### Check Indexes

```sql
-- PostgreSQL: list indexes on a table
\di public.*users*

-- Detailed index info
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'users';
```

### Check Foreign Keys

```sql
SELECT
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_schema = 'public'
    AND tc.table_name = 'orders';
```

---

## Rollback Testing

Testing `Down()` is as important as testing `Up()`.

```bash
# Scenario: New migration M080AddColumnToOrders

# 1. State: migrations up to M079
dotnet run -- /cnn:"..."

# 2. Apply M080
dotnet run -- /cnn:"..."

# 3. Verify M080 was applied
psql -c "SELECT * FROM public.versioninfo ORDER BY version DESC LIMIT 5;"

# 4. Rollback M080
dotnet run -- /cnn:"..." /action:rollback

# 5. Verify we are back at M079
psql -c "SELECT * FROM public.versioninfo ORDER BY version DESC LIMIT 5;"

# 6. Verify M080 changes were fully reverted
psql -c "\d public.orders"
```

---

## CI/CD Pipeline

GitHub Actions example for automated migration testing:

```yaml
name: Test Migrations

on: [pull_request]

jobs:
  test-migrations:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:11
        env:
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: testdb
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Run Migrations Up
        run: |
          dotnet run --project src/your.migrations -- \
            /cnn:"Host=localhost;Database=testdb;Username=postgres;Password=postgres"

      - name: Run Migrations Down
        run: |
          dotnet run --project src/your.migrations -- \
            /cnn:"Host=localhost;Database=testdb;Username=postgres;Password=postgres" \
            /action:rollback

      - name: Run Migrations Up Again (idempotency test)
        run: |
          dotnet run --project src/your.migrations -- \
            /cnn:"Host=localhost;Database=testdb;Username=postgres;Password=postgres"
```

---

## Pre-Commit Checklist

Run through this checklist before committing any migration:

### Naming and Versioning
- [ ] File named `M{NNN}{Description}.cs`
- [ ] Class name matches file name
- [ ] Migration number is sequential (greater than the last migration)
- [ ] Number is not reused from a deleted migration

### Code Quality
- [ ] `Up()` implemented correctly
- [ ] `Down()` implemented and is the inverse of `Up()`
- [ ] No dependencies on application code (models, enums, constants)
- [ ] Uses constants for table and column names
- [ ] Table names in `snake_case` and plural
- [ ] Index names follow `ix_{table}_{columns}` convention
- [ ] FK names follow `fk_{table}_{column}` convention

### Schema
- [ ] Frequently queried columns have indexes
- [ ] Foreign keys defined where applicable
- [ ] Unique constraints where applicable
- [ ] Default values are appropriate
- [ ] Nullable/NotNullable is correct
- [ ] `TransactionBehavior` is appropriate

### Security
- [ ] No SQL injection (no external input in SQL strings)
- [ ] No hardcoded secrets or passwords
- [ ] No sensitive data in seed migrations

### Testing
- [ ] Tested `Up()` locally
- [ ] Tested `Down()` locally
- [ ] Tested `Up()` again after rollback
- [ ] Verified schema in database matches expectations
- [ ] For data migrations, tested with a realistic dataset
