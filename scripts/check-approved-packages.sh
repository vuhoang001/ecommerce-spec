#!/usr/bin/env bash
# STK-001: the Technology Constraints stack is closed. Every PackageVersion in
# Directory.Packages.props must map to an approved runtime component, or be test-only.
#
# Adding a runtime component requires an amendment to the constitution naming the component,
# the rationale, and the enforcement mechanism. This script is that enforcement.
set -uo pipefail

MANIFEST="${1:-Directory.Packages.props}"
violations=0

# Approved runtime components, each traceable to a line in Technology Constraints.
APPROVED_RUNTIME='^(Microsoft\.EntityFrameworkCore|Npgsql|Dapper|MassTransit|Google\.Protobuf|Grpc\.|Serilog|Microsoft\.Extensions\.Diagnostics\.HealthChecks|Microsoft\.AspNetCore\.Diagnostics\.HealthChecks|Microsoft\.Extensions\.Logging\.Abstractions)'

# Test-only packages are outside the runtime stack by definition.
TEST_ONLY='^(xunit|Microsoft\.NET\.Test\.Sdk|coverlet|FluentAssertions|NetArchTest|Testcontainers|Respawn|Microsoft\.AspNetCore\.Mvc\.Testing)'

while IFS= read -r package; do
    if printf '%s' "$package" | grep -qE "$APPROVED_RUNTIME"; then continue; fi
    if printf '%s' "$package" | grep -qE "$TEST_ONLY"; then continue; fi

    [ $violations -eq 0 ] && echo "STK-001 violation: package outside the closed stack"
    printf '  %s\n' "$package"
    violations=$((violations + 1))
done < <(grep -oE 'PackageVersion Include="[^"]+"' "$MANIFEST" | sed 's/.*Include="//;s/"//' | sort -u)

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations package(s) not mapped to an approved component."
    echo "Amend Technology Constraints naming the component, the rationale and the enforcement,"
    echo "then add it here. See constitution STK-001 and GOV-002."
    exit 1
fi

echo "STK-001 OK: every package maps to an approved component"
exit 0
