# Stats endpoints

All stats endpoints support two parameter patterns:

1) Positional parameters:
- `startBlock` (number)
- `endBlock` (number)
- `options` (object, optional)

2) A single options object:
- `startBlock` (number)
- `endBlock` (number)
- plus endpoint-specific filters

This page is an index. Each endpoint’s details are split into smaller files:

- [`getsyscallstats`](stats/01-getsyscallstats.md)
- [`getopcodestats`](stats/02-getopcodestats.md)
- [`getlogstats`](stats/03-getlogstats.md)
- [`getblockstats`](stats/04-getblockstats.md)
- [`getnotificationstats`](stats/05-getnotificationstats.md)
- [`getstoragewritestats`](stats/06-getstoragewritestats.md)
- [`getstoragereadstats`](stats/07-getstoragereadstats.md)
- [`getcontractcallstats`](stats/08-getcontractcallstats.md)
