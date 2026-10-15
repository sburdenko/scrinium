#!/usr/bin/env bash
# Publishes Scrinium for Apple Silicon, wraps it in dist/Scrinium.app with its icon and Info.plist,
# and zips it as dist/Scrinium-<version>-macos-arm64.zip.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
bundle="$root/dist/Scrinium.app"
publish="$(mktemp -d "${TMPDIR:-/tmp}/scrinium-publish.XXXXXX")"
trap 'rm -rf "$publish"' EXIT

dotnet publish "$root/src/Scrinium.Desktop" -c Release -r osx-arm64 --self-contained -o "$publish"

rm -rf "$bundle"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
cp -R "$publish"/. "$bundle/Contents/MacOS/"
cp "$root/packaging/macos/Info.plist" "$bundle/Contents/"
version="$(dotnet msbuild "$root/src/Scrinium.Desktop" -getProperty:Version)"
/usr/libexec/PlistBuddy -c "Add :CFBundleShortVersionString string $version" -c "Add :CFBundleVersion string $version" "$bundle/Contents/Info.plist"
cp "$root/src/Scrinium.Desktop/Assets/Icon/scrinium.icns" "$bundle/Contents/Resources/Scrinium.icns"
plutil -lint "$bundle/Contents/Info.plist" >/dev/null

# Without a signature macOS reports a downloaded app as damaged instead of asking whether to open it.
codesign --force --deep --sign - "$bundle"
codesign --verify --deep --strict "$bundle"

archive="$root/dist/Scrinium-${version}-macos-arm64.zip"
rm -f "$archive"
ditto -c -k --keepParent "$bundle" "$archive"

echo "Scrinium.app and $(basename "$archive") ready in dist/"
