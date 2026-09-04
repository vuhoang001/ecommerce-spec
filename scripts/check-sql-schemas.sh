#!/usr/bin/env bash
# DAT-006: raw SQL MUST NOT reference a table outside its own module's schema.
#
# DAT-001's enforcement inspects DbContext mappings, which raw SQL bypasses entirely. Since
# DAT-004 moved every read onto Dapper, raw SQL is now the main read path — so this scan is what
# keeps schema ownership true on the read side.
set -uo pipefail

ROOT="${1:-src}"
violations=0

while IFS= read -r -d '' file; do
    module_path="${file#*/Modules/}"
    module="${module_path%%/*}"
    [ "$module" = "$file" ] && continue          # not inside a module
    own_schema="$(printf '%s' "$module" | tr '[:upper:]' '[:lower:]')"

    # Any schema-qualified table reference in a SQL context.
    # Strip comment lines first: prose such as "from research.md R6" is not SQL, and a scanner
    # that cannot tell the difference gets muted rather than fixed.
    sql_only="$(grep -vE '^[[:space:]]*(//|///|\*|<!--)' "$file" 2>/dev/null)"

    while IFS= read -r ref; do
        # Exclude file names that merely look schema-qualified.
        case "${ref##*.}" in
            md|cs|json|yml|yaml|sh|proto|props|sln|csproj|txt) continue ;;
        esac
        schema="${ref%%.*}"
        [ "$schema" = "$own_schema" ] && continue
        [ $violations -eq 0 ] && echo "DAT-006 violation: raw SQL references another module's schema"
        printf '  %s: %s (owning schema is "%s")\n' "$file" "$ref" "$own_schema"
        violations=$((violations + 1))
    done < <(printf '%s\n' "$sql_only" \
             | grep -oiE '\b(from|join|into|update|delete from)[[:space:]]+[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*' 2>/dev/null \
             | awk '{print $NF}' | sort -u)
done < <(find "$ROOT" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 2>/dev/null)

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations cross-schema SQL reference(s). See constitution DAT-006."
    exit 1
fi

echo "DAT-006 OK: no raw SQL references a schema outside its own module"
exit 0
