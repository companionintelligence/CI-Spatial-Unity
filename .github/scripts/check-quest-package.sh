#!/usr/bin/env bash
# Checks the APK or AAB that CommandLineBuild.BuildAndroidQuest wrote to Builds/Android and
# copies it to dist/ with its SHA-256, for the upload step. quest-build.yml runs it after
# the GameCI build step:
#
#   bash .github/scripts/check-quest-package.sh <debug|release> <apk|aab>
#
# The checks use the Android tools of the Unity image the build ran in: aapt2 and apksigner
# for an APK, bundletool, jarsigner and keytool for an AAB.
#   - versionCode and versionName as planned from bundleVersion, and target API level 34;
#   - an APK must verify with a v2 signature (the Meta Horizon Store accepts v1 or v2);
#   - a release package must be signed, and not with the Android debug certificate.
#
# Environment: UNITY_VERSION, VERSION, VERSION_CODE, and GitHub's GITHUB_OUTPUT,
# GITHUB_STEP_SUMMARY, GITHUB_REF_TYPE, GITHUB_SHA and RUNNER_TEMP.
# Step outputs: artifact-name, cert-sha256.
set -euo pipefail

signing="${1:-}"
package="${2:-}"
if [[ "$signing" != debug && "$signing" != release ]] || [[ "$package" != apk && "$package" != aab ]]; then
  echo "usage: $0 <debug|release> <apk|aab>" >&2
  exit 2
fi
: "${UNITY_VERSION:?}" "${VERSION:?}" "${VERSION_CODE:?}"

