# System Requirements

Designed to run on a minimum of a Raspberry Pi 2 (or equivalent), or any system with:

* x86, x86_64, amd64, or ARMv7+ processor
* at least 512mb RAM
* at least 150mb disk space

Users concerned about optimal performance should consider something faster.

# Known Issues

## Devices using a copy-on-write filesystem such as BTRFS or ZFS

slskd uses SQLite to store transactional data associated with things like transfers and searches, and these reads and writes experience too much I/O latency for the application to work properly with these filesystems.

Optimizations to the filesystems can be made to improve performance, but will come at a cost to most other use cases and are not recommended.

slskd can still run on devices using these filesystems if application data can be stored on a separate disk or partition using a different filesystem, or potentially on a RAM disk (which would be volatile unless configured otherwise).

Users can also enable 'volatile mode' (`SLSKD_VOLATILE` or `flags.volatile: true`), which will use in-memory SQLite tables.  Searches and transfers will be lost when the application is restarted while in this mode.

## Application data stored on network filesystems (NFS, SMB, Windows file sharing)

For the same reasons as copy-on-write systems.

## AWS EC2 t3a.nano with Ubuntu Linux

The instance will run out of memory and crash every 12 hours or so.  This instance size works when using Amazon Linux OS, but memory usage is very high and wouldn't be able to handle a lot of activity.