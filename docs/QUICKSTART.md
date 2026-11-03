# Ceeker preview for Windows x64

## Audio/video playback and navigation

Preview a Music or Videos file to use the native player. Files open paused; press Play. The transport bar offers play/pause, seeking and volume/mute. Scroll inside the dialog on smaller windows to reach its controls and status. Playback stops when you close the preview, move to another previewed file or close Ceeker. Playback uses installed Windows codecs; corrupt, missing, changed, linked or offline files show an error. The 100 MB still-image/PDF preview cap does not apply to streamed media. No playlist or persistent background playback is included.

Back/Forward buttons restore the previous browsing section or connected pinned folder, search text, filename/content scope and source filter. History holds the last 100 route entries for this session; typing edits the current route rather than recording every keystroke. A new route after Back replaces forward history. Disconnected folders return to All files; vanished source filters return to All sources. Sorting, Grid/List mode, selection and scroll positions are not stored in route history. Navigating scrolls to the top.

| Shortcut | Action |
|---|---|
| Alt+Left / Alt+Right | Back / Forward; within a preview, previous / next file |
| Ctrl+F | Focus search and select its text |
| Ctrl+R or F5 | Refresh connected folders |
| Ctrl+O | Connect a folder |
| Ctrl+H / Ctrl+D | Home / Duplicates |
| Ctrl+1 through Ctrl+6 | All files, Images, Videos, Music, Documents, Archives |

App browsing shortcuts pause while a preview/settings dialog is open. Existing selected-file Enter/Space, Ctrl+A and Ctrl/Shift selection controls remain available in their file views.

## File thumbnails

Grid cards and List rows load local thumbnails asynchronously as their controls appear. Windows supplies image, video and installed Office-handler thumbnails. PDFs up to 20 MB also have a built-in first-page rendering fallback. DOCX/XLSX/PPTX up to 20 MB can display an embedded PNG/JPEG preview; many documents do not contain one. Video formats depend on installed Windows codecs. No Office application is launched and no OCR runs to create thumbnails.

If no preview is available, Windows' file icon or Ceeker's category icon remains. Grid preview tooltips distinguish **File preview** from **File icon**. PDFs/documents fit inside their cards; image/video previews fill the card. List thumbnails fit the icon column. Unsupported, corrupt, offline, linked, changed and inaccessible files fall back to an icon. This also applies to files over 1 GB, thumbnail data over 2 MB, or images exceeding 1,024 pixels per side / one megapixel. Original files remain unchanged.

Two requests run concurrently. The shared in-memory cache holds up to 128 results and 32 MB of thumbnail data, keyed by path, indexed size and modification time. Removed/changed files are rechecked before using cached data. Navigation cancels pending work; late results belong only to their original card. Windows-provider work has a 10-second cancellation deadline; third-party handlers may finish later. The cache resets when Ceeker exits; Windows maintains its own thumbnail cache separately. Changes that preserve size and modification time may evade invalidation. Some registered Windows thumbnail providers are third-party components.

API reference: [Microsoft file thumbnails](https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.getthumbnailasync).
## Scanning settings

Open **Scanning settings…** in the sidebar. Settings apply per library to every connected folder, including Connect folder, Refresh library and automatic updates. **Save and refresh** persists the settings and refreshes existing roots; Cancel leaves settings unchanged.

- **Include Windows hidden files and folders** defaults to on, preserving existing behavior. Turning it off skips entries with the Windows Hidden attribute, including their nested contents. A leading dot alone does not mean hidden.
- **Excluded folder names** match nested directories exactly, ignoring case. Enter one name per line (commas/semicolons also work), up to 100 unique names. Paths and wildcards are rejected. A similarly named file is included. Explicitly connected roots are always scanned even when their name matches an exclusion or they are hidden.
- **Add common cache/build exclusions** adds `.git`, `node_modules`, `bin`, `obj`, `.cache` and `__pycache__`; review the list before saving.
- **Maximum files per connected folder** accepts a whole number from 1 to 1,000,000; default 100,000. This counts included files, not skipped entries. Exactly the limit is allowed. Encountering another included file stops that scan and retains that root's previous index. Increase the limit or exclude folders to complete it. Other successfully refreshed roots can still update.

