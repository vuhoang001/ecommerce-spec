#!/usr/bin/env bash
# STK-001: the Technology Constraints stack is closed. Every dependency must map to an approved
# component, or be test-only.
#
# Adding a runtime component requires an amendment to the constitution naming the component, the
# rationale, and the enforcement mechanism. This script is that enforcement.
#
# 2.2.0 widened the stack to admit a frontend on Node LTS, so both manifests are scanned: a gate
# reading only the NuGet manifest would report OK while an unapproved npm dependency sat
# unexamined, silently enforcing STK-001 for half the stack.
set -uo pipefail

MANIFEST="${1:-Directory.Packages.props}"
violations=0

# Approved runtime components, each traceable to a line in Technology Constraints.
# Microsoft.AspNetCore.* and Microsoft.Extensions.* ship with .NET 8 itself, which the stack names.
APPROVED_RUNTIME='^(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore\.|Microsoft\.Extensions\.|Npgsql|Dapper|MassTransit|Google\.Protobuf|Grpc\.|Serilog)'

# Test-only packages are outside the runtime stack by definition.
TEST_ONLY='^(xunit|Microsoft\.NET\.Test\.Sdk|coverlet|FluentAssertions|NetArchTest|Testcontainers|Respawn|YamlDotNet)'

# Approved frontend components, from Technology Constraints as amended in 2.2.0.
APPROVED_NODE='^(vue$|@vue/|primevue|primeicons|vite|@vitejs/|typescript|@types/|eslint|prettier|vitest|openapi-typescript)'

# ---- NuGet -------------------------------------------------------------------------------
if [ -f "$MANIFEST" ]; then
    while IFS= read -r package; do
        [ -z "$package" ] && continue
        printf '%s' "$package" | grep -qE "$APPROVED_RUNTIME" && continue
        printf '%s' "$package" | grep -qE "$TEST_ONLY" && continue

        [ $violations -eq 0 ] && echo "STK-001 violation: dependency outside the closed stack"
        printf '  %s (%s)\n' "$package" "$MANIFEST"
        violations=$((violations + 1))
    done < <(grep -oE 'PackageVersion Include="[^"]+"' "$MANIFEST" | sed 's/.*Include="//;s/"//' | sort -u)
fi

# ---- npm ---------------------------------------------------------------------------------
while IFS= read -r manifest; do
    [ -z "$manifest" ] && continue
    while IFS= read -r package; do
        [ -z "$package" ] && continue
        printf '%s' "$package" | grep -qE "$APPROVED_NODE" && continue

        [ $violations -eq 0 ] && echo "STK-001 violation: dependency outside the closed stack"
        printf '  %s (%s)\n' "$package" "$manifest"
        violations=$((violations + 1))
    done < <(python3 -c '
import json, sys
try:
    d = json.load(open(sys.argv[1]))
except Exception:
    sys.exit(0)
for section in ("dependencies", "devDependencies"):
    for name in d.get(section, {}):
        print(name)
' "$manifest")
done < <(find . -name package.json -not -path '*/node_modules/*' -not -path './.git/*' 2>/dev/null)

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations dependency(ies) not mapped to an approved component."
    echo "Amend Technology Constraints naming the component, the rationale and the enforcement,"
    echo "then add it here. See constitution STK-001 and the Governance amendment clause."
    exit 1
fi

echo "STK-001 OK: every dependency maps to an approved component (NuGet and npm)"
exit 0
