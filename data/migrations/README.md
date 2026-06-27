# b8agrate migrations

## Common commands

```bash
b8agrate add --name create_users
b8agrate migrate --connection "..."
b8agrate info --connection "..."
b8agrate undo --steps 1 --connection "..."
b8agrate unprovision --admin-connection "..."
```

## Naming

- `P__description.sql` runs before normal migrations with the admin connection.
- `PU__description.sql` undoes the provision script through `b8agrate unprovision`.
- `V000001__description.sql` runs once by default.
- `U000001__description.sql` undoes the matching version.
- `R__description.sql` reruns idempotent reference-data changes when the checksum changes.
