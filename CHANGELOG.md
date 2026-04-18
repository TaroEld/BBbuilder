# Changelog

## 1.5

### New Features
- **Extract Basegame command**: New `extract-basegame` command to extract and decompile the base game files
- **GUI overhaul**: Redesigned GUI with Extract Basegame tab and various UX improvements
- **Zip name and exclude/include folders**: Specify custom zip names (`-zipname`), folders to exclude (`-excludedfolders`), and folders to include (`-includedfolders`) during build
- **Zip name collision detection**: Improved checking for similar zip names in the data folder, with the option to remember your choice
- **Parallel checksum calculation**: File checksums are now calculated in parallel for faster builds
- **Colored console output**: Terminal output is now color-coded for better readability
- **Verbose mode and time logging**: Added `-verbose` flag and execution time logging
- **Version flag**: `bbbuilder -version` to print the current version
- **Data path validation**: The tool now checks if the configured data path is valid
- **Improved help output**: Short help by default, use `-help` for the full overview

### Bug Fixes
- Fixed double-zipping files
- Fixed incorrect variable usage when printing allowed zip names
- Fixed zero-length flags not producing errors
- Fixed folder creation and deletion issues
- Fixed brush file packing behavior with deleted files
- Fixed debugger detection check
- Fixed clock not interacting properly with GUI

### Internal Changes
- Switched from SQLite to JSON with file hashes for change tracking
- Consolidated to a single solution structure
- Various refactors for code quality and GUI compatibility

## 1.3

Previous release (July 2, 2024).
