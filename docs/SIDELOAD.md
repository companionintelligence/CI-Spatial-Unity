# Installing the Quest APK (sideloading)

This guide installs a Companion Scan build on a Meta Quest 3 from a Windows,
macOS or Linux computer, either with Meta Quest Developer Hub or with `adb`.
It applies to APKs from the [Quest APK Build](../.github/workflows/quest-build.yml)
workflow, from a GitHub Release, and from a local Unity build.

| | |
|---|---|
| App name in the headset library | `CompanionCapture` |
| Package name | `com.CompanionIntelligence.CompanionCapture` |

Both come from the Android Player Settings in `ProjectSettings/ProjectSettings.asset`.
If they change there, list the installed package with
`adb shell pm list packages | grep -i companion`
(PowerShell: `adb shell pm list packages | Select-String companion`).

## 1. Get the APK

**From a CI run.** On GitHub, open **Actions → Quest APK Build**, pick a
successful run and download the `CompanionScan-<version>` artifact (you need to
be signed in to GitHub). It is a zip holding `CompanionScan-<version>.apk` and
`CompanionScan-<version>.apk.sha256`. Artifacts are kept for 7 days. With the
`gh` CLI, from a clone of this repository:

```bash
gh run list --workflow quest-build.yml --status success --limit 5
gh run download <run-id> --dir quest-apk   # the APK and its .sha256 end up under quest-apk/
```

**From a release.** Pushing a `v*` tag attaches the same two files to a draft
GitHub Release. Drafts are visible to maintainers only until they are published.

```bash
gh release download <tag> --pattern 'CompanionScan-*'
```

