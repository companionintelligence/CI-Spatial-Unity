#!/usr/bin/env bash
# Tests check-quest-package.sh without a build or the Unity image. Stubs of docker, aapt2, java
# (for apksigner and bundletool), jarsigner and keytool answer from the files in fixtures/:
#   - aapt2-badging.txt is the "aapt2 dump badging" output of a debug APK of the app;
#   - bundletool-manifest.tsv and bundletool-resources.txt hold the same manifest the way
#     bundletool 1.17.2, the version in Unity's Android tools, prints it for an AAB;
#   - apksigner-verify.txt, jarsigner-verify.txt and keytool-printcert.txt are a debug signature
#     in the output formats of those tools, with made-up digests.
# Each case changes a copy of the fixtures the way a wrong build would, runs the script, and checks
# its exit code and its ::error:: lines.
#
#   bash .github/scripts/tests/test-check-quest-package.sh
#
# Needs bash 4.4 or later, awk, and GNU sed and coreutils, like the script it tests (Git Bash on
# Windows has them).
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
check="$here/../check-quest-package.sh"
# cd and pwd give a path without a drive letter in Git Bash on Windows too: it goes on PATH.
work="$(cd "$(mktemp -d)" && pwd)"
trap 'rm -rf "$work"' EXIT

# The stubs go where the inspect script looks for the tools in the Unity image, and docker on PATH.
export STUB_ROOT="$work/root" STUB_CASE="$work/case"
android="$STUB_ROOT/opt/unity/Editor/Data/PlaybackEngines/AndroidPlayer"
mkdir -p "$work/bin" "$android/SDK/build-tools/34.0.0/lib" "$android/OpenJDK/bin" "$android/Tools"
install_stub() {
  tr -d '\r' < "$here/stubs/$1" > "$2/$1"
  chmod +x "$2/$1"
}
install_stub docker "$work/bin"
install_stub aapt2 "$android/SDK/build-tools/34.0.0"
install_stub java "$android/OpenJDK/bin"
install_stub jarsigner "$android/OpenJDK/bin"
install_stub keytool "$android/OpenJDK/bin"
touch "$android/SDK/build-tools/34.0.0/lib/apksigner.jar" "$android/Tools/bundletool-all-1.17.2.jar"
export PATH="$work/bin:$PATH"
# The fixtures are versionCode 1000, versionName 0.1.0.
export UNITY_VERSION=6000.6.9f1 VERSION=0.1.0 VERSION_CODE=1000
export GITHUB_REF_TYPE=branch GITHUB_SHA=0123456789abcdef0123456789abcdef01234567
cert_sha256=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef

# Starts a case with a copy of the fixtures.
fresh() {
  rm -rf "$STUB_CASE"
  mkdir -p "$STUB_CASE"
  local fixture name
  for fixture in "$here"/fixtures/*; do
    name="${fixture##*/}"
    tr -d '\r' < "$fixture" > "$STUB_CASE/$name"
  done
}
# edit <fixture> <sed script>
edit() {
  sed "$2" "$STUB_CASE/$1" > "$STUB_CASE/$1.new"
  mv "$STUB_CASE/$1.new" "$STUB_CASE/$1"
}
# add_after <fixture> <awk regex> <line>: adds the line after the first line that matches. awk
# turns \t in both into a tab.
add_after() {
  awk -v re="$2" -v line="$3" '{ print } !done && $0 ~ re { print line; done = 1 }' "$STUB_CASE/$1" > "$STUB_CASE/$1.new"
  mv "$STUB_CASE/$1.new" "$STUB_CASE/$1"
}
# Signs the case with the release key that README.md names instead of the debug key.
release_key() {
  edit apksigner-verify.txt 's/CN=Android Debug, O=Android, C=US/CN=Companion Intelligence, O=LifeScope Inc./'
  edit keytool-printcert.txt 's/CN=Android Debug, O=Android, C=US/CN=Companion Intelligence, O=LifeScope Inc./'
}

