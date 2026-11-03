# Local Windows actions

Ceeker.Actions.exe is included beside Ceeker.App.exe. Read-only actions use the existing saved local index; they do not scan disks or alter files. Paths in JSON are local data: share output only deliberately.

PowerShell examples, from the installation directory:

```powershell
.\Ceeker.Actions.exe find 'pdfs last week' --limit 25
.\Ceeker.Actions.exe latest-download --limit 10
.\Ceeker.Actions.exe search-in-app 'screenshots today'
.\Ceeker.Actions.exe quick-search
```

Use `--library C:\path\library.db` for a different saved index. Missing libraries return an error and are not created. `find` supports built-in natural-language grammar, names, tags and fresh indexed FTS contents; it does not run the optional semantic models or probe camera metadata. Results contain interpreted filters, warnings and a total before the display limit. `latest-download` orders by first seen in Ceeker and covers connected indexed download locations only.

Use `--app C:\path\Ceeker.App.exe` for desktop commands outside the installation folder. GUI launch commands start an application instance; use the global shortcut for the running instance. OS shortcut files or a PowerShell automation can invoke these commands. No Apple Shortcuts/Siri bridge is included.
