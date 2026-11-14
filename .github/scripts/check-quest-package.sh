#!/usr/bin/env bash
# Checks the APK or AAB that CommandLineBuild.BuildAndroidQuest wrote to Builds/Android and
# copies it to dist/ with its SHA-256, for the upload step. quest-build.yml runs it after
# the GameCI build step:
#
#   bash .github/scripts/check-quest-package.sh <debug|release> <apk|aab>
#
# The checks use the Android tools of the Unity image the build ran in: aapt2 and apksigner
# for an APK, bundletool, jarsigner and keytool for an AAB. They read the built package, so they
# also see what the Meta XR SDK, Unity and AndroidX add to the manifest at build time.
#   - versionCode and versionName as planned from bundleVersion, and target API level 34;
#   - the package name and the label in every locale (in an AAB, every value of the label's
#     string resource), as set below;
#   - exactly the permissions in expected_permissions below, and none of unwanted_features;
#   - an APK must verify with a v2 signature (the Meta Horizon Store accepts v1 or v2);
#   - a release package must be signed, and not with the Android debug certificate.
#
# Environment: UNITY_VERSION, VERSION, VERSION_CODE, and GitHub's GITHUB_OUTPUT,
# GITHUB_STEP_SUMMARY, GITHUB_REF_TYPE, GITHUB_SHA and RUNNER_TEMP.
# Step outputs: artifact-name, cert-sha256.
# tests/test-check-quest-package.sh runs this script with stub tools, without a build.
set -euo pipefail

signing="${1:-}"
package="${2:-}"
if [[ "$signing" != debug && "$signing" != release ]] || [[ "$package" != apk && "$package" != aab ]]; then
  echo "usage: $0 <debug|release> <apk|aab>" >&2
  exit 2
fi
: "${UNITY_VERSION:?}" "${VERSION:?}" "${VERSION_CODE:?}"

# The package name stays fixed from the first store upload, and the headset and Android's permission
# prompts show the label (AppIdentityTests checks both in the project settings).
app_package=com.CompanionIntelligence.CompanionCapture
app_label='Companion Scan'
# The package must request each of these permissions and no other. Change the list together with
# Assets/Plugins/Android/AndroidManifest.xml, which says why the app needs the Space Setup and
# camera permissions and removes those that SDKs add but the app does not use. Unity's Meta OpenXR
# package adds USE_PASSTHROUGH_CAMERA with the camera permissions, INTERNET reaches the CV server,
# and AndroidX Core adds the last one, a signature permission for the app's own receivers.
expected_permissions=(
  com.oculus.permission.USE_ANCHOR_API
  com.oculus.permission.USE_SCENE
  horizonos.permission.HEADSET_CAMERA
  android.permission.CAMERA
  android.permission.INTERNET
  com.oculus.permission.USE_PASSTHROUGH_CAMERA
  "$app_package.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION"
)
# The app uses controllers only and no eye tracking; the manifest removes these features.
unwanted_features=(oculus.software.handtracking oculus.software.eye_tracking)

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

# Runs inside the Unity image. Prints the manifest values and the signature report, each part
# under an "== <part>" line.
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
    "$aapt2" dump badging "$1" || echo "aapt2 exit code $?"
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
    # Prints <name>=<value> for each value the XPath selects in the manifest. bundletool prints one
    # value per line, and an empty line when the XPath selects nothing.
    xpath() { "$java" -jar "$bundletool" dump manifest --bundle "$1" --xpath "$3" | sed -n "/./s/^/$2=/p"; }
    manifest="$(
      xpath "$1" versionCode /manifest/@android:versionCode
      xpath "$1" versionName /manifest/@android:versionName
      xpath "$1" targetSdkVersion /manifest/uses-sdk/@android:targetSdkVersion
      xpath "$1" package /manifest/@package
      xpath "$1" label /manifest/application/@android:label
      xpath "$1" uses-permission /manifest/uses-permission/@android:name
      xpath "$1" uses-permission-sdk-23 /manifest/uses-permission-sdk-23/@android:name
      xpath "$1" uses-feature //uses-feature/@android:name
    )"
    echo "== manifest"
    echo "$manifest"
    # The label is a reference to a string resource, such as @string/app_name: print the string's
    # value in each locale. bundletool finds a resource by type/name or ID, without "@" or a package.
    label="$(sed -n 's/^label=//p' <<< "$manifest")"
    if [[ "$label" == @* ]]; then
      resource="${label#@}"
      echo "== label resource"
      "$java" -jar "$bundletool" dump resources --bundle "$1" --resource "${resource#*:}" --values
    fi
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

