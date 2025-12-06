# CssClassUtility

A powerful MCP server for analyzing, editing, and optimizing CSS files.

## Tools

### 1. `analyze_css`
Performs read-only analysis on CSS files.
*   **Parameters**:
    *   `path` (string): Absolute path to the CSS file.
    *   `analysisType` (string):
        *   `Variables`: Suggests extraction of repeated values (design tokens).
        *   `Components`: Groups selectors into logical components (e.g. `.btn`, `.btn:hover`).
        *   `Missing`: Checks for "orphaned" classes (used in Razor but missing in CSS).
        *   `Usage`: Traces where a specific class is used in the project.
    *   `options` (json string, optional): `{ "threshold": 3, "classesToCheck": ["classA"], "className": "btn" }`.

### 2. `edit_css`
Batch edits CSS in-memory for high performance.
*   **Parameters**:
    *   `path` (string): Absolute path to the CSS file.
    *   `operations` (json string): Array of operations.
        *   `{ "op": "Set", "className": "btn", "key": "color", "value": "red" }`
        *   `{ "op": "Remove", "className": "btn" }`
        *   `{ "op": "Merge", "source": "other.css:.btn", "strategy": "Overwrite" }`

### 3. `consolidate_css`
Merges multiple CSS files into one target file.
*   **Parameters**:
    *   `targetPath` (string): Destination file.
    *   `sourcePaths` (string[]): List of source files.
    *   `strategy` (string): `Overwrite` or `FillMissing`.

### 4. `purge_css`
Removes unused CSS classes (Dead Code Elimination).
*   **Parameters**:
    *   `path` (string): Target CSS file.
    *   `usedClassesJson` (json string): List of used classes.
    *   `allowListJson` (json string): Classes to strictly preserve.

### 5. `get_css_info`
Inspects a single CSS Class structure.
*   **Parameters**:
    *   `path` (string): CSS file.
    *   `className` (string): Class name.

## Development
Run `dotnet build` to compile.
Run `CssClassUtility.exe --test` to execute internal unit tests.