Excluded or hidden records leave the saved library only after a successful scan. Inaccessible, cancelled and file-limited scans preserve prior records. Removing metadata also removes content search results when no other connected root retains the file. Original files are never changed by scanning settings. Restoring inclusion requires another successful refresh and Index file contents for newly included searchable content.
## Selection and batch actions

Click to select a file. Ctrl-click adds/removes individual files; Shift-click or Shift-arrow selects a range. Ctrl+A or **Select all shown** selects the displayed results (Home up to 12; other views up to 1,000). Selection persists across Grid/List and sorting. Filtering removes selections that are no longer shown. Double-click, Enter or Space previews; **Preview selection** steps through only selected files. Right-click within a selection preserves it; Open/Explorer/Copy path in that menu still target the clicked file.

The selection bar shows count and logical bytes. **Copy paths** copies full selected paths, one per line, in displayed order. **Copy image text (English)** sequentially reads supported selected images and copies joined text with filenames. Other file types are ignored. Empty, changed, inaccessible or corrupt images are counted as unavailable; readable images still succeed. Output is capped at 256,000 characters. Cancel leaves the clipboard unchanged; an OCR operation already underway may finish before cancellation is observed. No readable text also preserves the clipboard.

Drag selected files to an app that accepts Windows files. Ceeker supplies native StorageItems and requests **Copy**. Destination behavior depends on that app. Preparation rejects unavailable or changed files; originals remain in place. Automated native payload checks passed; dropping into an external application's UI remains unverified. PDF batch OCR and general batch recycling are not included.

## Downloads and source summaries

**Downloads** includes files under a folder component named Downloads within explicitly connected paths, including nested folders. It does not connect the Windows Downloads folder automatically or infer download time. Home shows up to eight most common saved sources, with count and logical size; missing metadata forms **Unknown source**. Click a source to browse all matching files across connected folders, then use the source dropdown for other sources. Totals count overlapping paths once and remain independent of search filters.
## File actions and preview navigation

Right-click a file in Grid or List for **Preview & file details**, **Open in default app**, **Show in Explorer** and **Copy path**. Keyboard users can select a file and use Shift+F10 for its menu. Enter or Space previews the selected file when the file view has keyboard focus.

Previews now include **Previous file** and **Next file** buttons, **Alt+Left / Alt+Right** shortcuts and a position indicator. Esc closes the preview. Navigation stops at the first and last selected file, following displayed order. Startup previews use shown results. The sequence is a snapshot of the shown files (Home maximum 12; other views maximum 1,000); it does not change while the dialog is open. PDF Previous/Next still changes PDF pages separately. Moving between files cancels the previous pane's work and suppresses late results.

Expand **File details** for kind, extension, containing folder, exact byte size, creation/modification dates, Windows-reported last access and attributes. Live size/modification changes since indexing show a refresh message. Filesystem dates are not proof of download time or recent app use; last-access timestamps may be disabled or delayed. These details are read on demand and are not new search filters. Missing or inaccessible files show an error. Camera metadata, duration and tags remain unavailable.

Keyboard implementation reference: [Microsoft keyboard accelerators](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-accelerators).

## Storage and review suggestions

Open **Storage & review** for category counts, logical sizes and proportional bars across the entire connected library. Home also shows category sizes and the summary. This measures indexed files, not drive capacity or allocated disk space. Overlapping connected paths count once. Search/source filters do not change the storage totals.

Review cards open **Large files**, **Old downloads** and **Old screenshots**. Large files defaults to at least 100 decimal MB. Both age thresholds default to more than 30 days since last modification. Old downloads includes only files inside connected paths with a folder component named Downloads. Old screenshots includes images named Screenshot or Screen shot. These rules suggest files to inspect; they do not establish that files are unused and do not delete them. Normal previews and Open/Explorer actions remain available. Suggestions overlap, so their sizes should not be added together as recoverable space.

