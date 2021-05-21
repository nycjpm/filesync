README.txt

filesync.exe ABOUT

Filesync can accept a from:path and to:path, or it can use a manifest:file.  
Using a manifest file, it can map different from and target folders and 
iterate them all.  Effectively, it iterates the lines in the manifest and 
calls op:sync for each mapping line in the manifest.

filesync op:sync performs file sychronization from and to specified root 
directories. It replicates copies new files, deletes d

Using nohash:true filesync will do a simple file sortmerge based exclusively 
on file existence, file size, and file timestamps.  These are filesystem based 
metadata and can are extremely fast.  This sortmerge will pick up all files 
which are not written in away which bypasses the filesystem.

Using nohash:false, filesync will perform a hash comparison for files which 
are identical size, time, and name.  This is a very slow operation, but has 
the advantage of finding files which are written past the file system, such 
as certain database files.

One strategy is to use metadata sync for most files, and to map additional, 
smaller, targeted subfolders using hash testing.  An alternative strategy 
is to run a metadata sync frequently (2x daily) and a hash sync less 
frequently (weekly).

filesync op:wayback uses date math to write to a collection of sync folders 
representing your data on various dates. op:wayback only works with a manifest 
file. it calculates the "current month" and then pospends it as "-YYYY-MM" 
to the target folder.  This will create a new folder every month, which would 
quickly fill your san.  However, op:purge will delete older wayback folders
with a retention policy.

filesync op:purge searches through the the wayback folders and discards 
extraneous/obsolete folders.  op:purge keeps every month this quarter and last 
quarter, the last month of every quarter this year and last year, and the last 
month of every year forever.  wayback folders can also be manually deleted, 
without upsetting the logic. op:purge only works with a manifest file.



filesync.exe USAGE

EXAMPLE: filesync from:"d:\" to:"g:\d-2019-08-01" echo:w nohash:true
EXAMPLE: manifest:"d:\filesync\manifest.txt op:wayback echo:w nohash:true

op:[sync,wayback,purge,readme] operation to be performed.  

	op:readme displays this full readme / documentation file

	op:sync writes to and deletes from a "to" (target, slave) tree from 
	a "from" (source, master) tree
	
	op:wayback uses the current date (at run time) to calculate the month, 
	and then postpends the month to the target folders, and uses op:sync 
	for each mapping

	op:purge uses the current date and manifest file to purge obsolete 
	folders / versions of the target using retain:m#,q#,y# for 
	retention plan

	op:remver removes all .wayback-versions folders from the target

from:folder (eg from:"c:\data") root path to sync FROM, the master source

to:folder (eg to:"g:\date-2019-08-01") root path to sync TO, the slave copy

manifest:file (eg manifest:"d:\filesync\manifest.txt" a list of source and 
target mappings

echo:[w,s] (eg echo:w,s) echo on write, echo on skip, DEFAULT=w

hash:true (hash source and target files to detect data differences not 
reflected in the file size and timestamp cheecks, DEFAULT=FALSE (do not 
HASH files for comparison)

noprompt:true (do not pause for keypress when done), DEFAULT=FALSE

versions:true (before touching any file, copy it to: 
.\.wayback-versions\[file].LastModified.  DEFAULT=FALSE (do not preserve
versions of files before replacing or deleting them)

retain:m#,q#,y# set the number of months, quarters and years to retain, 
DEFAULT=m6,q8,y99 (6 month end, 8 quarter end and 99 year end)

log:path (eg log:\\san\logs)

