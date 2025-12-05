# Walkthrough: CSS Session Management

## Overview
The CSS Session Management feature allows you to edit CSS files in memory before saving them to disk. This is useful for batch operations, intermediate states, or when you want to validate changes before committing them.

## Usage

### 1. Start a Session
To start editing a file, use `start_css_session`. You can optionally provide a file path to load existing content.

```json
{
  "name": "start_css_session",
  "arguments": {
    "filePath": "d:\\path\\to\\style.css"
  }
}
```
**Returns:** A `CssSession` object containing the `sessionId`.

### 2. Update Content
Use the `sessionId` to update the content.

```json
{
  "name": "update_css_session_content",
  "arguments": {
    "sessionId": "guid-session-id",
    "newContent": ".class { color: red; }"
  }
}
```

### 3. Get Session Details
Check the current state of the session.

```json
{
  "name": "get_css_session",
  "arguments": {
    "sessionId": "guid-session-id"
  }
}
```

### 4. Save to Disk
When you are satisfied with the changes, save them to disk. You can overwrite the original file or save to a new path.

```json
{
  "name": "save_css_session",
  "arguments": {
    "sessionId": "guid-session-id",
    "targetPath": "d:\\path\\to\\new_style.css" // Optional
  }
}
```

### 5. Close Session
Always close the session when done to free up resources.

```json
{
  "name": "close_css_session",
  "arguments": {
    "sessionId": "guid-session-id"
  }
}
```

### 6. List Sessions
View all active sessions.

```json
{
  "name": "list_css_sessions",
  "arguments": {}
}
```