Choose **Review settings…** to change the minimum size (1–1,000,000 MB) and ages (1–3,650 days). Settings persist per library with browsing preferences. Existing preferences retain the 100 MB / 30-day defaults. No unconnected folders or installed applications are included. Ages do not use creation, download or last-opened dates. Recycle actions remain limited to the existing exact-duplicate workflow.

## Downloaded From

Choose **Downloaded from** in Smart views, or use the source dropdown above file results. **Unknown source** shows files without usable saved download metadata. Cards, list rows and previews display the source. Queries such as `documents from github` and `from gmail` combine source filters with the existing type, size, date and content search filters. Quoted source phrases remain literal search text.

Ceeker reads Windows `Zone.Identifier` download metadata locally and stores only the resolved service name and hostname. It never fetches the saved URL or stores its query string. Known services receive friendly names; other valid websites show their hostname. Metadata can be absent or removed, so existing files may remain unknown. This is saved metadata, not verification of who sent the file. Source filters are session-only. Automatic folder checks refresh source-only changes without discarding indexed content.

Windows metadata reference: [Microsoft browser-download documentation](https://techcommunity.microsoft.com/blog/microsoftdefenderatpblog/hunting-tip-of-the-month-browser-downloads/220454).

Extract the entire ZIP into a normal local folder. Open the extracted Ceeker folder and double-click **Ceeker.App.exe**. Keep the accompanying files together. This package includes the .NET and Windows App SDK runtimes; an SDK is not required to run it.

This is an unsigned preview for Windows x64, targeting Windows build 19041 or later. It has been tested only on the development computer. No installer, automatic updates, or code-signing certificate is included. A clean Windows machine has not yet been tested.

## First use

1. Choose **Connect folder** and select a folder you want to browse.
2. Use categories, Grid/List, or a search such as `PDFs over 5 MB` or `images modified last week`.
3. Double-click a file for text, image or PDF preview. Unsupported formats show a message.
   In image previews, choose **Copy text** for offline English recognition, word highlights and selectable text. Check the result for mistakes.
4. **Auto-update folders** is enabled while Ceeker is open. New, changed, renamed and deleted files update automatically. **Refresh library** is also available. Connected folders and metadata persist between launches.
   Choose **Index contents** in the sidebar, then enable **Search file contents** below the search box to find whole words or quoted phrases inside text files, PDF/DOCX bodies and English image OCR. Examples: `pdfs "approved invoice"`, `documents "project report"` or `images "Invoice 12345"`.
5. In **Duplicates**, scan, choose a preferred copy and explicitly select extra copies. Review all paths before confirming **Move to Recycle Bin**.

## Data and recovery

The index is stored at `%LOCALAPPDATA%/Ceeker/library.db`. Files stay in their original folders. Explicit content indexing stores extracted text in that local database. Disconnecting the last root covering a file removes its searchable content and metadata; overlapping connected folders retain their records. Restoring a recycled file through Windows Recycle Bin requires a library refresh to show it again. Recycling does not immediately free disk space. This build migrates the database to schema 3; older builds cannot reopen it.

Cleanup currently supports local fixed NTFS drives. Changed, inaccessible, linked and offline files are skipped. Content is checked again before recycling, and the retained copy is held open during each operation. Avoid reorganizing files in another application during cleanup: a small concurrent path-replacement race remains at the final Windows shell call. Cancelling stops hashing and future copies; an underway shell operation completes.

## Preview limitations

Home shows up to 12 files; other browser views show at most 1,000 results while reporting the full count. Duplicate review shows at most 100 groups. Filename search remains the default; Search contents searches only explicitly indexed content, with whole-word matching and AND between terms. Quoted phrases match consecutive words. Type, size and modification-date filters still apply. Quote words that could otherwise be interpreted as metadata filters. No semantic search or similar-image detection is included. Preferred-copy selections are temporary.

Automatic folder updates monitor only connected roots while Ceeker is running. Changes are batched after roughly 1.5 seconds of quiet, with a 10-second batch window during continuous activity. A full check about once a minute recovers missed events and retries unavailable folders; large scans and other operations can delay it. Checks wait for scanning, indexing, duplicate cleanup and previews. Incomplete or unavailable folders preserve their saved records. Changed or deleted files invalidate saved searchable content; choose Index file contents to extract changed/new content. OCR does not run automatically. The checkbox pauses future checks for the current session; an active check finishes, and updates default to enabled on the next launch. Changes made while Ceeker is closed are checked after launch. Metadata changes clear old duplicate review selections. Files changed without changing length or modification time may escape detection. Manual Refresh remains available.

Content indexing supports TXT, MD, CSV, JSON, LOG, XML, YAML/YML, INI, CS, DOCX, PPTX, XLSX and PDF text/scan OCR plus the image formats supported by Copy text. Documents are limited to 20 MB and 64,000 extracted characters; DOCX body XML is limited to 8 MB. DOCX formatting, headers, footers, embedded objects and images are omitted. Legacy DOC/PPT/XLS and macro-enabled workbooks are not indexed yet. Unsupported, changed, inaccessible and linked files are skipped. Refresh first, then index again after changing files. Cancel preserves completed files; OCR already underway finishes before cancellation completes. Unchanged metadata cannot detect every edit performed by another application.

Excel indexing reads saved cell values from XLSX worksheets in workbook order, including hidden sheets. Shared/inline text, rich text, numbers, booleans and existing cached formula results are searchable. Formulas are never calculated; missing cached results are omitted and saved results may be stale. Numeric values use their stored representation, so formatted dates may appear as serial numbers and currencies lack their display formatting. The first 100 sheets, 8 MB of combined XML and 64,000 output characters are the limits; extra sheets/output report truncation and excessive XML is skipped. Charts, comments, images, macros and encrypted/legacy workbooks are unsupported. External resources are never fetched. Cancellation is checked between XML loads and cells; a synchronous XML load completes first. No Excel installation is needed. Search an indexed workbook with `documents "amber orbit"`.

PowerPoint indexing reads slide text in presentation order, including text in shapes and tables and hidden slides. It inspects up to 200 slides, with an 8 MB combined XML limit and the 64,000-character output limit. Slide order within complex layouts may differ from visual reading order. Notes, masters, charts, embedded objects and text inside images are omitted; PowerPoint image OCR is not implemented. Legacy PPT and encrypted presentations are unsupported. Empty or malformed presentations are skipped. No PowerPoint installation is needed. Search an indexed deck with a query such as `documents "blue comet"`.

PDF indexing reads selectable text first, then runs local English OCR on pages with no selectable text, including image-only scans. It inspects the first 200 pages and recognizes at most 20 pages per PDF, processing one page at a time. The 64,000-character limit still applies; exceeding page, OCR or output limits reports the file as truncated. Empty pages count toward the OCR budget. Scans are rendered at up to 300 DPI with a 2400-pixel side limit. A PDF with no detected text is skipped with a message. Passwords are not requested or stored; PDFs requiring one are skipped. Reading order and spacing may differ from the visible document, especially with columns and unusual fonts. Pages containing some selectable text are not OCRed, so scanned regions on those pages may be omitted. Cancellation is checked between parser/recognizer calls; a synchronous call already underway finishes first. The size/page/output limits do not bound every internal parser allocation or execution time.

OCR uses bundled Tesseract and English language data without a network connection. Image OCR processes the first frame, with limits of 20 MB, 4096 pixels per side and 8 megapixels. PDF scans use the tessdata_best English model; image previews retain the faster English model. Copy text does not save content; Index file contents recognizes supported images and PDF scan pages and saves their text for search. Recognition can make mistakes; verify important names, numbers and passages. Closing the preview suppresses pending results; native recognition already underway completes before its worker is released. Included third-party licenses are in the `licenses` folder.

The included performance report measures backend scan/index/search/hash operations with generated small text files. It does not establish UI frame responsiveness or large-document hashing speed. Populated duplicate review, preferred-copy protection, confirmation paths, cancellation and restart persistence were checked using an isolated generated library. Successful UI cleanup was observed; the final recycle click was not automated. Recycling, retained contents and metadata pruning also passed integration checks. A clean-machine package launch remains to be tested.

Advanced: `Ceeker.App.exe --library "C:\path\separate-library.db"` uses a separate index for this instance. The regular launch continues to use the default index.


## Browsing controls

Choose a sort order above the results: newest/oldest, name A–Z/Z–A, largest/smallest or kind A–Z/Z–A. Kind uses the file extension. Sorting applies before the result limit. In Grid, drag Thumbnail size to resize cards; the slider is hidden in List. View settings are saved beside the library database as `<database>.view.json`.

Choose a connected root, open Folder options and choose Pin selected folder. Its sidebar entry shows files in that root and its subfolders, combined with your search. Unpin selected folder removes the shortcut. Only connected roots can be pinned; disconnected roots disappear from the sidebar and return if reconnected. Large files includes files at least 100 MB. Screenshots identifies image filenames beginning Screenshot or Screen shot; it does not inspect image pixels or detect renamed screenshots.




## Reviewed cleanup and similar photos (0.19)

Home and Storage & review show a combined count and logical bytes for Large files, Old downloads and Old screenshots. Files matching several rules count once. Review all suggestions opens their union with reason labels. This summary excludes exact duplicates, similar-photo pairs and installed apps. It covers the indexed connected library; ordinary results still display at most 1,000 files.

Select files in Grid or List, then choose Review for Recycle Bin. A batch accepts 1–100 selected files. The app hashes each file before showing full paths and sizes, then hashes it again after confirmation. Cancel is the default; nothing is automatically selected for recycling. Changed, missing, offline or reparse paths are rejected. Failures and partial cancellation are reported individually; successful paths are removed from the saved index. Hashing reads the entire files and can take time. Cancel stops remaining work; files already recycled stay in Recycle Bin. Recycling is restricted to the existing local fixed NTFS shell implementation and does not immediately release disk space. A small concurrent path-replacement race remains after the final read handle closes; avoid reorganizing these files during cleanup.

Similar photos is a separate sidebar view (scroll the sidebar if needed). Choose Find similar photos to compare at most the first 2,000 distinct image paths in path order. The scan returns at most 300 candidate pairs. It decodes the first frame locally, honors EXIF orientation, and normalizes colors to sRGB. Images over 20 MB, 4,096 pixels per side or 8 megapixels, unsupported/corrupt images and samples with insufficient detail are skipped. Each decode has a 15-second cancellation deadline; native operations already underway may finish later. Formats such as HEIC depend on installed Windows codecs.

Candidates use a 64-bit difference hash (at most six different bits), aspect ratios within 5% and mean RGB channel differences within 30. They can include resized or recompressed copies, exact duplicates and false matches. Cropped, rotated or strongly edited photos may be missed. The displayed bit distance is not a confidence percentage. Review both originals opens the existing Previous/Next preview. To recycle a file after review, select it in its ordinary browsing view and use the separate confirmation workflow. No keeper is chosen automatically. Cancelling discards that scan's results; library changes invalidate candidates. Results are session-only and the image originals remain unchanged during detection.

## Copy text from a PDF preview (0.20)

Open a PDF preview, scroll below the page preview, and choose Read PDF text. Reading processes the document, using selectable text first and English OCR on pages without selectable text. Review the read-only text panel, then choose Copy PDF text. Reading alone leaves the clipboard unchanged and does not save text in the search index. Copy explicitly replaces the clipboard with the extracted text. Cancel reading stops remaining work; closing or moving to another file cancels that preview's reader. Native recognition/parser calls already underway finish before cancellation is observed.

The existing PDF limits apply: 20 MB, the first 200 pages, at most 20 OCR pages and 64,000 characters. Limited results show a warning. Pages containing selectable text are not OCRed, so scanned regions on those pages may be omitted. Recognition and reading order can contain mistakes. Blank/corrupt/password-required or changed files report an error with Copy disabled. The full document is read independently of the displayed page. Refresh stale library metadata before reading a changed PDF.

## Appearance and image-hover OCR (0.21)

The Dark theme checkbox is below Auto-update folders. It changes Ceeker's main views, cards, previews, dialogs and menus immediately and saves per library in the existing .view.json preferences. Existing libraries default to Light. This is an explicit application choice rather than automatic Windows-theme following; the standard OS window frame can retain its Windows appearance. Original document pages, images and yellow folder illustrations keep their own colors.

Hover over a supported image card or its list-row text to reveal Copy text. Keyboard focus also reveals the button. Clicking runs local English OCR for that single image, validates indexed metadata before and after, and copies only recognized text. Hover alone does no recognition or copying. Empty, missing, changed or failed images leave the clipboard unchanged. Cancel copy text stops remaining work; native recognition already running finishes before cancellation is observed. The shortcut supports PNG/JPEG/BMP/GIF/TIFF, first frame only, with the existing 20 MB, 4,096-pixel-side and 8-megapixel limits. Other formats omit the shortcut. Check recognized names and numbers for mistakes. The existing selected-image OCR action remains available.

## Installed apps (0.22)

Choose Apps in the sidebar (scroll the navigation section if needed). It reads desktop app registrations for the current user and the machine's 32/64-bit uninstall views, plus packaged apps installed for the current Windows user. Hidden system components, child uninstall entries, framework/resource/bundle packages and unnamed desktop entries are omitted. Portable applications and other users' packaged apps are not scanned. Duplicate desktop/package registrations can remain separate; counts are registrations rather than a guaranteed count of distinct products.

Search names/publishers or sort by largest reported size. Installer-reported sizes are approximate; unavailable sizes sort last, and packaged app sizes are not measured. This view is separate from connected-file storage totals and cleanup suggestions. Enumeration stops at 10,000 visited registrations/packages; the view displays up to 300 matching rows. Narrow the search to reach other matches. Refresh apps re-reads metadata; Cancel refresh retains the last completed snapshot. Results and filters are session-only. Windows calls already in progress finish before cancellation is checked.

Manage / uninstall in Windows opens Windows Settings. Desktop entries open the installed-app list; select the named app there. Packaged entries use their app-specific settings URI, with a generic-list fallback if launching fails. Windows decides which uninstall options are available and owns any confirmation/elevation. Ceeker never executes registry-provided uninstall commands or removes application files. Return to Ceeker and Refresh apps afterward. App use/last-launch history and unused-app suggestions remain unavailable.

Settings handoff uses Microsoft's documented [Settings URI scheme](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings).

## Getting started (0.23)

A new empty library opens a three-step guide covering folders, search/previews/OCR and reviewed cleanup. Next and Back navigate the guide. Later dismisses it without marking completion; it can appear again on the next empty-library launch. Get started saves completion for that library. Existing connected libraries are not interrupted. Replay it anytime using Getting started at the bottom of the scrollable sidebar. After finishing, use + Connect folder to choose your first folder. The guide does not connect or scan a folder automatically.


## Suggested folders and date filters (0.24)

Completing the guide for an empty library opens Suggested folders. You can also open it from the bottom of the scrollable sidebar. Windows resolves Desktop, Documents, Downloads and Pictures, including redirected locations. Nothing is preselected; connected folders are disabled. Select one or more, then Connect selected. Cancel closes without scanning. Scans run sequentially with the current scanning settings; Cancel scanning retains folders already completed and preserves earlier snapshots for incomplete scans. Unavailable folders are omitted and scan failures are reported.

Search examples: PDFs modified on 2026-10-09; images modified since 2026-10-01 modified before 2026-11-01; documents last 7 days. Dates use YYYY-MM-DD and your local time zone. On covers that whole day; before excludes it; after begins the following day; since includes it. Last N days includes today and the preceding N-1 calendar days, excludes future days, and supports 1–3650. Date filters intersect; contradictions display a warning. Creation/download dates remain unsupported. Quote date-like text to search it literally in filenames.

The earlier 0.23 guide note about manually connecting after finishing is superseded by this optional suggested-folder screen.


## Windows installer (0.25)

Run Ceeker-0.25.0-preview.1-Setup-x64.exe to install for the current Windows user. The default destination is LocalAppData\Programs\Ceeker. Setup bundles the runtimes and adds a Start menu shortcut; the desktop shortcut is optional. It supports Windows 10 build 19041 or newer on x64-compatible Windows. Close Ceeker before upgrading. Install a later setup into the same location to upgrade.

Uninstall through Windows Settings > Apps > Installed apps. Uninstall removes installed application files and shortcuts but retains your saved library/preferences under LocalAppData\Ceeker and all connected original files. The preview installer is unsigned. Code signing and automatic updates remain later stages.

Build with scripts/Package.ps1, then scripts/BuildInstaller.ps1. Pass -Compiler with the path to Inno Setup 6.7+ ISCC.exe if it is not available in artifacts/tools/inno. VerifyInstaller.ps1 performs an isolated install/reinstall/uninstall test and refuses to run when Ceeker is already installed.


## Release checks (0.26)

Open Updates at the bottom of the sidebar. Enter your release-manifest HTTPS URL and choose Check now. The address is saved for this library. No background requests are made. The dialog displays the installed version, newest release notes and supplied SHA256, then enables Open installer download for a newer version. That opens your browser for manual downloading and installation; the app does not download, checksum-verify or automatically execute an installer.

The website address is intentionally unset until launch preparation. A release feed is a small JSON object with version, installerUrl, sha256 and notes. Versions support major.minor.patch and -preview.N. HTTPS redirects are rejected; serve the manifest directly at the configured URL. Checks are cancellable on closing the dialog, capped at 64 KB and timed out after 15 seconds.

After building the installer, use scripts/CreateReleaseManifest.ps1 -InstallerUrl with its actual HTTPS URL. Upload the installer first, then publish the generated manifest as latest.json. A checksum from the same website is not a publisher signature; signed releases and automatic installation remain future work.


## Touch-screen mode (0.27)

Scroll to the bottom of the sidebar and enable Touch-screen mode. The setting is saved for this library. Buttons, input controls, sliders and file-item targets use a minimum 44 logical pixels in the visible app and dialog trees. Tap files to toggle multiple selections without Ctrl; use Preview selection in the selection bar to open them. Supported image Copy text buttons remain visible without hovering. Native scrolling supports swiping. Turn touch mode off to restore ordinary target sizes and Ctrl/Shift selection. Existing keyboard shortcuts and mouse input remain available.

This is an explicit interaction preference, not automatic detection of touch hardware. Physical touchscreen and pen testing are separate from mouse-driven layout validation.


## Tags and combined search (0.28)

Right-click a file in Grid or List and choose Edit tags. Enter comma-separated tags; clear the field to remove them. Tags are saved only in this library and do not change the original file. Names and tags are searched together. Enable Include indexed file contents to also match saved document text and OCR. All terms must match across names/tags, or all terms must match indexed content; type/date/size/source filters apply to both. Tags survive refresh while the path remains connected, but are removed when no connected root retains that path. Renames do not transfer tags automatically.

Schema 4 migration is automatic when opening a library; older Ceeker versions cannot reopen that migrated index. Select one file and use Edit tags for selected file for a touch-friendly alternative to right-click.

## Select image words (0.29)

In an image preview choose Select text to run local English OCR without copying. Tap recognized word boxes on the image or toggle the larger word buttons below it. Copy selected words copies only your selection in OCR recognition order, with spaces between words. Toggle again to deselect. Selection is temporary and is cleared with the preview; OCR can make mistakes. Copy text continues to copy the complete recognition result. PDF word selection is not included.


See FEATURES-0.30.md for the new search grammar, background OCR, improved photo matching and cleanup rules.

