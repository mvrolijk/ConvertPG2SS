# Change Log
All notable changes to this project will be documented in this file.
This project adheres to [Semantic Versioning](http://semver.org/).

## [0.0.9] - 2015-10-12
### Added
- Foreign keys constraints

### Changed
- Fixed a default values bug,

## [0.0.8] - 2015-10-10
### Added
- Sequences

### Changed
- Eliminated the need for temp tables.
- Fixed varchar(max) and binary type bugs.
- Got rid of some magic values.

## [0.0.7] - 2015-09-30
### Added
- Custom types.

### Changed
- Changed from VS 2013 to 2015.
- Bunch of code refactoring.

## [0.0.6] - 2015-09-29
### Changed
- Move index & primary key creation to another file.

## [0.0.5] - 2015-09-28
### Added
- First complete bulk generation completed: 17.7GB in TSV files. PG db is 45.6GB.

### Changed
- Before bulk read change from DataReader to DataAdapter.
- Refactored ProcessPgSchema.cs to use more sane column names.
- Fixed bulk generation i.c.w. null columns.

## [0.0.4] - 2015-09-27
### Added
- GNU GPL v3 licesing.
- README.md and some of the usual text files.

### Changed
- Bulk generation of import files is functional now. Need to get the bugs sorted.

## [0.0.3] - 2015-09-25
### Changed
- ProcessSchema is in a satisfactory state now. Working on ProcessBulk.cs.

## [0.0.2] - 2019-09-24
### Added
- Generate create_schemas.sql & create_tables.sql.
- Fixed ExecuteReader exception handling.
- Expansion of column arays into multiple columns now working.
- Fixed namespace issues for Security project.

## [0.0.1] - 2019-09-23
### Added
- Initial commit
- Fixed exception handling in method ConnectToPgDb.
- Cleanup
