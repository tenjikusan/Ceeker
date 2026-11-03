# Automatic duplicate comparison

Enable **Automatic duplicate scanning** at the bottom of navigation. Wormy compares connected files after library updates and when an enabled library is restored. It waits until the current operation and file preview finish, then uses the existing size-grouped SHA-256 scanner. Results appear in Duplicates without changing the current page.

The option defaults off and is saved per library. Turning it off cancels an automatic scan. Cancelling an automatic scan through the progress bar also pauses the saved option, so it does not immediately restart. A manual duplicate scan remains available. It only runs while Wormy is open; file removal still uses the existing explicit review and revalidation.

Automatic comparison can read substantial data on large libraries. Linked, offline, missing or changed files are skipped. Results show up to 100 groups and do not prove disk space will immediately be freed.
