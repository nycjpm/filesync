README.txt -> filesync.exe - File Synchronization Utility

=== ABOUT ======================

filesync is a high-performance file synchronization tool that replicates a source 
folder tree (master) to a target folder tree (slave). It supports parallel execution, 
manifest-based batch operations, wayback snapshots, and retention-based purging.

filesync accepts either a from:/to: pair on the command line, or a manifest:file 
containing multiple mappings. All options can be specified on the command line and 
optionally overridden per-mapping line in the manifest.

By default, filesync runs in read-only compare mode (write:off). No files are written, 
copied, or deleted unless write:on is explicitly specified. This makes it safe to run 
as a dry-run to see what would change before committing.

=== OPERATIONS ======================

op:sync (default)
    Synchronizes the source tree to the target tree. Copies new and changed files from 
    source to target. Deletes files and folders from target that no longer exist in source.
    File comparison uses size and timestamp (with 5-second tolerance for FAT/exFAT 
    filesystem granularity). Optionally uses MD5 hash comparison for files that match 
    on size and timestamp but may differ in content.

op:wayback
    Uses the current date to calculate the current month (YYYY-MM) and appends it to 
    the target path, then performs op:sync into that dated subfolder. Running daily 
    leaves a versioned archive of your data at monthly granularity. Use op:purge to 
    manage retention. Example target (calculated): \\nas\z-wayback\code\asof-2026-04

op:purge
    Scans wayback target folders and deletes those outside the retention policy. 
    Retention is controlled by retain:m#,q#,y# specifying how many month-end, 
    quarter-end, and year-end snapshots to keep. Folders not matching the slug 
    prefix or date format are never touched.

op:fixfolderdates
    Walks the source tree bottom-up and sets each folder's modified date to the 
    maximum LastWriteTime of all files contained within it recursively. Folders with 
    no files anywhere in their subtree are ignored. This operation is particularly 
    useful after restoring from a backup, zip extract, or FTP transfer, all of which 
    reset folder timestamps to the transfer date rather than preserving the original 
    dates of the content within.

    Only uses from:path. The to:path is not used for this operation.
    Requires write:on to actually change dates. Run write:off first to preview.

    Note: Setting timestamps on network share root folders may fail with an UNABLE TO 
    error — this is a Windows limitation and is non-fatal.

op:readme
    Displays this documentation.

=== OPTIONS ======================

write:[on|off]
    Controls whether filesync actually writes to the filesystem.
    write:off (DEFAULT) - compare only, log what would happen, touch nothing
    write:on - perform all copies, deletes, and folder operations
    IMPORTANT: scheduled tasks and command lines must specify write:on explicitly
    or no changes will be made.

from:folder
    Root path to sync FROM (the master/source).
    Example: from:"\\newnas\code"

to:folder
    Root path to sync TO (the slave/target).
    Example: to:"\\oldnas\code"
    Not used for op:fixfolderdates.

manifest:file
    Path to a manifest file containing multiple from->to mappings, one per line.
    Lines starting with # are comments. Lines may include per-mapping option overrides.
    Example: manifest:"d:\filesync\manifest.txt"

op:[sync,wayback,purge,fixfolderdates,readme]
    Operation to perform. DEFAULT=sync

hash:[true|false]
    When false (DEFAULT), file comparison uses size and timestamp only. Fast.
    When true, files matching on size and timestamp are also compared by MD5 hash.
    Useful for detecting files altered in ways that bypass filesystem metadata.
    Note: hash:true serializes directory recursion and is significantly slower.
    Note: hashing both sides across a network share is extremely I/O intensive.

echo:[w,s]
    w - log writes (copies, deletes, timestamp updates, and proforma equivalents)
    s - log skips (files determined to be already in sync)
    Combine with comma: echo:w,s
    DEFAULT=w

threads:n
    Maximum degree of parallelism for file operations and directory recursion.
    DEFAULT=unrestricted (system default)
    Example: threads:8

