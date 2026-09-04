#!/usr/bin/env bash
# SPC-001: spec.md describes behaviour only. Framework, library, database, protocol and pattern
# names belong in plan.md.
#
# The enforcement map claimed this scan existed for some time before it did. A rule whose check
# does not run is unenforced, whatever a document says about it -- which is the failure GATE-001
# names and the reason this file exists.
#
# Scans git-tracked spec files, so it enforces exactly what CI sees on a clean checkout.
set -uo pipefail

violations=0

# Technology names. Deliberately specific: a generic word like "service" or "cache" appears in
# legitimate behavioural prose, and a scanner that cries wolf gets muted rather than obeyed.
KEYWORDS='\b(\.NET|dotnet|C#|ASP\.NET|EF Core|EntityFramework|Dapper|PostgreSQL|Postgres|MySQL|SQLite|MongoDB|Redis|RabbitMQ|Kafka|MassTransit|gRPC|GraphQL|OpenAPI|Swagger|Docker|Kubernetes|Vue|React|Angular|PrimeVue|Tailwind|Bootstrap|Node\.js|npm|TypeScript|JavaScript|Python|Java|Nginx|Serilog|xUnit|NUnit|Testcontainers|Npgsql|protobuf|Protocol Buffers)\b'

while IFS= read -r spec; do
    [ -z "$spec" ] && continue
    [ -f "$spec" ] || continue

    # A Clarifications entry may quote a decision that names a technology; that is a record of a
    # conversation, not a requirement. Everything else is in scope.
    hits="$(grep -nEi "$KEYWORDS" "$spec" 2>/dev/null || true)"
    [ -z "$hits" ] && continue

    while IFS= read -r hit; do
        [ -z "$hit" ] && continue
        [ $violations -eq 0 ] && echo "SPC-001 violation: a specification names a technology"
        printf '  %s:%s\n' "$spec" "$(printf '%s' "$hit" | cut -c1-140)"
        violations=$((violations + 1))
    done <<< "$hits"
done < <(git ls-files 'specs/*/spec.md' 2>/dev/null)

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations line(s) name a technology in a specification."
    echo "Move the decision to that feature's plan.md; the spec states behaviour only."
    echo "See constitution SPC-001."
    exit 1
fi

echo "SPC-001 OK: no tracked specification names a technology"
exit 0
