#!/usr/bin/env bash
# DAT-002: foreign keys crossing schema boundaries are FORBIDDEN.
#
# Scans generated EF Core migrations and fails when a migration owned by one
# module declares a foreign key referencing a table in another module's schema.
# Accepts an optional root directory so the guard itself is testable (T025).
set -uo pipefail

ROOT="${1:-src}"
violations=0

emit() { printf '  %s\n' "$1"; }

while IFS= read -r -d '' migration; do
    # A module owns exactly one schema; derive it from the module directory name.
    module_dir="${migration#*/Modules/}"
    module="${module_dir%%/*}"
    [ "$module" = "$migration" ] && module="$(basename "$(dirname "$(dirname "$migration")")")"
    own_schema="$(printf '%s' "$module" | tr '[:upper:]' '[:lower:]')"

    # 1. EF fluent foreign keys: principalSchema: "other"
    while IFS= read -r line; do
        schema="$(printf '%s' "$line" | sed -n 's/.*principalSchema: *"\([^"]*\)".*/\1/p')"
        if [ -n "$schema" ] && [ "$schema" != "$own_schema" ]; then
            [ $violations -eq 0 ] && echo "DAT-002 violation: foreign key crosses a schema boundary"
            emit "$migration: principalSchema \"$schema\" != owning schema \"$own_schema\""
            violations=$((violations + 1))
        fi
    done < <(grep -n 'principalSchema:' "$migration" 2>/dev/null)

    # 2. Raw SQL foreign keys: REFERENCES other_schema.table
    while IFS= read -r line; do
        schema="$(printf '%s' "$line" | sed -n 's/.*[Rr][Ee][Ff][Ee][Rr][Ee][Nn][Cc][Ee][Ss] *\([A-Za-z_][A-Za-z0-9_]*\)\..*/\1/p')"
        if [ -n "$schema" ] && [ "$schema" != "$own_schema" ]; then
            [ $violations -eq 0 ] && echo "DAT-002 violation: foreign key crosses a schema boundary"
            emit "$migration: REFERENCES $schema.* != owning schema \"$own_schema\""
            violations=$((violations + 1))
        fi
    done < <(grep -niE 'references +[A-Za-z_][A-Za-z0-9_]*\.' "$migration" 2>/dev/null)
done < <(find "$ROOT" -path '*/Migrations/*.cs' -print0 2>/dev/null)

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations cross-schema foreign key(s) found. See constitution DAT-002."
    exit 1
fi

echo "DAT-002 OK: no cross-schema foreign keys in $ROOT"
exit 0