# The package name, the label in each locale as <locale>=<label> lines, and the requested
# permissions and declared features, one per line. Only the manifest and label resource parts of
# the report are read: keytool's part has tab-indented lines too.
section() { sed -n "/^== $1\$/,/^== /{/^== /d;p}" <<< "$report"; }
manifest="$(section manifest)"
label=""
labels=""
permissions=""
features=""
if [ "$package" = apk ]; then
  package_name="$(sed -n "s/^package: name='\([^']*\)'.*/\1/p" <<< "$manifest")"
  # aapt2 resolves the label for the default locale and for each locale the APK has resources in.
  label_re="^application-label(-([^:]+))?:'(.*)'\$"
  permission_re="^uses-permission(-sdk-23)?: name='([^']*)'"
  feature_re="^[[:space:]]*uses-feature(-not-required)?: name='([^']*)'"
  while IFS= read -r line; do
    if [[ $line =~ $label_re ]]; then
      labels+="${BASH_REMATCH[2]:-(default)}=${BASH_REMATCH[3]}"$'\n'
    elif [[ $line =~ $permission_re ]]; then
      permissions+="${BASH_REMATCH[2]}"$'\n'
    elif [[ $line =~ $feature_re ]]; then
      features+="${BASH_REMATCH[2]}"$'\n'
    fi
  done <<< "$manifest"
else
  package_name="$(sed -n 's/^package=//p' <<< "$manifest")"
  permissions="$(sed -n 's/^uses-permission\(-sdk-23\)\{0,1\}=//p' <<< "$manifest")"
  features="$(sed -n 's/^uses-feature=//p' <<< "$manifest")"
  label="$(sed -n 's/^label=//p' <<< "$manifest")"
  if [[ "$label" == @* ]]; then
    # bundletool prints each value of the string as <tab><configuration> - [STR] "<text>".
    value_re=$'^\t(.+) - \\[([A-Z]+)\\] (.*)$'
    locale_re='^locale: "(.*)"$'
    string_re='^"(.*)"$'
    while IFS= read -r line; do
      if [[ $line =~ $value_re ]]; then
        config="${BASH_REMATCH[1]}"
        value_type="${BASH_REMATCH[2]}"
        text="${BASH_REMATCH[3]}"
        if [[ $config =~ $locale_re ]]; then
          config="${BASH_REMATCH[1]}"
        fi
        if [ "$value_type" = STR ] && [[ $text =~ $string_re ]]; then
          text="${BASH_REMATCH[1]}"
        else
          text="[$value_type] $text"
        fi
        labels+="$config=$text"$'\n'
      fi
    done < <(section 'label resource')
  elif [ -n "$label" ]; then
    labels="(default)=$label"$'\n'
  fi
fi

kind="${package^^}"
if [ -z "$package_name" ]; then
  errors+=("Found no package name in the $kind")
elif [ "$package_name" != "$app_package" ]; then
  errors+=("The $kind's package name is $package_name, not $app_package")
fi

if [ -z "$labels" ] && [ -n "$label" ]; then
  errors+=("Found no value for the $kind's label $label")
elif [ -z "$labels" ]; then
  errors+=("Found no application label in the $kind")
else
  if ! grep -q '^(default)=' <<< "$labels"; then
    errors+=("The $kind has no application label for the default locale")
  fi
  # One error for each wrong label, naming the locales that show it. The quotes keep an empty
  # label from being an empty key, which bash rejects.
  declare -A wrong_labels=()
  while IFS= read -r entry; do
    if [ -n "$entry" ] && [ "${entry#*=}" != "$app_label" ]; then
      wrong_labels["'${entry#*=}'"]+=" ${entry%%=*}"
    fi
  done <<< "$labels"
  for text in "${!wrong_labels[@]}"; do
    errors+=("The $kind's label is $text, not '$app_label', in:${wrong_labels[$text]}")
  done
fi

expected="$(printf '%s\n' "${expected_permissions[@]}" | LC_ALL=C sort -u)"
requested="$(sed '/^$/d' <<< "$permissions" | LC_ALL=C sort -u)"
while IFS= read -r permission; do
  errors+=("The $kind requests a permission that is not expected: $permission")
done < <(LC_ALL=C comm -13 <(echo "$expected") <(echo "$requested") | sed '/^$/d')
while IFS= read -r permission; do
  errors+=("The $kind does not request the expected permission $permission")
done < <(LC_ALL=C comm -23 <(echo "$expected") <(echo "$requested") | sed '/^$/d')

if [ -z "$(sed '/^$/d' <<< "$features")" ]; then
  errors+=("Found no uses-feature in the $kind, so its features could not be checked")
fi
for feature in "${unwanted_features[@]}"; do
  if grep -qxF "$feature" <<< "$features"; then
    errors+=("The $kind declares an unwanted feature: $feature")
  fi
done

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
echo "Package checks passed: $app_package, label '$app_label' in every locale, the ${#expected_permissions[@]} expected permissions"
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
