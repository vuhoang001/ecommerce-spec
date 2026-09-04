#!/usr/bin/env bash
# MSG-003: a breaking change to an event schema MUST ship as a new version, and the previous
# version MUST keep being published until every consumer has migrated.
#
# Compares contract files against a baseline ref and fails when a published schema changes in
# place instead of gaining a new version.
set -uo pipefail

BASELINE="${1:-origin/main}"
CONTRACT_GLOBS=("*/Contracts/Protos/*.proto" "*/Contracts/Events/*.cs")
violations=0

if ! git rev-parse --verify "$BASELINE" >/dev/null 2>&1; then
    echo "Baseline '$BASELINE' not found; treating this as the first commit of the contracts."
    echo "MSG-003 OK: nothing to compare against"
    exit 0
fi

for glob in "${CONTRACT_GLOBS[@]}"; do
    while IFS= read -r file; do
        [ -z "$file" ] && continue
        git show "$BASELINE:$file" >/dev/null 2>&1 || continue   # newly added: always fine

        if ! git diff --quiet "$BASELINE" -- "$file"; then
            # The file changed. A version bump means a NEW file appeared alongside it; changing
            # a published schema in place is what MSG-003 forbids.
            removed=$(git diff "$BASELINE" -- "$file" | grep -cE '^-[^-]' || true)

            if [ "$removed" -gt 0 ]; then
                [ $violations -eq 0 ] && echo "MSG-003 violation: a published schema changed in place"
                echo "  $file: $removed line(s) removed or altered against $BASELINE"
                echo "    A breaking change requires a new version (.v2) published alongside .v1."
                violations=$((violations + 1))
            fi
        fi
    done < <(git ls-files "$glob")
done

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations schema(s) changed in place. See constitution MSG-003."
    exit 1
fi

echo "MSG-003 OK: no published event schema changed in place against $BASELINE"
exit 0
