# SMS Search

A Windows WPF desktop utility for searching and exploring LOC SMS SQL Server schemas, tables, fields, functions, totalizers, and `Fields.csv` data dictionaries.

[Download Latest Release](https://github.com/Rapscallion0/SMSSearchWPF/releases/latest)

---

## Key Features

### Search & Dictionary
- **Search Modes:**
  - **Field:** Search by Field Number, Description, Table (table schema or live table data), or **Custom SQL** with wildcard and **Any Match** support.
  - **Function (`FCT_TAB`):** Query function definitions with customizable default columns via the Column Picker.
  - **Totalizer (`TLZ_TAB`):** Query totalizer definitions with customizable default columns.
- **Database Dictionary (`Fields.csv` / `Fields2.csv` / `Fields.es`):**
  - Auto-detects `Fields.csv` beside the executable or lets you browse / drag-and-drop a custom path (with an option to copy dictionary files into the application folder).
  - Non-destructively merges `Fields2.csv` (custom fields/tables) and `Fields.es` (Spanish translations).
  - **Hybrid & Offline Modes:** Enriches live SQL Server queries with `Fields.csv` descriptions/types, and searches `Fields.csv` directly in-memory when no SQL Server is connected.
  - **Multi-Language & `DCT` Columns:** Choose a primary language (English, French, or Spanish), optionally display extra language columns and the `Dictionary` (`DCT`) column, and switch between `Auto`, `CSV`, and `DB` on the fly from the status bar.
- **Initial Setup & Server Discovery:**
  - First-start setup wizard lets you configure a SQL Server connection (`RB_TABLES` / `RB_FIELDS`), a `Fields.csv` dictionary, or both.
  - Instant local SQL Server instance discovery from the Windows Registry, plus opt-in **Search for network servers** scanning.

### Results & SQL Editor
- **Virtualized Results Grid:** Fast paging for large result sets, right-click header toggling between Field `#` and Description, instant search/filtering with match highlighting, and export to **CSV**, **JSON**, and **XML**.
- **SQL Editor & Formatting:** AvalonEdit-based SQL editor with multi-level schema IntelliSense, query history, and **SQL Clean / Beautify** with customizable formatting and regex rules.

### Built-In Utilities
- **Import Customer Tables:** Drag-and-drop or browse `.sql` table files to import into a target database, supporting optional template databases (`RB_FIELDS` / `RB_TABLES` + exact column types) or automatic schema inference with interactive column-type confirmation.
- **Unarchive Drop Target:** Floating always-on-top drag-and-drop target for quickly unarchiving dropped files.
- **GS1 Barcode Toolkit:** Parse, inspect, and generate GS1 barcodes with optional clipboard monitoring.
- **Status Bar & Desktop Integration:** Live Julian ↔ Gregorian date converter, global hotkey/clipboard listener, Light & Dark themes, and Windows DPAPI-encrypted SQL credentials.

---

## Getting Started

1. Download `SMS_Search.zip` from the [Latest Release](https://github.com/Rapscallion0/SMSSearchWPF/releases/latest).
2. Extract `SMS Search.exe` to a folder of your choice.
3. *(Optional)* Place your `Fields.csv` (and optional `Fields2.csv` / `Fields.es`) in the same folder as `SMS Search.exe` for automatic detection, or browse/drag-and-drop it during initial setup.
4. Launch `SMS Search.exe`.

---

## Building from Source

### Requirements
- Windows (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`net10.0-windows`)

### Build & Test
```powershell
dotnet restore SMSSearch.sln
dotnet build SMSSearch.sln -c Release --no-restore
dotnet test SMSSearch.sln -c Release --no-build
```

---

## Disclaimer

SMS Search is provided free of charge for personal or commercial use **"AS IS"** without warranty of any kind. It is an independent utility and is **not** affiliated with, endorsed by, sponsored by, or supported by **LOC Software** or **LOC SMS**. Always verify Custom SQL queries and maintain database backups before making changes.