shopt -s nullglob
files=(Builds/Android/*."$package")
if [ "${#files[@]}" -ne 1 ]; then
  echo "::error::Expected one .$package file in Builds/Android, found ${#files[@]}"
  ls -la Builds/Android || true
  exit 1
fi
file="${files[0]}"

image=""
while read -r ref; do
  if [[ "$ref" == "unityci/editor:ubuntu-${UNITY_VERSION}-android-"* ]]; then
    image="$ref"
    break
  fi
done < <(docker image ls --format '{{.Repository}}:{{.Tag}}')
if [ -z "$image" ]; then
  echo "::error::No unityci/editor Android image for Unity ${UNITY_VERSION} found locally to check the package with"
  exit 1
fi

# Runs inside the Unity image. Prints the manifest values and the signature report.
inspect="$RUNNER_TEMP/inspect-quest-package.sh"
cat > "$inspect" <<'SH'
set -u
tool() { find -L /opt -type f -name "$1" -path "$2" 2>/dev/null | sort -V | tail -n 1; }
java="$(tool java '*/OpenJDK/bin/*')"
[ -n "$java" ] || java="$(command -v java || true)"
case "$2" in
  apk)
    aapt2="$(tool aapt2 '*/build-tools/*')"
    apksigner="$(tool apksigner.jar '*/build-tools/*')"
    if [ -z "$java" ] || [ -z "$aapt2" ] || [ -z "$apksigner" ]; then
      echo "java, aapt2 or apksigner not found in the Unity image" >&2
      exit 1
    fi
    echo "== manifest"
    "$aapt2" dump badging "$1" | grep -E "^(package|targetSdkVersion):"
    echo "== signature"
    "$java" -jar "$apksigner" verify --verbose --print-certs "$1" || echo "apksigner exit code $?"
    ;;
  aab)
    bundletool="$(tool 'bundletool-all-*.jar' '*/AndroidPlayer/Tools/*')"
    jdk_bin="$(dirname "$java")"
    if [ -z "$java" ] || [ -z "$bundletool" ] || [ ! -x "$jdk_bin/jarsigner" ] || [ ! -x "$jdk_bin/keytool" ]; then
      echo "java, jarsigner, keytool or bundletool not found in the Unity image" >&2
      exit 1
    fi
    echo "== manifest"
    for xpath in /manifest/@android:versionCode /manifest/@android:versionName /manifest/uses-sdk/@android:targetSdkVersion; do
      echo "${xpath##*:}=$("$java" -jar "$bundletool" dump manifest --bundle "$1" --xpath "$xpath")"
    done
    echo "== signature"
    "$jdk_bin/jarsigner" -verify "$1" || echo "jarsigner exit code $?"
    echo "== certificate"
    "$jdk_bin/keytool" -printcert -jarfile "$1" || echo "keytool exit code $?"
    ;;
esac
SH

if ! report="$(docker run --rm --entrypoint /bin/bash \
  -v "$inspect:/inspect.sh:ro" -v "$PWD/Builds/Android:/package:ro" \
  "$image" /inspect.sh "/package/$(basename "$file")" "$package" 2>&1)"; then
  echo "$report"
  echo "::error::Could not inspect $file"
  exit 1
fi
echo "$report"

errors=()
if [ "$package" = apk ]; then
  grep -qF "versionCode='${VERSION_CODE}' versionName='${VERSION}'" <<< "$report" ||
    errors+=("The APK is not versionCode ${VERSION_CODE}, versionName ${VERSION}")
  grep -qx "targetSdkVersion:'34'" <<< "$report" || errors+=("The APK does not target API level 34")
  grep -qx "Verifies" <<< "$report" || errors+=("apksigner does not verify the APK")
  grep -qx "Verified using v2 scheme (APK Signature Scheme v2): true" <<< "$report" ||
    errors+=("The APK has no v2 signature (the Meta Horizon Store needs v1 or v2)")
  dn="$(sed -n 's/^Signer #1 certificate DN: //p' <<< "$report" | head -n 1)"
  sha256="$(sed -n 's/^Signer #1 certificate SHA-256 digest: //p' <<< "$report" | head -n 1)"
else
  if ! grep -qx "versionCode=${VERSION_CODE}" <<< "$report" || ! grep -qx "versionName=${VERSION}" <<< "$report"; then
    errors+=("The AAB is not versionCode ${VERSION_CODE}, versionName ${VERSION}")
  fi
  grep -qx "targetSdkVersion=34" <<< "$report" || errors+=("The AAB does not target API level 34")
  dn="$(sed -n 's/^Owner: //p' <<< "$report" | head -n 1)"
  sha256="$(sed -n 's/^[[:space:]]*SHA256: //p' <<< "$report" | head -n 1 | tr -d ':' | tr 'A-F' 'a-f')"
  if [ "$signing" = release ] && ! grep -qx "jar verified." <<< "$report"; then
    errors+=("jarsigner does not verify the AAB")
  fi
fi
if [ "$signing" = release ]; then
  if [ -z "$dn" ] || [ -z "$sha256" ]; then
    errors+=("No signing certificate found in the release ${package^^}")
  elif [[ "$dn" == *"CN=Android Debug"* ]]; then
    errors+=("The release ${package^^} is signed with the Android debug certificate")
  fi
fi
if [ "${#errors[@]}" -gt 0 ]; then
  for error in "${errors[@]}"; do echo "::error::$error"; done
  exit 1
fi
echo "Package checks passed: ${package^^} signed with the $signing key, ${dn:-no certificate}"

# Every build of one version has the same versionCode, so builds other than tagged releases
# carry the commit in their name, and debug builds say so.
name="CompanionScan-${VERSION}"
if [ "${GITHUB_REF_TYPE:-}" != tag ]; then
  name="${name}-${GITHUB_SHA::7}"
fi
if [ "$signing" = debug ]; then
  name="${name}-debug"
fi
# Builds/ belongs to the container's root user, so stage in dist/.
mkdir -p dist
cp "$file" "dist/${name}.${package}"
(cd dist && sha256sum "${name}.${package}" > "${name}.${package}.sha256")
{
  echo "artifact-name=${name}"
  echo "cert-sha256=${sha256}"
} >> "$GITHUB_OUTPUT"
{
  echo "### Quest ${package^^}"
  echo ""
  echo "| | |"
  echo "|---|---|"
  echo "| File | \`${name}.${package}\` |"
  echo "| Version / versionCode | ${VERSION} / ${VERSION_CODE} |"
  echo "| Unity | ${UNITY_VERSION} |"
  echo "| Signed with | ${signing} key: ${dn:-none} |"
  echo "| Certificate SHA-256 | \`${sha256:-none}\` |"
  echo "| ${package^^} SHA-256 | \`$(cut -d ' ' -f 1 "dist/${name}.${package}.sha256")\` |"
  echo ""
  echo "Install an APK on a Quest 3 with docs/SIDELOAD.md."
} >> "$GITHUB_STEP_SUMMARY"
