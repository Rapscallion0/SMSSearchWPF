# Project Instructions & Engineering Standards

## 1. Environment & Build Verification
- **Target Stack:** C# / .NET (SDK-style `.csproj`), Visual Studio 2026 solution (`.sln`).
- **Build & Test Verification:** After modifying C# files, `.csproj` files, or XAML, run `dotnet build -c Release` (and `dotnet build -c Debug`) from the solution root to verify zero compilation errors before concluding your task. Run `dotnet test` if test projects exist.
- **Project File Hygiene:** Prefer SDK-style implicit globbing. Do not manually add `<Compile Include="..." />` entries to `.csproj` files unless explicitly required for custom build actions or linked files.
- **Visual Studio Locking:** Never delete or modify `.vs/`, `bin/`, or `obj/` directories while Visual Studio may have the solution open.

## 2. Architecture & Design Patterns
- **UI Separation (MVVM / MVP):** Keep code-behind (`.xaml.cs` or `Form.cs`) strictly limited to UI lifecycle events, focus management, and view-specific visual state. Place all business logic, state, and commands inside ViewModels or dedicated Services.
- **Repository Pattern & Dependency Injection:**
  - All data access and external I/O must sit behind explicit interfaces (e.g., `IRepository`, `IService`).
  - Register services and repositories via Constructor Injection; avoid static service locators or `new`-ing up concrete data dependencies inside UI layers.
- **Asynchronous I/O:** Use `async`/`await` end-to-end for all file system, database, and network operations. Never block the UI thread with `.Result`, `.Wait()`, or `Task.Run()` wrappers around synchronous UI calls. Propagate `CancellationToken` where applicable.

## 3. Logging, Error Handling & Security
- **Structured Logging (Serilog):** Use Serilog (`ILogger`) with structured message templates (e.g., `_logger.LogInformation("Loaded {RecordCount} items from {Source}", count, source)`) rather than string interpolation inside log calls.
- **Exception Handling:** Catch specific exceptions at service/repository boundaries, log context with Serilog, and surface clean, actionable error states to the UI rather than swallowing exceptions silently.
- **Secrets & Local Data:** Never hardcode API keys, connection strings, or signing certificates. Use Windows DPAPI (`ProtectedData`), environment variables, or local user secrets outside source control.

## 4. Git & GitHub Actions CI/CD Workflows
- **Branch & Push Guardrails:**
  - Verify the active Git branch before making edits. Never apply multi-file edits directly on `main` or `master`—prompt the user to switch to a feature/fix branch first if on `main`.
  - Never execute `git push`, `git tag`, or `gh release create` automatically; leave remote pushes and release triggers for manual user approval.
- **Workflow & Solution Parity (`.github/workflows/*.yml`):**
  - Whenever `.csproj` target frameworks, solution paths, build configurations, or packaging scripts (e.g., Inno Setup `.iss` files) change, inspect `.github/workflows/` and update any affected build, test, or release steps so CI stays in sync with local builds.
  - Ensure CI `dotnet restore`, `dotnet build`, and `dotnet publish` flags (such as `--runtime win-x64`, `--self-contained`, or version properties) match local release expectations.
- **GitHub Actions Best Practices & Verification:**
  - Pin official actions to major versions (e.g., `actions/checkout@v4`, `actions/setup-dotnet@v4`) and specify explicit least-privilege `permissions:` blocks (`contents: read` by default, `contents: write` only on release jobs).
  - Never echo secrets, signing tokens, or environment variables in workflow logs. Use `${{ secrets.* }}` and clean up temporary certificate/artifact files in `if: always()` steps.
  - Validate YAML syntax and schema after editing any workflow file (using `actionlint` if installed in the environment, or strict YAML parsing) and verify local build commands mirror the workflow steps.

## 5. Coding Conventions & Diff Discipline
- Enable and respect nullable reference types (`#nullable enable`). Fix nullability warnings properly instead of suppressing them with `!` unless guaranteed by framework lifecycle invariants.
- Keep diffs focused on the requested task. Do not reformat unrelated files, reorder `using` directives across untouched classes, or rename public symbols without explicit approval.
- When proposing architectural or CI/CD changes with meaningful trade-offs, briefly outline the **Pros** and **Cons** before applying major structural refactors.