folderdates:[on|off]
    on (DEFAULT) - sync forces To: folder modified dates to match From: folder modified 
        dates this addresses the issue where folder operations (create folder, create or 
        delete files, or edit file metadata) set the folder date to "now". When syncing 
        data the folder modified dates are deemed owned by the source, not the target.
    off - does not force To: folder modified dates.
    
retries:[on|off]
    on (DEFAULT) - retry failed file copies with exponential backoff up to 15 seconds.
        Also clears read-only attributes on destination before retry.
    off - fail immediately on first error, emit one clean error line per file.
        Useful for quickly generating a clean list of unreadable source files.

prompt:[true|false]
    true (DEFAULT) - pause for keypress when done
    false - exit immediately when done, suitable for scheduled tasks

retain:m#,q#,y#
    Retention policy for op:purge. Specifies how many month-end, quarter-end, 
    and year-end wayback folders to keep.
    DEFAULT=m6,q8,y99
    Example: retain:m3,q4,y10 keeps 3 month-end, 4 quarter-end, 10 year-end folders

slug:prefix
    Prefix used to identify wayback folders created by op:wayback.
    DEFAULT=asof-
    Example: slug:asof- produces folders named asof-2026-04


=== MANIFEST FILE FORMAT ======================

Format: FROM PATH -> TO PATH -> option:val option:val
Token is " -> " (space-arrow-space). Third column is optional per-mapping overrides.
Lines beginning with # are ignored.

Example manifest.txt:
    # live shares - mirror to internal drive
    \\nas\code -> m:\code
    \\nas\data -> m:\data

    # wayback snapshots
    \\nas\code -> \\nas\z-wayback\code -> op:wayback echo:w
    \\nas\data -> \\nas\z-wayback\data -> op:wayback echo:w

    # wayback retention cleanup
    \\nas\code -> \\nas\z-wayback\code -> op:purge retain:m3,q4,y10
    \\nas\data -> \\nas\z-wayback\data -> op:purge retain:m3,q4,y10


=== EXAMPLES ======================

Dry run - see what would change without touching anything:
    filesync from:"\\newnas\code" to:"m:\code" echo:w

Live sync - actually copy and delete:
    filesync from:"\\newnas\code" to:"m:\code" write:on echo:w

Fast sync limited to 8 threads, no prompt:
    filesync manifest:"d:\filesync\manifest.txt" write:on threads:8 prompt:false

Generate clean error list of unreadable source files:
    filesync manifest:"d:\filesync\validate.txt" retries:off write:on > errors.txt

Wayback and purge via manifest (suitable for scheduled task):
    filesync manifest:"d:\filesync\manifest.txt" write:on threads:8 prompt:false echo:w

Compare only - full manifest dry run showing all differences:
    filesync manifest:"d:\filesync\manifest.txt" echo:w,s

Preview folder date conforming without changing anything:
    filesync op:fixfolderdates from:"\\newnas\code" echo:w prompt:false

Conform folder dates to actual file content dates:
    filesync op:fixfolderdates from:"\\newnas\code" write:on echo:w prompt:false


=== NOTES ======================

- write:off is the default. Always verify with a dry run before running write:on 
  against a large tree for the first time.

- FAT and exFAT filesystems have 2-second timestamp granularity. filesync uses a 
  5-second tolerance window to avoid unnecessary copies due to rounding on both sides.

- All timestamp comparisons use UTC to avoid time zone and DST ambiguity.

- Folder timestamps are set from source after all files and child folders are synced, 
  ensuring they accurately reflect source folder modification times.

- The $RECYCLE.BIN and RECYCLER folders are always excluded from source enumeration.

- op:purge only touches folders matching the slug prefix and yyyy-MM date format. 
  Manually created or named folders are never deleted by purge.

- op:fixfolderdates only sets the modified (LastWriteTime) date on folders. Creation 
  dates are not set because File.Copy always stamps the destination with the copy date, 
  making creation dates meaningless after any migration or restore operation. because
  op:fixfolderdates exists purely to calculate modified dates for folders, the 
  folderdates:on|off option does not apply.

- op:fixfolderdates is idempotent - running it multiple times produces the same result.
  It is safe to run on a live share as it only modifies folder metadata, not file content.