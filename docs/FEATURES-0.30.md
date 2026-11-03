# Search, background OCR, photo review and cleanup

Search accepts `inside folder "Summer Trip"`, `in august 2026` (an omitted year means the most recent matching month), `older than 6 months`, `screenshots` and `from my iphone`. Dates remain modification dates. Device searches read recorded camera manufacturer/model or device metadata for up to 1,000 images/videos or 10 seconds, skipping unknown/offline files. This does not prove how a file was transferred.

Enable **Background image OCR** at the bottom of navigation to index supported images while Wormy is open. One image is processed at a time; valid saved content is skipped, changed metadata invalidates stale text, and failures retry after five minutes. **Pause OCR** disables the saved option. It starts off in existing libraries. Processing uses local English OCR; recognition can be wrong.

Similar photos combine detail and frequency hashes with a center sample, accepting small visual changes and brightness edits. Sampling supports up to 40 MB, 100 megapixels and 16,384 pixels per side, with existing 2,000-image/300-pair limits. HEIC decoding needs a compatible installed Windows codec. Results are review candidates, not proof of identical files; strong crops, rotations or motion may not match.

Home cleanup includes Windows installer packages and setup/installer-named EXEs modified over 30 days ago, plus videos above the Large files size threshold modified over 90 days ago. Installer installation status is unverified. File recycling still requires review.

Unused-app candidates use the last recorded Explorer launch older than 90 days; unknown history is excluded. Windows launch history is incomplete, and other launch methods may be absent. Review/uninstall in Windows Settings; Wormy does not recycle app files. The timestamp parser follows the [UserAssist format used by Velociraptor](https://github.com/Velocidex/velociraptor/blob/master/artifacts/definitions/Windows/Registry/UserAssist.yaml), which also documents these coverage limits.
