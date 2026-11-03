# Ceeker 0.61.0-preview.1 validation

Validated on the development Windows machine on 2026-11-03. This is a preview, not a clean-machine certification.

## Passed

- Release application build: zero warnings/errors (`artifacts/build-061-final.log`). Final publish also completed successfully, including subsequent panel-fit/theme and compact folder-summary changes (`artifacts/package-061.log`).
- Automation helper Release build: zero warnings/errors (`artifacts/actions-061.log`).
- Full core regression executable completed successfully (`artifacts/core-061-final.log`, 32 output lines). Coverage includes search grammar/date/size/phrase/ZIP aliases, tags and indexed document formats, duplicates and retained reviews, scoped scans, cleanup revalidation, similarity, watcher updates, thumbnail cache bounds, cancellation, SQLite restart/rollback and content invalidation.
- New generated-file checks: saved-search/preference persistence, default-disabled rules, first-seen download ordering/deduplication, selected-folder boundaries, screenshot filing with unchanged originals policy, destination-conflict rejection, cancellation, repeat-filing exclusion, and read-only content/tag/latest-download retrieval. Missing database is rejected without creation. Fixture insertion times are deterministic and SQLite pooling is disabled for fixture teardown.
- Native isolated UI: Home rendered; Playful to Minimal category symbols and neutral selection changed visibly; Ctrl+Shift+Space opened Quick Search; `txt` produced four expected results from the generated library. Save live search dialog opened and saving added a sidebar entry and persisted its route/preferences. The Windows automation helper's indexed `txt` query also returned the expected count.
- Release ZIP checksum and all 787 manifest payload entries verified; required desktop helper files/documentation present; extracted helper `--help` launched (`artifacts/package-verification.json`).
- Extracted release smoke check: CLI total count/display limit, missing-library exit status/no database creation; application started, responded and closed using only the generated test library (`artifacts/packaged-launch-061.log`).

## Packaging

Compact installer compiled successfully (`artifacts/installer-061.log`) and its SHA-256 was independently checked against the sidecar (`artifacts/installer-061-check.json`). Installer: `artifacts/releases/Ceeker-0.61.0-preview.1-Setup-x64-Compact.exe`. SHA-256: `2968537A19063E2863DED423511C9A6E307B41387CBC5B4D60EAF2ECB17CC86E`.

No installed user application or normal library was upgraded by these checks. The existing GitHub export remains an earlier snapshot.

## Remaining manual validation

Tray click/keyboard interaction, recent-download thumbnails and cross-application multi-file drops; custom hotkey capture/conflict/release; saved-search rename/remove and live relative-date refresh after restart; settings style-control synchronization; full light/dark and multi-monitor/DPI matrix; narrow-window breakpoints/action wrapping; preview grid-row keyboard navigation and media-control focus; native automatic-rule opt-in, cancellation/history and Recycle Bin restoration remain pending. Core rule moves were tested only on generated files; no automatic actions ran against user files.

Previously outstanding native duplicate trigger/cancel/layout and retention, Explorer grouped selection/unavailable presentation, category/sidebar/dashboard navigation/layout, HEIC orientation overlays, physical touch, install/upgrade/uninstall and clean-machine checks remain pending. Artwork is not claimed pixel-exact. Signed updates and checkout remain inactive; no Apple Siri integration is claimed. Background validation automation was not resumed.
