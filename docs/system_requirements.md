# System Requirements

Designed to run on a minimum of a Raspberry Pi 2 (or equivalent), or any system with:

* x86, x86_64, amd64, or ARMv7+ processor
* at least 512mb RAM
* at least 150mb disk space

Users concerned about optimal performance should consider something faster.

# Storage Hardware

* SSD/NVMe: Ideal
* HDD: Shouldn't notice any issues
* Flash: It should work but you're probably going to kill the card

# Filesystems

## Supported

* ext4
* NTFS
* XFS
* APFS
* HFS+

## Might Work but Complaints Will Be Ignored

* ZFS
* Btrfs
* FAT32
* exFAT

## Not Supported

* NFS
* SMB/CIFS
* APF
* Distributed databases (Ceph, GlusterFS, Lustre, HDFS)
* Anything that puts a network between slskd and `/app`
