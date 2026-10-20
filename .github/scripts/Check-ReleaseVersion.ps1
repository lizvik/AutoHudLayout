param(
    [string]$ExpectedTag
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $root 'AutoHudLayout/AutoHudLayout.csproj'
$repoPath = Join-Path $root 'repo.json'

[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($version -match '^[0-9]+\.[0-9]+\.[0-9]+$') {
    $version += '.0'
} elseif ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Project Version '$version' must have three or four numeric parts."
}

$entries = @(ConvertFrom-Json -InputObject (Get-Content -LiteralPath $repoPath -Raw))
if ($entries.Count -ne 1) {
    throw 'repo.json must contain exactly one plugin entry.'
}
foreach ($field in @('AssemblyVersion', 'TestingAssemblyVersion')) {
    if ($entries[0].$field -ne $version) {
        throw "repo.json $field must be $version (found '$($entries[0].$field)')."
    }
}

$tag = "v$version"
if ($ExpectedTag -and $ExpectedTag -cne $tag) {
    throw "Tag '$ExpectedTag' does not match project version '$tag'."
}
Write-Output $tag
