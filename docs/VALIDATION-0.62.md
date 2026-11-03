# Ceeker 0.62.0-preview.1 — smaller installer

Validated on the development Windows machine, 2026-11-03.

## Passed

- Full core regression run completed successfully (`artifacts/core-062.log`). Added optional-model presence tests reject absent and empty packs and accept a complete generated presence fixture. This is a presence check, not a model inference test.
- Release publish for Standard and its CLI completed successfully (`artifacts/package-062-standard.log`). Standard excludes the generated Models directory before manifest/archive creation. Runtime binaries and OCR models remain included for offline, self-contained use.
- Standard archive checksum and all 780 payload manifest entries verified (`artifacts/package-verification.json`); desktop helper components, OCR/PDF dependencies and startup resources present; no Models folder. Packaged CLI help launched successfully.
- Standard bundled CLI returned four expected matches with a display limit of two from the generated library (`artifacts/standard-actions-062.json`).
- Native extracted Standard launch and search: with SemanticSearch previously enabled in isolated test preferences, `alpha` returned Alpha.txt and displayed the missing-optional-pack status, without a model-load failure (`artifacts/standard-smoke-062.log`). Normal user library/installation were not used or modified.
- Separate AI pack: source files were checked against model-hashes.json with size, hash, path-containment and unexpected-file checks. Inno Setup compilation completed successfully (`artifacts/ai-pack-062.log`). Pack installer has an independent uninstall location and checks for Ceeker.App.exe at its destination. Models use the existing local inference format and require no network after installation.

## Release outputs

Both installers compiled successfully and their final SHA-256 hashes were independently verified (`artifacts/installers-062-check.json`).

| Download | Bytes | Decimal MB | SHA-256 |
| --- | ---: | ---: | --- |
| Ceeker-0.62.0-preview.1-Setup-x64-Standard.exe | 135505491 | 135.5 | 8C5AF3E7A080089036EF86F06DAC6E84998DC07E0F1D8F0DE1DADD2E99331574 |
| Ceeker-0.62.0-preview.1-AI-Pack-x64.exe | 195943924 | 195.9 | D280D73A576261A9C7593A129937E5B9DBFED0800D359E09BB963BA9A016561B |

Standard is 58.9% smaller than the retained 329440218-byte full offline 0.61 installer. Downloading both new components totals 331449415 bytes; splitting saves initial download size for users who do not need AI similarity, not total bytes when both are downloaded. Neither new installer was installed over the user's application.

## Limits and remaining checks

Manual AI-pack installation/uninstallation, post-install AI inference, Full-to-Standard and Standard-to-Full upgrades, custom installation paths and clean-machine testing remain pending. Standard over Full deliberately leaves existing models intact; removing the separate pack is a separate Windows Apps action. No automatic AI download is active until a hosting address is configured. The previous 0.61 native UI, DPI, touch and clean-machine validation limits remain applicable. Background validation automation remains paused. No aggressive framework/runtime trimming was used.
