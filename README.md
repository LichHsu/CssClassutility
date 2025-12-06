# CssClassUtility

A powerful MCP server for analyzing, processing, and consolidating CSS files.
Now supports strongly-typed parameters for enhanced AI interaction.

## Tools

### 1. `analyze_css`
Analyzes CSS content and usage.
*   **Parameters**:
    *   `path` (string): Path to file or directory.
    *   `analysisType` (string):
        *   `Variables`: Suggests variables from repeated values.
        *   `Components`: Groups selectors by common prefixes.
        *   `Missing`: Finds classes used in `classesToCheck` but missing in CSS.
        *   `Usage`: Traces usage of a class in the project.
    *   `options`: (Object)
        *   `threshold`: Min frequency for variable suggestion.
        *   `classesToCheck`: List of class names (for Missing).
        *   `className`: Target class (for Usage).
        *   `projectRoot`: Search scope (for Usage).

### 2. `edit_css`
Batch edits CSS files (In-Memory Processing).
*   **Parameters**:
    *   `path` (string): Path to CSS file.
    *   `operations`: (List of Objects)
        *   `op`: `Set`, `Remove` (property), `Remove` (class).
        *   `className`: Target CSS class (e.g., `btn-primary`).
        *   `key`: CSS property (e.g., `color`).
        *   `value`: CSS value.
        *   `source`: Source file path (for Merge op).
        *   `strategy`: `Overwrite`, `FillMissing`.

### 3. `consolidate_css`
Merges multiple CSS files into one.
*   **Parameters**:
    *   `sourceFiles` (List<string>): List of paths to merge.
    *   `outputFile` (string): Destination path.

## Development
Run `dotnet build` to compile.
Run `CssClassUtility.exe --test` to execute internal unit tests.