runs=0
failed=0
# check_case <name> <debug|release> <apk|aab> <exit code> [error]...
# Runs the script on the current case. Each error must be an ::error:: line of its own (one ending
# in "..." only starts the line), and there must be no other ::error:: line. A case that passes
# must also stage the package in dist/ and set the step outputs.
check_case() {
  local name="$1" signing="$2" kind="$3" want="$4"
  shift 4
  runs=$((runs + 1))
  local run="$work/runs/$runs"
  mkdir -p "$run/Builds/Android" "$run/temp"
  echo "not a real $kind" > "$run/Builds/Android/CompanionScan.$kind"
  local output status=0
  output="$(cd "$run" && GITHUB_OUTPUT="$run/output" GITHUB_STEP_SUMMARY="$run/summary" RUNNER_TEMP="$run/temp" \
    bash "$check" "$signing" "$kind" 2>&1)" || status=$?

  local errors count text problems=()
  errors="$(sed -n 's/^::error:://p' <<< "$output")"
  count="$(grep -c . <<< "$errors" || true)"
  if [ "$status" -ne "$want" ]; then
    problems+=("exit code $status, expected $want")
  fi
  for text in "$@"; do
    if [[ "$text" == *... ]]; then
      if ! awk -v start="${text%...}" 'index($0, start) == 1 { found = 1 } END { exit !found }' <<< "$errors"; then
        problems+=("no error starting: ${text%...}")
      fi
    elif ! grep -qxF -- "$text" <<< "$errors"; then
      problems+=("no error: $text")
    fi
  done
  if [ "$count" -ne "$#" ]; then
    problems+=("$count errors, expected $#")
  fi
  if [ "$want" -eq 0 ]; then
    local artifact="CompanionScan-$VERSION-${GITHUB_SHA::7}"
    if [ "$signing" = debug ]; then artifact+="-debug"; fi
    if ! grep -qx "artifact-name=$artifact" "$run/output" 2> /dev/null; then
      problems+=("no step output artifact-name=$artifact")
    fi
    if ! grep -qx "cert-sha256=$cert_sha256" "$run/output" 2> /dev/null; then
      problems+=("no step output cert-sha256=$cert_sha256")
    fi
    if [ ! -s "$run/dist/$artifact.$kind" ] || [ ! -s "$run/dist/$artifact.$kind.sha256" ]; then
      problems+=("dist/$artifact.$kind or its .sha256 file is missing")
    fi
  fi

  if [ "${#problems[@]}" -eq 0 ]; then
    echo "ok $runs - $name"
  else
    failed=$((failed + 1))
    echo "not ok $runs - $name"
    printf '    %s\n' "${problems[@]}"
    local lines
    mapfile -t lines <<< "$output"
    printf '    | %s\n' "${lines[@]}"
  fi
}

permission_error="requests a permission that is not expected:"

fresh
check_case "APK: the debug build passes" debug apk 0

fresh
release_key
check_case "APK: a release build passes" release apk 0

fresh
check_case "APK: a release build signed with the debug key fails" release apk 1 \
  "The release APK is signed with the Android debug certificate"

fresh
add_after aapt2-badging.txt "^uses-permission: name='android.permission.INTERNET'" \
  "uses-permission: name='com.oculus.permission.HAND_TRACKING'"
check_case "APK: HAND_TRACKING fails" debug apk 1 \
  "The APK $permission_error com.oculus.permission.HAND_TRACKING"

fresh
add_after aapt2-badging.txt "uses-feature: name='com.oculus.feature.PASSTHROUGH'" \
  "  uses-feature-not-required: name='oculus.software.handtracking'"
check_case "APK: the hand tracking feature fails" debug apk 1 \
  "The APK declares an unwanted feature: oculus.software.handtracking"

fresh
edit aapt2-badging.txt "s/^application-label-fr:'Companion Scan'\$/application-label-fr:'Companion Capture'/"
check_case "APK: a wrong label in one locale fails" debug apk 1 \
  "The APK's label is 'Companion Capture', not 'Companion Scan', in: fr"

fresh
edit aapt2-badging.txt "s/^\(application-label[^:]*\):'Companion Scan'\$/\1:'Companion Capture'/; s/label='Companion Scan'/label='Companion Capture'/"
check_case "APK: the old label fails" debug apk 1 \
  "The APK's label is 'Companion Capture', not 'Companion Scan', in: (default) af am ar as..."

fresh
edit aapt2-badging.txt "/^application-label/d"
check_case "APK: no label fails" debug apk 1 \
  "Found no application label in the APK"

fresh
edit aapt2-badging.txt "/^uses-permission: name='android.permission.INTERNET'\$/d"
check_case "APK: a missing permission fails" debug apk 1 \
  "The APK does not request the expected permission android.permission.INTERNET"

