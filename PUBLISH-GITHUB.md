# Upload this folder to GitHub

Create an empty GitHub repository first. Install Git LFS, then use a terminal in this folder; the included .gitattributes routes ONNX and OCR model binaries through LFS. Upload the folder contents as the repository root. Do not use the browser drag-and-drop uploader for this complete model-bearing snapshot.

```powershell
git init -b main
git lfs install
git add .
git lfs ls-files
git commit -m "Initial Ceeker source snapshot"
git remote add origin https://github.com/YOUR-ACCOUNT/YOUR-REPOSITORY.git
git push -u origin main
```

Replace the repository URL before running the final two commands. Git LFS storage/bandwidth limits depend on your account. For a clone, install Git LFS and run git lfs pull before building.

The generated Windows installer belongs in GitHub Releases, not in the source commit. It is intentionally outside this folder. No GitHub upload, commit or publication was performed automatically. Choose a project license before describing the code as open source; this export retains existing third-party licenses and does not invent a license grant.

Official guidance: https://docs.github.com/en/repositories/working-with-files/managing-large-files/configuring-git-large-file-storage
