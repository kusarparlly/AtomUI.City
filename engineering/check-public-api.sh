#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
core_project="src/AtomUI.City.Core/AtomUI.City.Core.csproj"
shipped_api="src/AtomUI.City.Core/PublicAPI.Shipped.txt"
unshipped_api="src/AtomUI.City.Core/PublicAPI.Unshipped.txt"
eventbus_project="src/AtomUI.City.EventBus/AtomUI.City.EventBus.csproj"
eventbus_shipped_api="src/AtomUI.City.EventBus/PublicAPI.Shipped.txt"
eventbus_unshipped_api="src/AtomUI.City.EventBus/PublicAPI.Unshipped.txt"
presentation_project="src/AtomUI.City.Presentation/AtomUI.City.Presentation.csproj"
presentation_shipped_api="src/AtomUI.City.Presentation/PublicAPI.Shipped.txt"
presentation_unshipped_api="src/AtomUI.City.Presentation/PublicAPI.Unshipped.txt"
validation_output="output/public-api/package-validation"

validate_build_artifacts() {
  local product_name="$1"
  local assembly_name="$2"
  local project="$3"
  local signature_count="$4"
  local require_documented_members="${5:-true}"
  local xml_output_root="output/bin/$configuration/$assembly_name"
  local sourcelink_output_root="output/$assembly_name/obj/$configuration"
  local xml_document_count=0
  local xml_member_count=0
  local sourcelink_document_count=0

  while IFS= read -r xml_document; do
    current_member_count="$(grep -c '<member name=' "$xml_document" || true)"
    if [[ "$require_documented_members" == true && "$current_member_count" -eq 0 ]]; then
      printf '%s XML documentation has no API members: %s\n' "$product_name" "$xml_document" >&2
      exit 1
    fi

    xml_document_count=$((xml_document_count + 1))
    xml_member_count=$((xml_member_count + current_member_count))
  done < <(find "$xml_output_root" -name "$assembly_name.xml" -type f | sort)

  if [[ "$xml_document_count" -eq 0 ]]; then
    printf '%s XML documentation was not produced for %s.\n' "$product_name" "$configuration" >&2
    exit 1
  fi

  while IFS= read -r sourcelink_document; do
    if ! grep -q 'https://raw.githubusercontent.com/' "$sourcelink_document"; then
      printf '%s SourceLink document does not contain a canonical GitHub raw URL: %s\n' \
        "$product_name" \
        "$sourcelink_document" >&2
      exit 1
    fi

    sourcelink_document_count=$((sourcelink_document_count + 1))
  done < <(find "$sourcelink_output_root" -mindepth 2 -name "$assembly_name.sourcelink.json" -type f | sort)

  if [[ "$sourcelink_document_count" -ne "$xml_document_count" ]]; then
    printf '%s SourceLink count (%s) does not match built target framework count (%s).\n' \
      "$product_name" \
      "$sourcelink_document_count" \
      "$xml_document_count" >&2
    exit 1
  fi

  dotnet pack "$project" \
    --configuration "$configuration" \
    --no-build \
    --no-restore \
    --output "$validation_output" \
    -p:TreatWarningsAsErrors=true

  package_path="$(find "$validation_output" -maxdepth 1 -name "$assembly_name.*.nupkg" ! -name '*.snupkg' -type f | sort | tail -n 1)"
  if [[ -z "$package_path" ]]; then
    printf '%s validation package was not produced.\n' "$product_name" >&2
    exit 1
  fi

  package_repository="$(unzip -p "$package_path" '*.nuspec' | grep -Eo '<repository[^>]+>' || true)"
  if [[ "$package_repository" != *'url="https://github.com/'* ]] ||
     [[ "$package_repository" != *"commit=\"$head_revision\""* ]]; then
    printf '%s package repository metadata is not a canonical GitHub URL at HEAD: %s\n' \
      "$product_name" \
      "$package_repository" >&2
    exit 1
  fi

  printf '%s public API gate passed: %s frozen signatures, %s XML members and %s SourceLink document(s) across %s target framework(s).\n' \
    "$product_name" \
    "$signature_count" \
    "$xml_member_count" \
    "$sourcelink_document_count" \
    "$xml_document_count"
}

