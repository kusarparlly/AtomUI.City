#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration|-c)
      configuration="$2"
      shift 2
      ;;
    *)
      printf 'Unknown argument: %s\n' "$1" >&2
      exit 2
      ;;
  esac
done

package_dir="output/NuGet/$configuration"
version="$(sed -n 's:.*<AtomUICityVersion>\(.*\)</AtomUICityVersion>.*:\1:p' build/Version.props | head -n 1)"

if [[ -z "$version" ]]; then
  printf 'Unable to determine AtomUI.City version.\n' >&2
  exit 2
fi

if [[ ! -d "$package_dir" ]]; then
  printf 'Package output directory does not exist: %s\n' "$package_dir" >&2
  exit 1
fi

require_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    printf 'Missing package file: %s\n' "$path" >&2
    exit 1
  fi
}

require_entry() {
  local package="$1"
  local entries="$2"
  local entry="$3"

  if ! grep -Fxq "$entry" <<< "$entries"; then
    printf 'Package %s is missing entry: %s\n' "$package" "$entry" >&2
    exit 1
  fi
}

require_entry_pattern() {
  local package="$1"
  local entries="$2"
  local pattern="$3"

  if ! grep -Eq "$pattern" <<< "$entries"; then
    printf 'Package %s is missing entry pattern: %s\n' "$package" "$pattern" >&2
    exit 1
  fi
}

reject_entry_pattern() {
  local package="$1"
  local entries="$2"
  local pattern="$3"
  local message="$4"

  if grep -Eq "$pattern" <<< "$entries"; then
    printf 'Package %s has forbidden entry pattern: %s (%s)\n' "$package" "$pattern" "$message" >&2
    exit 1
  fi
}

require_nuspec_metadata() {
  local package="$1"
  local nuspec="$2"
  local project_name="$3"

  require_nuspec_text "$package" "$nuspec" "<id>$project_name</id>"
  require_nuspec_text "$package" "$nuspec" "<version>$version</version>"
  require_nuspec_text "$package" "$nuspec" '<license type="expression">LGPL-3.0-only</license>'
  require_nuspec_text "$package" "$nuspec" '<repository type="git" url="https://github.com/AtomUI/AtomUI.City"'
  require_nuspec_text "$package" "$nuspec" '<readme>README.nuget.md</readme>'
  require_nuspec_text "$package" "$nuspec" '<releaseNotes>See RELEASE_NOTES.md for release notes.</releaseNotes>'
}

require_nuspec_dependency_group() {
  local package="$1"
  local nuspec="$2"

  require_nuspec_text "$package" "$nuspec" '<dependencies>'
  require_nuspec_text "$package" "$nuspec" '<group targetFramework='
}

require_nuspec_text() {
  local package="$1"
  local nuspec="$2"
  local text="$3"

  if ! grep -Fq "$text" <<< "$nuspec"; then
    printf 'Package %s nuspec is missing text: %s\n' "$package" "$text" >&2
    exit 1
  fi
}

while IFS= read -r project; do
  project_name="$(basename "$project" .csproj)"
  nupkg="$package_dir/$project_name.$version.nupkg"
  snupkg="$package_dir/$project_name.$version.snupkg"

  require_file "$nupkg"

  entries="$(unzip -Z1 "$nupkg")"
  require_entry "$nupkg" "$entries" "$project_name.nuspec"
  nuspec="$(unzip -p "$nupkg" "$project_name.nuspec")"
  require_nuspec_metadata "$nupkg" "$nuspec" "$project_name"
  require_entry "$nupkg" "$entries" "LICENSE"
  require_entry "$nupkg" "$entries" "README.nuget.md"
  require_entry "$nupkg" "$entries" "RELEASE_NOTES.md"

  case "$project_name" in
    AtomUI.City.Build)
      require_file "$snupkg"
      require_nuspec_dependency_group "$nupkg" "$nuspec"
      require_entry_pattern "$nupkg" "$entries" "^lib/.+/$project_name\\.dll$"
      require_entry_pattern "$nupkg" "$entries" "^lib/.+/$project_name\\.xml$"
      require_entry "$nupkg" "$entries" "buildTransitive/AtomUI.City.Build.props"
      require_entry "$nupkg" "$entries" "buildTransitive/AtomUI.City.Build.targets"
      require_entry "$nupkg" "$entries" "buildTransitive/AtomUI.City.Application.targets"
      require_entry "$nupkg" "$entries" "buildTransitive/AtomUI.City.Plugin.targets"
      require_entry "$nupkg" "$entries" "buildTransitive/AtomUI.City.Core.Diagnostics.targets"
      require_entry "$nupkg" "$entries" "analyzers/dotnet/cs/AtomUI.City.Generators.dll"
      snupkg_entries="$(unzip -Z1 "$snupkg")"
      require_entry_pattern "$snupkg" "$snupkg_entries" "^lib/.+/$project_name\\.pdb$"
      ;;
    AtomUI.City.Generators)
      require_file "$snupkg"
      require_entry "$nupkg" "$entries" "analyzers/dotnet/cs/$project_name.dll"
      require_entry "$nupkg" "$entries" "analyzers/dotnet/cs/$project_name.pdb"
      reject_entry_pattern "$nupkg" "$entries" "^lib/" "Generator package contains runtime lib asset"
      ;;
    AtomUI.City.Templates)
      require_entry "$nupkg" "$entries" "content/templates/atomui-city-app/.template.config/template.json"
      require_entry "$nupkg" "$entries" "content/templates/atomui-city-plugin/.template.config/template.json"
      ;;
    *)
      require_nuspec_dependency_group "$nupkg" "$nuspec"
      require_file "$snupkg"
      require_entry_pattern "$nupkg" "$entries" "^lib/.+/$project_name\\.dll$"
      require_entry_pattern "$nupkg" "$entries" "^lib/.+/$project_name\\.xml$"
      snupkg_entries="$(unzip -Z1 "$snupkg")"
      require_entry_pattern "$snupkg" "$snupkg_entries" "^lib/.+/$project_name\\.pdb$"
      ;;
  esac
done < <(find src/AtomUI.City.* -name 'AtomUI.City.*.csproj' | sort)

printf 'Validated packages in %s\n' "$package_dir"
