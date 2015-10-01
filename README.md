# PostgreSQL 9.4 to SQL Server 2012/2014 Converter

## Synopsis

The original intention of this program was to convert a large (47GB) PG 9.4 
database to SQL Server 2012 / 2014. As it's growing I thought it would be a 
good idea to make this my first publicly-released GPL program.

It's in a pre-alpha stage, so *caveat emptor*.

## Features

* The program uses an **INI** file to store its configuration.
* The program creates various scripts that you can exceute in SSMS or command line:
  - **00_create_schemas.sql**: creates the schemas.
  - **01_create_types.sql**: create custom types.
  - **02_create_tables.sql**: creates the tables, including DEFAULT constraints and
  table and columns comments/remarks/descriptions. The program also expands
  array columns by appending a sequence number to the original column name.
  - **03_bulk_copy.sql**: performs the bulk copy of the generated TSV (Tab 
  Separated Values) files.
  - **04_create_indexes_&_constraints.sql**: creates the primary keys and indexes.
  Unique keys and foreing keys are in the pipeline.
  - **50_truncate_tables.sql**: truncate all the tables, in  case you need to redo
  the bulk copy.
  - **51_drop_tables.sql**: drop all the tables.
  - **52_drop_types.sql**: drop all the custom types.

* Bulk import files:
  - TSV format (Tab Separated Values)
  - Text values are not enclosed in quotes.
  - Arrays are expanded e.g. {1,2,3,4} -> 1\t2\t3\t4.

## TODO:

* Foreign keys
* More complex column constraints (like CHECK)
* Views
* Triggers

## Wish List

* The plan is to eventually include SS-to-PG code also.
* Unit testing
* Better character set / encoding support