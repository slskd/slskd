# Browse Deep Links and Lazy File Loading Design

## Summary

Implement persistent browse URLs and fix large-share browse lag as one Browse page redesign. The web UI will use a lightweight directory index for the tree and load files only for the selected directory. Persistent URLs will use the path-style shape `/browse/:username/*directory`, so links from search results can open a user's folder directly.

## Goals

- Support stable URLs for browsing a user's shares and a specific directory.
- Add direct links from search result directories to the Browse page.
- Prevent the Browse page from becoming unresponsive for users with very large share lists.
- Preserve existing browse progress feedback and download behavior.
- Keep existing backend browse behavior compatible unless investigation during implementation proves it is private to the current Browse page.

## Non-goals

- Change the Soulseek peer browse protocol.
- Add full file pagination across all browse results.
- Cache remote user shares permanently server-side.
- Redesign the visual appearance of the Browse page beyond states needed for lazy loading.

## Recommended approach

Add a browse-index response for the web UI. The server will still use Soulseek browse to retrieve the user's share list, but the API response consumed by the Browse page will include only directory metadata: directory name, file count, lock state, and summary counts. It will not include each directory's file array.

The Browse page will build and render the directory tree from that lightweight index. When a directory is selected, the page will request that directory's files through the existing `/users/{username}/directory` API and render the file list panel only for that selected directory.

## URL and routing

The canonical route will be path-style:

```text
/browse/:username/*directory
```

The username and directory path will be URL-decoded when reading route params and URL-encoded when generating links. Directory paths may contain either `\` or `/` internally, but generated route links should normalize to URL path segments while preserving the original directory value for API calls.

Opening a persistent URL should:

1. Read the username and optional directory from the route.
2. Start the browse-index request automatically.
3. Build the directory tree when the index arrives.
4. Select the routed directory if it exists.
5. Fetch files for the selected directory and show them in the file panel.

If the route has only a username, the page should auto-browse the user and show the directory tree without selecting a folder.

## Browse page state

The route should be the source of truth for the browsed username and selected directory. UI actions should update browser history:

- Manual username search navigates to `/browse/:username`.
- Selecting a directory navigates to `/browse/:username/*directory`.
- Clearing browse state navigates back to `/browse`.
- Search result folder links navigate directly to `/browse/:username/*directory`.

The Browse page should no longer persist the full browse tree and file payload in localStorage. It will persist only the last username and directory path if the existing "resume last browse" behavior is retained.

## Loading behavior

Initial browse loading remains a page-level pending state with browse progress. Directory file loading is a separate selected-directory state:

- The tree remains interactive after the browse index has loaded.
- Selecting a folder shows a loader in the file panel while files are fetched.
- Re-selecting a folder reuses already loaded files during the current page lifetime.
- Download selection behavior remains scoped to the currently displayed files.

## Error handling

Browse-level failures keep the existing browse error behavior for offline, blacklisted, forbidden, or failed browse-index requests.

Directory-level failures should not discard the loaded tree. If a routed directory does not exist in the returned index, show a directory-level message and leave the browsed user visible. If fetching files for a selected directory fails, keep the selected folder open and show an inline error or toast consistent with the existing search directory fetch behavior.

## Backend components

Add or reuse DTOs under the Users API area for the browse-index response. The DTO should separate summary metadata from directory metadata and avoid serializing file arrays for tree construction.

The existing full browse endpoint remains unchanged for compatibility. The Browse page calls this new endpoint:

```text
GET /api/v0/users/{username}/browse/index
```

The endpoint should keep the same relay-agent guard, blacklist behavior, authorization policy, and expected domain exception handling as the existing browse endpoint.

## Frontend components

Update `src\web\src\lib\users.js` with a browse-index API helper and keep the existing directory contents helper.

Update the Browse route in `App.jsx` to accept optional username and directory params. Update `Browse.jsx` so it:

- Parses route params on mount and route changes.
- Requests the browse index instead of full browse payloads for the tree.
- Builds the tree from directory metadata only.
- Selects routed or clicked directories by name.
- Fetches selected directory files on demand.
- Updates history when browse state changes.

Update search result directory rendering so each directory can link to the canonical Browse route for that user and directory.

## Testing

Backend unit tests should cover browse-index DTO shaping: directory entries omit file arrays, preserve file counts, preserve locked-directory metadata, and preserve existing forbidden/not-found behavior.

Frontend tests should cover route parsing and link generation helpers, Browse auto-loading from username and directory params, directory selection updating history, selected directory file loading, and search results generating Browse links.

## Implementation notes

The current `Browse.jsx` tree building uses repeated child filtering by prefix and renders the full recursive tree. The lazy-file design removes the largest file payload cost, but implementation should also avoid obviously quadratic tree construction if large directory counts still cause lag. A path-segment map or parent-child lookup can build the tree in one pass and keeps the tree work bounded to directory count rather than directory count multiplied by depth.

Do not introduce broad catch-and-ignore behavior. Preserve current API status semantics and surface directory-level errors explicitly.