**From your own Unity build.** In the editor, **CompanionScan → Build Quest APK**
writes `Builds/Quest/CompanionScan.apk`. **CompanionScan → Build and Run Quest APK**
also installs it on the connected headset, which skips the rest of this guide.
Both are signed with your own debug key. A release-signed build comes from
`CommandLineBuild.BuildAndroidQuest -release` (README, "Release builds from the
command line"), which writes `Builds/Android/CompanionCapture.apk`.

**Check the checksum** of CI and release builds before installing:

```bash
sha256sum -c CompanionScan-<version>.apk.sha256            # Linux
shasum -a 256 -c CompanionScan-<version>.apk.sha256        # macOS
```

```powershell
$apk = 'CompanionScan-<version>.apk'
(Get-FileHash $apk -Algorithm SHA256).Hash -eq (Get-Content "$apk.sha256").Split(' ')[0]   # True
```

## 2. Prepare the headset (once)

1. **Developer account.** Join (or create) a developer team on the Meta Horizon
   Developer Dashboard and verify the account there (SMS two-factor or a
   payment method). Developer Mode is not available otherwise.
2. **Developer Mode.** In the Meta Horizon app on your phone, tap the headset
   icon, select your headset, then **Headset Settings → Developer Mode** and
   turn **Developer Mode** on.
3. **Windows only:** install the Oculus ADB Drivers from Meta's developer
   downloads (unzip, right-click the `.inf` file, **Install**) so that `adb`
   can talk to the headset.
4. **Connect** the headset with a USB-C data cable, put it on and accept the USB
   debugging prompt with **Always allow from this computer**.

## 3a. Install with Meta Quest Developer Hub

Meta Quest Developer Hub (MQDH; some newer releases call it Meta VR Developer
Hub) is Meta's desktop app for Windows and macOS.

1. Install it from the Meta developer downloads and sign in with the developer
   account from step 2.
2. Connect the headset. It shows up in **Device Manager**.
3. Under **Apps**, click **Add Build** and choose the `.apk`, or drag the `.apk`
   onto the MQDH window and drop it on the headset. MQDH installs it and
   replaces an installed build with the same package name.

If the install is refused because the signature or version differs, remove the
installed build first (see [Updating or replacing a build](#5-updating-or-replacing-a-build)).

## 3b. Install with adb

1. Install the Android SDK
   [Platform-Tools](https://developer.android.com/tools/releases/platform-tools)
   and put `adb` on your `PATH`.
2. Check the connection:

   ```bash
   adb devices
   ```

   The headset must be listed with the state `device`. `unauthorized` means the
   USB debugging prompt in the headset has not been accepted yet.
3. Install, replacing any earlier build signed with the same key and keeping
   its data:

   ```bash
   adb install -r CompanionScan-<version>.apk
   ```

   With more than one device connected, add `-s <serial>` (the first column of
   `adb devices`) to every `adb` command.
4. Optionally start the app from the computer:

   ```bash
   adb shell monkey -p com.CompanionIntelligence.CompanionCapture -c android.intent.category.LAUNCHER 1
   ```

**Without a cable.** While the headset is still connected by USB, read its IP
address from `adb shell ip route` (the address after `src`), then run
`adb tcpip 5555` and `adb connect <headset-ip>:5555`, and unplug the cable.
`adb disconnect` ends the wireless session.

## 4. Start the app

1. On the headset, open the **Library** and look under **Unknown Sources**:
   sideloaded apps are listed there, not with store apps.
2. Run **Space Setup** for the room if you have not done so. Meta Scene
   Understanding (pipeline A) reads the room model it creates.
3. Allow the permissions the app asks for. On first launch it asks for the
   headset cameras (`horizonos.permission.HEADSET_CAMERA`) and camera access
   (`android.permission.CAMERA`); frame capture starts once both are allowed.
   Spatial data (`com.oculus.permission.USE_SCENE`) is asked for at the first
   capture. A dismissed prompt is shown again the next time the app needs the
   permission. If you denied one, turn it back on in the headset's app
   permission settings (Meta Help Center: [Manage headset camera permissions](https://www.meta.com/help/quest/511837681585017/))
   and capture picks it up, or uninstall and reinstall the app to be asked again.
4. The CV server pipeline (pipeline B) needs the headset to reach the CV server
   configured in the build. See [Quick Start](../README.md#quick-start), steps 1
   and 2.

## 5. Updating or replacing a build

An update installs in place, keeping the app's data, only when the new APK is
signed with the same key and does not lower the version code.

- **Different signing key.** CI builds made without the `ANDROID_KEYSTORE_*`
  repository secrets are signed with a debug key generated for that run, and
  local editor builds use your own debug key. Installing over a build signed
  with another key fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`.
- **Lower version code.** Installing an older build over a newer one fails with
  `INSTALL_FAILED_VERSION_DOWNGRADE` (`adb install -d` only helps for
  debuggable builds).

In both cases, uninstall the old build first. **Uninstalling deletes the app's
data on the headset**, including glTF exports under `files/exports`, so copy
them off first:

```bash
adb pull /sdcard/Android/data/com.CompanionIntelligence.CompanionCapture/files/exports ./quest-exports
adb uninstall com.CompanionIntelligence.CompanionCapture
adb install CompanionScan-<version>.apk
```

## Troubleshooting

| Symptom | What to do |
|---|---|
| `adb devices` lists nothing | Use a USB-C cable that carries data, check that Developer Mode is on, install the Oculus ADB Drivers on Windows, then run `adb kill-server` and try again. |
| Headset listed as `unauthorized` | Put the headset on and accept the USB debugging prompt. If none appears, unplug and reconnect the cable. |
| `more than one device/emulator` | Add `-s <serial>` from `adb devices` to the command. |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` or `INSTALL_FAILED_VERSION_DOWNGRADE` | Uninstall the installed build first, as in section 5. |
| `INSTALL_FAILED_INSUFFICIENT_STORAGE` | Free up space on the headset. |
| App missing from the Library | Switch the Library to **Unknown Sources**. |
| App starts but CV results never arrive | Check that the headset can reach the configured CV server from its network. Watch the app log with `adb logcat -s Unity`. |

Meta's documentation covers the same ground in more depth:
[adb with Quest](https://developers.meta.com/horizon/documentation/unity/ts-adb/),
[device setup and Developer Mode](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/)
and [installing builds with Developer Hub](https://developers.meta.com/horizon/documentation/native/android/ts-odh-deploy-build/).