fresh
add_after aapt2-badging.txt "^uses-permission: name='android.permission.INTERNET'" \
  "uses-permission-sdk-23: name='android.permission.RECORD_AUDIO'"
check_case "APK: a permission for API 23 and later fails too" debug apk 1 \
  "The APK $permission_error android.permission.RECORD_AUDIO"

fresh
edit aapt2-badging.txt 's/com\.CompanionIntelligence\.CompanionCapture/com.CompanionIntelligence.CompanionScan/g'
check_case "APK: another package name fails" debug apk 1 \
  "The APK's package name is com.CompanionIntelligence.CompanionScan, not com.CompanionIntelligence.CompanionCapture" \
  "The APK $permission_error com.CompanionIntelligence.CompanionScan.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION" \
  "The APK does not request the expected permission com.CompanionIntelligence.CompanionCapture.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION"

fresh
check_case "AAB: the debug build passes" debug aab 0

fresh
release_key
check_case "AAB: a release build passes" release aab 0

fresh
check_case "AAB: a release build signed with the debug key fails" release aab 1 \
  "The release AAB is signed with the Android debug certificate"

fresh
add_after bundletool-manifest.tsv 'android[.]permission[.]INTERNET$' \
  '/manifest/uses-permission/@android:name\tcom.oculus.permission.HAND_TRACKING'
check_case "AAB: HAND_TRACKING fails" debug aab 1 \
  "The AAB $permission_error com.oculus.permission.HAND_TRACKING"

fresh
add_after bundletool-manifest.tsv 'com[.]oculus[.]feature[.]PASSTHROUGH$' \
  '//uses-feature/@android:name\toculus.software.handtracking'
check_case "AAB: the hand tracking feature fails" debug aab 1 \
  "The AAB declares an unwanted feature: oculus.software.handtracking"

fresh
add_after bundletool-resources.txt '^\t[(]default[)]' '\tlocale: "fr" - [STR] "Companion Capture"'
check_case "AAB: a wrong label in one locale fails" debug aab 1 \
  "The AAB's label is 'Companion Capture', not 'Companion Scan', in: fr"

fresh
edit bundletool-resources.txt 's/"Companion Scan"/"Companion Capture"/'
check_case "AAB: the old label fails" debug aab 1 \
  "The AAB's label is 'Companion Capture', not 'Companion Scan', in: (default)"

fresh
edit bundletool-resources.txt 's/(default)/locale: "en"/'
check_case "AAB: a label without a default value fails" debug aab 1 \
  "The AAB has no application label for the default locale"

fresh
edit bundletool-manifest.tsv 's#@string/app_name$#@string/missing#'
check_case "AAB: a label that names no resource fails" debug aab 1 \
  "Found no value for the AAB's label @string/missing"

fresh
edit bundletool-manifest.tsv 's#@string/app_name$#@com.CompanionIntelligence.CompanionCapture:string/app_name#'
check_case "AAB: a label reference with the package name passes" debug aab 0

fresh
edit bundletool-manifest.tsv 's#@string/app_name$#Companion Scan#'
check_case "AAB: a label that is not a reference passes" debug aab 0

fresh
edit bundletool-manifest.tsv '/android[.]permission[.]INTERNET$/d'
check_case "AAB: a missing permission fails" debug aab 1 \
  "The AAB does not request the expected permission android.permission.INTERNET"

fresh
add_after bundletool-manifest.tsv 'android[.]permission[.]INTERNET$' \
  '/manifest/uses-permission-sdk-23/@android:name\tandroid.permission.RECORD_AUDIO'
check_case "AAB: a permission for API 23 and later fails too" debug aab 1 \
  "The AAB $permission_error android.permission.RECORD_AUDIO"

fresh
edit bundletool-manifest.tsv 's/com\.CompanionIntelligence\.CompanionCapture/com.CompanionIntelligence.CompanionScan/g'
check_case "AAB: another package name fails" debug aab 1 \
  "The AAB's package name is com.CompanionIntelligence.CompanionScan, not com.CompanionIntelligence.CompanionCapture" \
  "The AAB $permission_error com.CompanionIntelligence.CompanionScan.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION" \
  "The AAB does not request the expected permission com.CompanionIntelligence.CompanionCapture.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION"

echo "$((runs - failed)) of $runs cases passed"
[ "$failed" -eq 0 ]
