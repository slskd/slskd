# Features

## Persistent URL for browsing a user shares

Be able to have a persistent URL to a user's share and directory

e.g. https://localhost:5031/browse/user/path/to/folder

This would also allow us to provide a direct link from the search page to that folder that the user can click and then easily discover more music by the artist (assuming good directory organization).

# Bugs

## Browse page can lag out for users that have many files

When browsing the shares of a user who has lots of files, the browse page becomes unresponsive.

### Potential solutions

- Introduce paging (only load x number of files at a time)
- Just send directories to browser first, then load files on-demand when a directory is selected.
  - Might dove-tail with the persistent URL feature.