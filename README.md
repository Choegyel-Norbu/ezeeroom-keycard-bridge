# ezeeroom-keycard-bridge

Local Windows service that sits between **ezeeroom-web** (browser on the hotel front-desk PC)
and the **ProUSB K9-MF key-card encoder** (USB, driven by the vendor's 32-bit `proRFL.dll`).
ezeeroom (cloud) never talks to the encoder directly; the browser calls this bridge on
`http://127.0.0.1:<port>` and the bridge reports events to ezeeroom-api asynchronously.

Design reference: `../docs/KEYCARD_IMPLEMENTATION_GUIDE.md` (this repo implements Phase 1)
and `../docs/proUSB_Integration_Guide.md` (vendor system reference).

## ⚠️ Status: scaffold — Gates 1 and 2 have NOT been run

Per the implementation guide (Phase 0), no real DLL integration is allowed before:

- **Gate 1** — prove `proRFL.dll` works against a real door at the hotel.
- **Gate 2** — decode the lock-number and validity-time formats from vendor-issued cards,
  and prove the HTTPS-page → localhost path in the hotel's browser.

Until both pass, `EncoderMode` stays `"stub"` (the default). `ProRflEncoder` is a shell:
its P/Invoke signatures are provisional and every card operation throws
`NOT_IMPLEMENTED_PENDING_GATE2`. The stub mode is itself useful — it is exactly the
"static stub bridge" Gate 2 calls for to validate CORS/PNA/token from the deployed web app.

## Architecture (one PC, three processes)

```
ezeeroom-web (browser) ──HTTP──▶ this bridge (Windows service, x86)
                                    │  proRFL.dll (P/Invoke)      ──▶ K9-MF encoder ──▶ card
                                    │  SQLite write-ahead journal
                                    └──HTTPS (background, retry)  ──▶ ezeeroom-api
```

Invariants (from the design principles — these override convenience):

1. Check-in works with the internet down: issuance is local; cloud sync is async.
2. Every write is verified by a mandatory read-back before the flow reports success.
3. Journal before and after every DLL call; the journal sync — not the browser — is the
   system of record (API dedupes by event UUID).
4. Guest cards only. Master/floor/building/emergency cards are never exposed here.
5. Binds to 127.0.0.1 only; exact-match `Origin` allowlist; `X-Bridge-Token` on every request.

## HTTP API (`/v1`, localhost only)

| Endpoint | Action |
|---|---|
| `GET /v1/status` | Encoder connected, DLL version, pending journal count (never queues behind card ops) |
| `GET /v1/version` | Bridge + DLL versions |
| `POST /v1/cards/issue` | `{lockRoomNo, validFrom, validUntil, bookingRef, operator}` → write + verify → `{cardData, verified: true}` |
| `POST /v1/cards/read` | Read the card on the encoder |
| `POST /v1/cards/erase` | Read (capture) → erase → confirm blank |

All requests need `X-Bridge-Token`. Errors carry stable codes the UI maps to operator
instructions: `NO_CARD`, `ENCODER_DISCONNECTED`, `VERIFY_MISMATCH`, `ERASE_NOT_CONFIRMED`,
`INVALID_TOKEN`, `TOKEN_NOT_PROVISIONED`, `NOT_IMPLEMENTED_PENDING_GATE2`.

## Run locally (stub mode, any OS)

```bash
cd src/Ezeeroom.KeycardBridge
dotnet run                                   # listens on http://127.0.0.1:17800
# in another shell, provision a dev token once:
dotnet run -- set-token dev-token
curl -H "X-Bridge-Token: dev-token" http://127.0.0.1:17800/v1/status
```

Requires the .NET 8 SDK. On macOS/Linux the token falls back to a plaintext file
(DPAPI is Windows-only) — stub/dev use only.

## Configuration (`appsettings.json`, section `Bridge`)

| Key | Meaning |
|---|---|
| `Port` | Localhost port (default 17800) |
| `AllowedOrigin` | Exact ezeeroom-web origin, e.g. `https://app.ezeeroom.bt` |
| `EncoderMode` | `stub` or `prorfl` (only after Gates 1/2) |
| `DllDirectory` | Folder with `proRFL.dll` + companion DLLs |
| `ProUsbHotelId` | Vendor hotel id (fill during Gate 2) |
| `MaxIssuesPerMinute` | Issue rate limit (default 6) |
| `Api.BaseUrl` | ezeeroom-api base URL; empty disables journal sync |
| `Api.HotelId` | ezeeroom hotel id attached to synced events |

The shared token is NOT in config — it is DPAPI-protected (machine scope):
`EzeeroomKeycardBridge.exe set-token <token>` writes
`%ProgramData%\EzeeroomKeycardBridge\token.bin`. The journal SQLite db lives beside it.

## Build the Windows installer

On a Windows machine with the .NET 8 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
.\build\publish.ps1                      # self-contained win-x86 publish
iscc .\installer\EzeeroomKeycardBridge.iss   # → installer\output\ezeeroom-keycard-bridge-0.1.0-setup.exe
```

The installer registers the auto-start Windows service `EzeeroomKeycardBridge`.
Post-install, once per machine: run `set-token`, edit `appsettings.json`, restart the service.

## What is deliberately NOT here (guide §0.5 / §2.4)

Staff/master/emergency card issuance, door-log (S70) import, api↔bridge WebSocket,
auto-update/telemetry, any status state machine. The vendor software stays installed
and owns everything this bridge doesn't do.

## Before production (tracked TODOs)

- [ ] Gate 1 + Gate 2 executed; field reference (guide §4) filled in.
- [ ] `ProRflNative` signatures confirmed against SDK docs; `ProRflEncoder` implemented.
- [ ] Validity-time timezone/format handling per Gate 2 findings (currently ISO-8601 passthrough).
- [ ] Bridge→api authentication for journal sync (API contract TBD).
- [ ] Installer: ACL tightening on `%ProgramData%\EzeeroomKeycardBridge`, token generation step.
- [ ] Service recovery options (restart on failure) + post-Windows-Update auto-start check (guide §5.3).
