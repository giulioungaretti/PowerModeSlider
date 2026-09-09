# Microsoft Store release preparation

**The PowerModeSlider Store name has not been reserved.** This repository provides
packaging infrastructure, not a reserved product, published listing, or approved
submission. Real Store packages require the identity assigned to this app in
Partner Center. Nothing here submits or publishes an app automatically.

## What is already implemented

The app is a Windows 11 tray utility with three power modes, a reusable acrylic
flyout, dynamic tray icons, keep-awake, and an opt-in **Start with Windows** menu
item. Power-mode changes apply to both AC and battery operation; the displayed
mode is read from AC. Keep-awake keeps both the system and display awake until
disabled or the app exits; it has no timer and is off at a new app launch.

The remaining release work is account/product setup, real-identity packaging,
clean-machine testing, listing assets, declarations, and certification. No UI
rewrite or framework migration is needed.

Driftwave informed the separate Release/MSBuild packaging and downloadable
artifact approach. Its package identity, Store ID, radio/network disclosures,
credentials, and automatic publication workflow are **not** reused. Its release
history is not evidence that this app's submission will succeed.

## 1. Reserve the name and obtain the identity

Open [Partner Center](https://aka.ms/submitwindowsapp) with the intended developer
account. Complete any outstanding account verification and agreements. Choose
**New product > MSIX or PWA app**, check availability, and reserve the intended
name. A name reservation currently lasts three months; complete the first
submission within the reservation period.

In the new product, open **Product management > Product identity**. Copy these
values exactly, including capitalization, spaces, and punctuation:

| Script parameter | GitHub Actions repository variable | Source |
| --- | --- | --- |
| `IdentityName` | `STORE_IDENTITY_NAME` | `Package/Identity/Name` |
| `Publisher` | `STORE_IDENTITY_PUBLISHER` | `Package/Identity/Publisher`, including `CN=` |
| `PublisherDisplayName` | `STORE_PUBLISHER_DISPLAY_NAME` | `Package/Properties/PublisherDisplayName` |
| `DisplayName` | `STORE_DISPLAY_NAME` | The reserved app display name |

These are public identity values, not secrets. The separate **Store ID**, package
family name, and package SID are not substitutes for `IdentityName`. Record the
Store ID for the eventual listing link, but packaging does not need it.

Do not edit the checked-in development identity or associate the project through
a wizard that overwrites it. `Build-StorePackage.ps1` creates a separate manifest
for each Store build. Do not borrow Driftwave's identity or use the development
`CN=gungaretti` publisher. The script rejects missing values and development or
fixture identities. Its offline checks validate syntax, **not** reservation
ownership; compare the generated manifest with Partner Center before upload.

## 2. Build locally

Use Windows, PowerShell 7, the .NET 10 SDK, and a compatible Visual Studio/MSBuild
installation with WinUI/.NET desktop build support and the Windows SDK. A current
Visual Studio Developer PowerShell is suitable. The script also discovers
64-bit MSBuild using `vswhere`, including an installed prerelease VS if no stable
installation is available. It never installs tooling or trusts certificates.
The existing winapp development flow remains available, but `winapp pack` is
not this Store upload/symbol build path.

Run from the repository root after setting the four environment variables above
to the **real values** from the reservation:

```powershell
$identity = @{
    IdentityName = $env:STORE_IDENTITY_NAME
    Publisher = $env:STORE_IDENTITY_PUBLISHER
    PublisherDisplayName = $env:STORE_PUBLISHER_DISPLAY_NAME
    DisplayName = $env:STORE_DISPLAY_NAME
}

.\scripts\Build-StorePackage.ps1 @identity -Version 1.5.0.0 -ValidateOnly
.\scripts\Build-StorePackage.ps1 @identity -Version 1.5.0.0
```

The default builds **x64 and arm64**. Use `-Architecture x64` or
`-Architecture arm64` to build only one. Each architecture gets an isolated
restore, intermediate directory, and output directory, so development and Store
builds cannot reuse each other's XAML code generation or package identity.
Restore uses the normal configured NuGet sources; there is no added feed.

`1.5.0.0` is an example matching the current development package version, not a
chosen first Store release. Specify the intended version explicitly:
`Major.Minor.Build.0`, major 1-65535, minor/build 0-65535, with no leading zeros.
The fourth component must be zero because the Store reserves it. Both
architectures use the same supplied version. There are no date/run-number
increments, and the source manifest is not modified.

Successful outputs for that example are:

```text
artifacts\store\1.5.0.0\x64\PowerModeSlider_1.5.0.0_x64.msixupload
artifacts\store\1.5.0.0\arm64\PowerModeSlider_1.5.0.0_arm64.msixupload
```

Each architecture directory also contains `AppxManifest.xml` extracted from the
actual MSIX and `package-validation.json` with the identity, architecture,
included .NET runtime version, framework dependencies, payload count, and upload
SHA-256. `build\`, `packages\`, and `Package.Store.appxmanifest` are intermediate
outputs, not additional files to submit. All default outputs are under the
ignored `artifacts\` directory.

Existing architecture output directories are rejected rather than cleaned or
silently reused. Move the specific old output aside, or use a fresh root:

```powershell
.\scripts\Build-StorePackage.ps1 @identity -Version 1.5.0.0 `
    -OutputRoot .\artifacts\store-rebuild
```

The build uses Release, explicit RIDs, the SDK's `StoreOnly` packaging mode, and
one unbundled MSIX per architecture. It requires exactly one `.msixupload` per
build, containing an MSIX and its SDK-generated `.appxsym` symbol archive. It
does not select the largest file or fall back to arbitrary old `.msix` files.

The current MSIX SDK no longer supports stripping private PDB information.
Symbols are kept in the upload container for Partner Center diagnostics and
excluded from the installed MSIX; do not describe the symbol archive as
public-only or publish it as a GitHub release asset. Review symbol sharing and
artifact access according to the repository's visibility.

### Validate infrastructure before reservation

This explicit local-only mode exercises the same build and inspection path:

```powershell
.\scripts\Build-StorePackage.ps1 -LocalValidation -Version 1.5.0.0
```

It uses `PowerModeSlider.LocalPackagingValidation`, a publisher and display name
marked as a test, and defaults to `artifacts\store-validation\`. Output
upload filenames start with `LOCAL-VALIDATION-ONLY_` and reports set
`LocalValidationOnly` to `true`. **Never submit these fixtures.** Successful
fixture packaging does not establish a Store identity or certify runtime
behavior. This mode is not exposed by the GitHub workflow.

## 3. Build through GitHub Actions

After reserving the product, set the four repository **variables** listed above
in **Settings > Secrets and variables > Actions > Variables**. No Partner Center
API secrets, private PFX, certificate thumbprint, or Store CLI setup is needed.

Once this change is on the repository's default branch, select
**Actions > Prepare Microsoft Store packages > Run workflow**, choose the
intended ref, and enter a four-component version. The validation job fails
before either architecture build if required identity values or the version
are missing or invalid.

Download both `PowerModeSlider-Store-<version>-<architecture>` Actions artifacts.
Unzip the Actions downloads, then upload the two **`.msixupload` files** into
the **same Partner Center submission**. Do not upload the outer GitHub ZIP,
the JSON report, the loose build directory, a certificate, or an installer
script. The Store can accept separate packages of the same version for different
architectures; no multi-architecture bundle is required here.

This workflow runs only by manual dispatch, has read-only repository
permissions, and uploads artifacts with seven-day retention. It does not
trigger on a tag/release, attach public release assets, call a Store API, or
submit/publish the product. An artifact upload/quota failure is a failed run,
not a successful submission; resolve the quota or use the local build path.

## Signing, OS support, and clean-machine dependencies

| Distribution path | Identity and signing | Runtime model |
| --- | --- | --- |
| Store preparation | Supplied Partner Center identity; unsigned upload; Microsoft signs it after certification | .NET and Windows App SDK included in each package |
| Existing GitHub/sideload release | Existing development identity and shared self-signed certificate | Existing behavior and installer instructions unchanged |
| winapp development | Existing development/loose-package identity | Existing SDK/runtime prerequisites unchanged |

An `.msixupload` is a submission container, **not an installer**. The MSIX inside
this Store upload is intentionally unsigned and is not directly installable as
a trusted retail app. Do not import a certificate or install a fixture as part
of packaging. A paid signing certificate, PFX, CER, or hardware token is not
required for an MSIX Store submission.

Store-only settings are in `PowerModeSlider\Properties\StorePackaging.targets`.
They target `net10.0-windows10.0.22000.0` and set the package minimum to
Windows 11 build 22000. The power APIs require that OS and the app reads a power
mode during startup; advertising Windows 10 would not be appropriate. The
development target framework and sideload manifest are deliberately unchanged.

`SelfContained=true` and `WindowsAppSDKSelfContained=true` include both runtimes;
trimming stays **disabled** to preserve WinUI XAML/`x:Bind` types. ReadyToRun stays
enabled for Release. There is no single-file publish or Native AOT migration.
The package inspector requires the .NET host/runtime, native WinUI, application
assemblies, and all six tray icons, and checks the architecture of the apphost,
CLR, and WinUI binaries. It rejects unexpected runtime framework dependencies.
Any supported VCLibs framework dependency is recorded in the report and would
be provisioned by the Store, not by asking customers to install developer tools.

The intended customer prerequisite is Windows 11 on x64 or ARM64, **not** a
separately installed .NET SDK/runtime or Windows App Runtime. Confirm this with
a real-identity Release build on clean machines before submission; a successful
package build on a developer PC is not proof of startup on a clean machine.
Cross-compiling ARM64 is not an ARM64 runtime test. Self-contained deployment
makes packages larger and requires rebuilding/releasing the app to distribute
runtime security updates.

The Store and sideload identities are different package families, so one is
not an automatic update of the other. Do not run both copies during testing:
two copies can produce two tray icons. Plan the user-facing transition before
announcing a Store release.

## 4. First-submission checklist

- [ ] **Account and identity:** finish developer account checks, reserve the name,
  record the real identity/Store ID, and compare every generated identity field
  with Product identity. No reservation or account setting is created by this repo.
- [ ] **Version and packages:** choose the version, build both architectures,
  inspect their manifests/reports, and upload both `.msixupload` files to one
  draft submission. Confirm Windows 11 desktop applicability and runtime
  dependencies in Partner Center.
- [ ] **Clean-machine and hardware testing:** test x64 and native ARM64 with no
  developer runtimes installed, the declared minimum OS where available, and
  current Windows 11. Exercise launch, all power modes on AC/battery, reopen and
  light-dismiss, DPI/multi-monitor behavior, normal/awake tray icons, and Exit.
  Confirm keep-awake disables on Exit and remains off at the next launch.
  Power-mode selection is a Windows setting and is not reset on app exit.
- [ ] **Startup and tray behavior:** verify startup is initially disabled; opt in
  through **Start with Windows**, sign out/in, disable it, and verify Task
  Manager/user/policy blocks are respected. Capture the tray overflow case.
  Do not assume a hidden main window means startup failed.
- [ ] **Certification:** use a Store private flight or an explicitly authorized
  test-signed copy on a test machine for install/runtime validation; do not
  alter the unsigned submission artifact in place. Run the Windows App
  Certification Kit and review results before submission. Installation and
  certificate trust are separate, intentional test-machine actions.
- [ ] **Listing and screenshots:** write a concise description of the actual
  power slider, AC/DC behavior, keep-awake, and optional startup. Choose the
  appropriate category, languages, markets, and pricing in Partner Center;
  these are owner decisions, not defaults supplied here. Capture current
  Windows 11 screenshots showing the flyout, awake state, tray/overflow, and
  context menu using Partner Center's required dimensions. `docs\flyout.png`
  is a useful reference image, not a complete approved Store screenshot set.
  Review app logos at all supplied scales.
- [ ] **Privacy and support:** provide real, publicly accessible HTTPS privacy
  and support/contact URLs controlled by the publisher. Check them signed out.
  No final privacy policy, legal entity/contact details, hosting URL, or
  retention promise is invented here. Review the source-grounded notes below
  and the actual dependency/diagnostic behavior before publishing a policy.
- [ ] **Declarations and age rating:** complete the actual content/age-rating
  questionnaire and applicable product declarations. The checked-in app has
  no account, ads, commerce, chat, user-generated content, or radio streaming
  feature. Do not reuse Driftwave's answers or infer legal/privacy answers
  solely from a manifest capability.
- [ ] **Restricted capability:** supply a truthful `runFullTrust` justification
  and reviewer instructions (draft below). It does not mean "run as
  administrator"; no elevation, service, or driver is requested by this app.
- [ ] **Submission and release control:** inspect all Partner Center validation
  messages, select the intended manual/scheduled publication option, and
  submit for certification deliberately. Packaging does not choose a release
  date or make the app publicly available.

### Source-grounded privacy review notes (not a published policy)

The application source calls local Windows APIs to read/change power modes,
maintain an execution-state request, display tray/flyout UI, and query/change its
opt-in startup task. The low-level mouse hook is used for flyout light-dismiss,
not an analytics feature. No application-authored HTTP client, telemetry service,
account flow, advertising, or event/content persistence is present in the
checked-in application source.

These observations do not establish what Windows, the Microsoft Store,
dependencies, or a publisher's support channel may collect. Review those
separately and describe them accurately in the final privacy policy. Do not
promise that no data is ever collected by any party. If the release adds
diagnostics or network behavior, revisit both the policy and declarations.

### Draft capability justification and reviewer notes

> PowerModeSlider is a WinUI desktop application that runs in the current user's
> notification area. It needs `runFullTrust` for its Win32 tray integration and
> flyout light-dismiss hook, the documented
> `PowerGet/SetUserConfiguredAC/DCPowerMode` APIs, and `SetThreadExecutionState`.
> The slider changes the user's AC and battery power-mode settings. Keep-awake
> prevents automatic system/display idle sleep while enabled and is released
> when disabled or the process exits. The app does not request elevation or
> install a service or driver. Startup is disabled by default and is enabled
> only through the user's "Start with Windows" selection; Windows startup
> policy/user controls remain authoritative.
>
> After launch, there is intentionally no visible main window. Find the
> PowerModeSlider icon in the Windows 11 notification area or its overflow
> menu. Left-click it to open the power slider and coffee/keep-awake toggle.
> Right-click for "Start with Windows" and "Exit". Test only one copy at a time.
> Keep-awake does not override an explicit user Sleep command and is not a
> scheduled/timed feature.

## References

- [Reserve an app name](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/reserve-your-apps-name)
- [Product identity fields](https://learn.microsoft.com/en-us/windows/apps/publish/view-app-identity-details)
- [Store package requirements, signing, and version rules](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [MSIX packages, bundles, and upload containers](https://learn.microsoft.com/en-us/windows/msix/package/packaging-uwp-apps)
- [Windows App SDK self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
- [Windows App Certification Kit](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit)
