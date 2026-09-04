#!/usr/bin/env bash
# DEP-001: every deployable artifact MUST ship as a container image built by CI from a
# Dockerfile checked into the repository. Build or install steps performed outside the image
# are FORBIDDEN.
#
# A "deployable" is any project that produces a runnable process: an ASP.NET Core web project
# or a console entry point. Class libraries and test projects are not deployables.
set -uo pipefail

ROOT="${1:-src}"
violations=0

while IFS= read -r project; do
    # A deployable declares an SDK that produces a host, or an OutputType of Exe.
    is_web=$(grep -c 'Sdk="Microsoft.NET.Sdk.Web"' "$project" 2>/dev/null || true)
    is_exe=$(grep -c '<OutputType>Exe</OutputType>' "$project" 2>/dev/null || true)
    [ "$is_web" -eq 0 ] && [ "$is_exe" -eq 0 ] && continue

    dir="$(dirname "$project")"
    if [ ! -f "$dir/Dockerfile" ]; then
        [ $violations -eq 0 ] && echo "DEP-001 violation: a deployable has no Dockerfile"
        printf '  %s has no Dockerfile\n' "$project"
        violations=$((violations + 1))
        continue
    fi

    # Build steps outside the image are forbidden, so the Dockerfile must restore and publish
    # itself rather than copy artifacts the runner produced.
    if ! grep -q 'dotnet publish' "$dir/Dockerfile"; then
        [ $violations -eq 0 ] && echo "DEP-001 violation: image does not build itself"
        printf '  %s/Dockerfile never runs dotnet publish — it must not copy a host-built artifact\n' "$dir"
        violations=$((violations + 1))
    fi
done < <(find "$ROOT" -name '*.csproj' -not -path '*/obj/*' 2>/dev/null)

if [ "$violations" -gt 0 ]; then
    echo
    echo "$violations deployable(s) not shipped as a self-building image. See constitution DEP-001."
    exit 1
fi

echo "DEP-001 OK: every deployable has a Dockerfile that builds itself"
exit 0