for required_file in \
  "$core_project" "$shipped_api" "$unshipped_api" \
  "$eventbus_project" "$eventbus_shipped_api" "$eventbus_unshipped_api" \
  "$presentation_project" "$presentation_shipped_api" "$presentation_unshipped_api"; do
  if [[ ! -f "$required_file" ]]; then
    printf 'Missing public API gate input: %s\n' "$required_file" >&2
    exit 1
  fi
done

shipped_signature_count="$(grep -cEv '^[[:space:]]*(#|$)' "$shipped_api" || true)"
if [[ "$shipped_signature_count" -eq 0 ]]; then
  printf 'Core shipped public API baseline is empty: %s\n' "$shipped_api" >&2
  exit 1
fi

eventbus_shipped_signature_count="$(grep -cEv '^[[:space:]]*(#|$)' "$eventbus_shipped_api" || true)"
if [[ "$eventbus_shipped_signature_count" -eq 0 ]]; then
  printf 'EventBus shipped public API baseline is empty: %s\n' "$eventbus_shipped_api" >&2
  exit 1
fi

presentation_shipped_signature_count="$(grep -cEv '^[[:space:]]*(#|$)' "$presentation_shipped_api" || true)"
if [[ "$presentation_shipped_signature_count" -eq 0 ]]; then
  printf 'Presentation shipped public API baseline is empty: %s\n' "$presentation_shipped_api" >&2
  exit 1
fi

if ! grep -q 'Microsoft.CodeAnalysis.PublicApiAnalyzers' "$core_project"; then
  printf 'Core must reference Microsoft.CodeAnalysis.PublicApiAnalyzers.\n' >&2
  exit 1
fi

if ! grep -q '<EnablePackageValidation>true</EnablePackageValidation>' "$core_project"; then
  printf 'Core must enable SDK package validation.\n' >&2
  exit 1
fi

if ! grep -q 'Microsoft.CodeAnalysis.PublicApiAnalyzers' "$eventbus_project"; then
  printf 'EventBus must reference Microsoft.CodeAnalysis.PublicApiAnalyzers.\n' >&2
  exit 1
fi

if ! grep -q '<EnablePackageValidation>true</EnablePackageValidation>' "$eventbus_project"; then
  printf 'EventBus must enable SDK package validation.\n' >&2
  exit 1
fi

if ! grep -q 'Microsoft.CodeAnalysis.PublicApiAnalyzers' "$presentation_project"; then
  printf 'Presentation must reference Microsoft.CodeAnalysis.PublicApiAnalyzers.\n' >&2
  exit 1
fi

if ! grep -q '<EnablePackageValidation>true</EnablePackageValidation>' "$presentation_project"; then
  printf 'Presentation must enable SDK package validation.\n' >&2
  exit 1
fi

dotnet restore "$core_project" -p:Configuration="$configuration"
dotnet restore "$eventbus_project" -p:Configuration="$configuration"
dotnet restore "$presentation_project" -p:Configuration="$configuration"

dotnet build "$core_project" \
  --configuration "$configuration" \
  --no-restore \
  -p:TreatWarningsAsErrors=true

dotnet build "$eventbus_project" \
  --configuration "$configuration" \
  --no-restore \
  -p:TreatWarningsAsErrors=true

dotnet build "$presentation_project" \
  --configuration "$configuration" \
  --no-restore \
  -p:TreatWarningsAsErrors=true

mkdir -p "$validation_output"
head_revision="$(git rev-parse HEAD)"

validate_build_artifacts \
  "Core" \
  "AtomUI.City.Core" \
  "$core_project" \
  "$shipped_signature_count"

validate_build_artifacts \
  "EventBus" \
  "AtomUI.City.EventBus" \
  "$eventbus_project" \
  "$eventbus_shipped_signature_count"

validate_build_artifacts \
  "Presentation" \
  "AtomUI.City.Presentation" \
  "$presentation_project" \
  "$presentation_shipped_signature_count" \
  false
