# Moving sunnybot off .NET 11 preview

On 2026-08-04 this repo went from .NET 7 to **.NET 11 preview 6**
(SDK `11.0.100-preview.6.26359.118`). .NET 11 GA was scheduled for
**2026-11-10**. This is the runbook for cutting over once it ships.

A scheduled cloud routine fires on 2026-11-13 and works through this file.
It lives at https://claude.ai/code/routines and auto-disables after one run.

## 1. Confirm GA actually shipped

```bash
curl -s https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json | head -40
```

Read the channel-level `latest-release` and `latest-sdk`.

A `-preview` or `-rc` suffix on `latest-release` means GA slipped. Stop, and
open an issue saying so, because the one-shot routine has now spent itself and
needs re-arming.

Otherwise keep the exact `latest-sdk` string; step 2 needs it.

## 2. The four edits

| File | Change |
| --- | --- |
| `global.json` | `sdk.version` to the GA `latest-sdk`; drop `allowPrerelease` |
| `Dockerfile` | `sdk:11.0-preview` to `sdk:11.0` |
| `Dockerfile` | `aspnet:11.0-preview` to `aspnet:11.0` |
| `flake.nix` | nixpkgs input off `nixpkgs-unstable` |

Confirm the two image tags exist before relying on them:

```bash
curl -s https://mcr.microsoft.com/v2/dotnet/sdk/tags/list
curl -s https://mcr.microsoft.com/v2/dotnet/aspnet/tags/list
```

The flake is on `nixpkgs-unstable` only because the preview SDK was
unstable-only. Move it to the newest NixOS stable branch whose
`dotnetCorePackages` carries an 11.0 GA SDK, and delete the stale comment above
the input. If no stable branch has it yet, leave it alone and say so in the PR.

Regenerating `flake.lock` needs nix:

```bash
tmp=$(mktemp -d); cp flake.nix "$tmp/"; nix flake lock "path:$tmp"; cp "$tmp/flake.lock" .
```

If nix is unavailable, make the `flake.nix` edit anyway and flag in the PR that
the lock still needs regenerating locally.

## 3. Do not "helpfully" change these

**`TargetFramework` stays `net11.0`.** It is already correct.

**The Six Labors pins are deliberate.** ImageSharp stays on 3.1.x, Drawing on
2.1.x, Fonts on 2.1.x. ImageSharp 4.0 and Drawing 3.0 fail the build outright
without a paid licence key, which this project does not have. There is a
comment in `sunny.csproj` saying so.

**AWSSDK.S3 is on v4**, where empty collections come back as `null` rather than
an empty list. `Program.cs` guards the `ListBuckets` startup check for exactly
that reason. Leave it.

## 4. Verify

`dotnet build` and `dotnet publish -c Release`. Four pre-existing `CS8604`
nullable warnings are expected and are not a regression.

Booting the app end to end needs S3 credentials. A stub that answers
`ListBuckets` with a `ListAllMyBucketsResult` body and returns 200 to `PUT` is
enough to exercise `/`, `/index.html`, `/sunny` and `/slack` without them.

## 5. Open a PR, do not merge

**Never commit to `main`.** `.github/workflows/deploy.yml` deploys to Fly
production on every push to `main`, with no approval gate.

## Afterwards

.NET 11 is **STS, not LTS**. Support ends **2028-11-09**, two years rather than
three. Worth arming a second reminder for mid-2028.
