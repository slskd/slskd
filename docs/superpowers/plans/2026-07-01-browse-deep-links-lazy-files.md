# Browse Deep Links and Lazy File Loading Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persistent Browse URLs and prevent large-share Browse page lag by loading a lightweight directory index first and files only for the selected directory.

**Architecture:** The backend adds a new `/api/v0/users/{username}/browse/index` endpoint that shapes `Soulseek.BrowseResponse` into summary and directory metadata without serializing file arrays. The frontend treats `/browse/:username?/*directory?` as the source of truth, builds the tree from the index, loads selected directory files through the existing directory endpoint, and adds Browse links to search result directories.

**Tech Stack:** .NET 10 ASP.NET Core Web API, xUnit, Moq, React 16, react-router-dom 5, Semantic UI React, Jest via CRACO/react-scripts.

## Global Constraints

- Use path-style persistent Browse URLs: `/browse/:username/*directory`.
- Keep the existing full browse endpoint unchanged for compatibility.
- The Browse page must call `GET /api/v0/users/{username}/browse/index` for tree loading.
- The browse-index response must include directory metadata and summary counts but omit file arrays.
- Directory file loading must use the existing `/users/{username}/directory` API.
- The route is the source of truth for browsed username and selected directory.
- Do not persist the full browse tree or file payload in localStorage.
- Preserve relay-agent guard, blacklist behavior, authorization policy, and expected domain exception handling from the existing browse endpoint.
- Preserve browse-level progress feedback and current download behavior.
- Use repository conventions: backend tests in `tests\slskd.Tests.Unit`, web tests in `src\web\src`, single quotes in web code, and Windows paths in commands.

---

## File Structure

- Create `src\slskd\Users\API\DTO\BrowseIndexResponse.cs`: response DTO containing summary counts and lightweight directory entries.
- Modify `src\slskd\Users\API\Controllers\UsersController.cs`: add `GET {username}/browse/index` endpoint using `BrowseIndexResponse.FromSoulseek`.
- Create `tests\slskd.Tests.Unit\Users\API\DTO\BrowseIndexResponseTests.cs`: unit tests for shaping directories, locked directories, counts, and file omission.
- Create `src\web\src\components\Browse\browseRoutes.js`: route helper functions for canonical Browse URL generation and route param decoding.
- Create `src\web\src\components\Browse\browseRoutes.test.js`: Jest tests for URL encoding, separator normalization, and path decoding.
- Modify `src\web\src\lib\users.js`: add `browseIndex({ username })`.
- Modify `src\web\src\components\App.jsx`: route `/browse`, `/browse/:username`, and `/browse/:username/*directory` to `Browse`.
- Modify `src\web\src\components\Browse\Browse.jsx`: replace full browse state with index loading plus selected-directory file loading.
- Modify `src\web\src\components\Browse\Directory.jsx`: make it safe to render a pending or failed selected directory panel.
- Modify `src\web\src\components\Search\Response.jsx`: add Browse links from search result directories.
- Create `src\web\src\components\Search\Response.test.js`: focused test proving search result directories use the canonical Browse link helper.

---

### Task 1: Backend Browse Index DTO

**Files:**
- Create: `src\slskd\Users\API\DTO\BrowseIndexResponse.cs`
- Test: `tests\slskd.Tests.Unit\Users\API\DTO\BrowseIndexResponseTests.cs`

**Interfaces:**
- Consumes: `Soulseek.BrowseResponse`, `Soulseek.Directory`, and `Soulseek.File`.
- Produces:
  - `BrowseIndexResponse.FromSoulseek(BrowseResponse response): BrowseIndexResponse`
  - `BrowseIndexResponse.Directories: IReadOnlyCollection<BrowseIndexDirectory>`
  - `BrowseIndexResponse.LockedDirectories: IReadOnlyCollection<BrowseIndexDirectory>`
  - `BrowseIndexDirectory.Name: string`
  - `BrowseIndexDirectory.FileCount: int`

- [ ] **Step 1: Write the failing DTO tests**

Create `tests\slskd.Tests.Unit\Users\API\DTO\BrowseIndexResponseTests.cs`:

```csharp
namespace slskd.Tests.Unit.Users.API.DTO
{
    using System.Linq;
    using slskd.Users.API;
    using Soulseek;
    using Xunit;

    public class BrowseIndexResponseTests
    {
        [Fact]
        public void FromSoulseek_Maps_Unlocked_Directory_Metadata_Without_Files()
        {
            var response = new BrowseResponse(
                directories: new[]
                {
                    new Directory("Music\\Artist", new[]
                    {
                        new File(1, "one.flac", 123, "flac", []),
                        new File(1, "two.flac", 456, "flac", []),
                    }),
                },
                lockedDirectories: []);

            var result = BrowseIndexResponse.FromSoulseek(response);

            var directory = Assert.Single(result.Directories);
            Assert.Equal("Music\\Artist", directory.Name);
            Assert.Equal(2, directory.FileCount);
            Assert.Empty(result.LockedDirectories);
            Assert.Equal(1, result.Info.Directories);
            Assert.Equal(2, result.Info.Files);
            Assert.Equal(0, result.Info.LockedDirectories);
            Assert.Equal(0, result.Info.LockedFiles);
            Assert.DoesNotContain(
                typeof(BrowseIndexDirectory).GetProperties().Select(p => p.Name),
                name => name == "Files");
        }

        [Fact]
        public void FromSoulseek_Maps_Locked_Directory_Metadata_And_Counts()
        {
            var response = new BrowseResponse(
                directories: [],
                lockedDirectories: new[]
                {
                    new Directory("Locked\\Artist", new[]
                    {
                        new File(1, "secret.mp3", 789, "mp3", []),
                    }),
                });

            var result = BrowseIndexResponse.FromSoulseek(response);

            Assert.Empty(result.Directories);
            var directory = Assert.Single(result.LockedDirectories);
            Assert.Equal("Locked\\Artist", directory.Name);
            Assert.Equal(1, directory.FileCount);
            Assert.Equal(0, result.Info.Directories);
            Assert.Equal(0, result.Info.Files);
            Assert.Equal(1, result.Info.LockedDirectories);
            Assert.Equal(1, result.Info.LockedFiles);
        }

        [Fact]
        public void FromSoulseek_Treats_Null_Collections_As_Empty()
        {
            var response = new BrowseResponse(directories: null, lockedDirectories: null);

            var result = BrowseIndexResponse.FromSoulseek(response);

            Assert.Empty(result.Directories);
            Assert.Empty(result.LockedDirectories);
            Assert.Equal(0, result.Info.Directories);
            Assert.Equal(0, result.Info.Files);
            Assert.Equal(0, result.Info.LockedDirectories);
            Assert.Equal(0, result.Info.LockedFiles);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj --filter "FullyQualifiedName~BrowseIndexResponseTests"
```

Expected: FAIL because `BrowseIndexResponse` and `BrowseIndexDirectory` do not exist.

- [ ] **Step 3: Add the DTO implementation**

Create `src\slskd\Users\API\DTO\BrowseIndexResponse.cs`:

```csharp
// <copyright file="BrowseIndexResponse.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Users.API
{
    using System.Collections.Generic;
    using System.Linq;
    using Soulseek;

    public record BrowseIndexInfo
    {
        public int Directories { get; init; }
        public int Files { get; init; }
        public int LockedDirectories { get; init; }
        public int LockedFiles { get; init; }
    }

    public record BrowseIndexDirectory
    {
        public string Name { get; init; }
        public int FileCount { get; init; }

        public static BrowseIndexDirectory FromSoulseek(Directory directory)
        {
            return new BrowseIndexDirectory
            {
                Name = directory.Name,
                FileCount = directory.FileCount,
            };
        }
    }

    public record BrowseIndexResponse
    {
        public BrowseIndexInfo Info { get; init; }
        public IReadOnlyCollection<BrowseIndexDirectory> Directories { get; init; }
        public IReadOnlyCollection<BrowseIndexDirectory> LockedDirectories { get; init; }

        public static BrowseIndexResponse FromSoulseek(BrowseResponse response)
        {
            var directories = (response.Directories ?? Enumerable.Empty<Directory>()).ToList();
            var lockedDirectories = (response.LockedDirectories ?? Enumerable.Empty<Directory>()).ToList();

            return new BrowseIndexResponse
            {
                Info = new BrowseIndexInfo
                {
                    Directories = directories.Count,
                    Files = directories.Sum(directory => directory.FileCount),
                    LockedDirectories = lockedDirectories.Count,
                    LockedFiles = lockedDirectories.Sum(directory => directory.FileCount),
                },
                Directories = directories.Select(BrowseIndexDirectory.FromSoulseek).ToList(),
                LockedDirectories = lockedDirectories.Select(BrowseIndexDirectory.FromSoulseek).ToList(),
            };
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj --filter "FullyQualifiedName~BrowseIndexResponseTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src\slskd\Users\API\DTO\BrowseIndexResponse.cs tests\slskd.Tests.Unit\Users\API\DTO\BrowseIndexResponseTests.cs
git commit -m "Add browse index DTO" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Backend Browse Index Endpoint

**Files:**
- Modify: `src\slskd\Users\API\Controllers\UsersController.cs`
- Test: `tests\slskd.Tests.Unit\Users\API\Controllers\UsersControllerBrowseIndexTests.cs`

**Interfaces:**
- Consumes: `BrowseIndexResponse.FromSoulseek(BrowseResponse response)` from Task 1.
- Produces: `GET /api/v0/users/{username}/browse/index` returning `BrowseIndexResponse`.

- [ ] **Step 1: Write failing controller tests**

Create `tests\slskd.Tests.Unit\Users\API\Controllers\UsersControllerBrowseIndexTests.cs`:

```csharp
namespace slskd.Tests.Unit.Users.API.Controllers
{
    using System.Threading.Tasks;
    using System.Net;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Users;
    using slskd.Users.API;
    using Soulseek;
    using Xunit;

    public class UsersControllerBrowseIndexTests
    {
        [Fact]
        public async Task BrowseIndex_Returns_NotFound_Given_Blacklisted_User()
        {
            var fixture = GetFixture();
            fixture.Users.Setup(users => users.IsBlacklisted("bad-user", null, true)).Returns(true);

            var result = await fixture.Controller.BrowseIndex("bad-user");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task BrowseIndex_Returns_NotFound_Given_Offline_User()
        {
            var fixture = GetFixture();
            fixture.Client
                .Setup(client => client.BrowseAsync("offline-user"))
                .ThrowsAsync(new UserOfflineException("offline-user"));

            var result = await fixture.Controller.BrowseIndex("offline-user");

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("offline-user", notFound.Value);
        }

        [Fact]
        public async Task BrowseIndex_Returns_Index_Response_Given_Browse_Response()
        {
            var fixture = GetFixture();
            fixture.Client
                .Setup(client => client.BrowseAsync("good-user"))
                .ReturnsAsync(new BrowseResponse(
                    directories: [new Directory("Music", [new File(1, "song.mp3", 123, "mp3", [])])],
                    lockedDirectories: []));

            var result = await fixture.Controller.BrowseIndex("good-user");

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<BrowseIndexResponse>(ok.Value);
            Assert.Equal(1, response.Info.Directories);
            Assert.Equal(1, response.Info.Files);
            Assert.Equal("Music", Assert.Single(response.Directories).Name);
        }

        private static Fixture GetFixture()
        {
            var client = new Mock<ISoulseekClient>();
            var browseTracker = new Mock<IBrowseTracker>();
            var users = new Mock<IUserService>();
            var options = new Mock<IOptionsSnapshot<Options>>();

            users.Setup(service => service.IsBlacklisted(It.IsAny<string>(), It.IsAny<IPAddress>(), true)).Returns(false);

            return new Fixture(
                client,
                browseTracker,
                users,
                new UsersController(client.Object, browseTracker.Object, users.Object, options.Object));
        }

        private record Fixture(
            Mock<ISoulseekClient> Client,
            Mock<IBrowseTracker> BrowseTracker,
            Mock<IUserService> Users,
            UsersController Controller);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj --filter "FullyQualifiedName~UsersControllerBrowseIndexTests"
```

Expected: FAIL because `UsersController.BrowseIndex` does not exist.

- [ ] **Step 3: Add the endpoint**

In `src\slskd\Users\API\Controllers\UsersController.cs`, insert this action after the existing `Browse` action and before `BrowseStatus`:

```csharp
        /// <summary>
        ///     Retrieves the directory index for the files shared by the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/browse/index")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(BrowseIndexResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> BrowseIndex([FromRoute, UrlEncoded, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (Users.IsBlacklisted(username))
            {
                return NotFound();
            }

            try
            {
                var result = await Client.BrowseAsync(username);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    BrowseTracker.TryRemove(username);
                });

                return Ok(BrowseIndexResponse.FromSoulseek(result));
            }
            catch (UserOfflineException ex)
            {
                return NotFound(ex.Message);
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj --filter "FullyQualifiedName~BrowseIndex"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src\slskd\Users\API\Controllers\UsersController.cs tests\slskd.Tests.Unit\Users\API\Controllers\UsersControllerBrowseIndexTests.cs
git commit -m "Add user browse index endpoint" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Browse Route Helpers and API Helper

**Files:**
- Create: `src\web\src\components\Browse\browseRoutes.js`
- Create: `src\web\src\components\Browse\browseRoutes.test.js`
- Modify: `src\web\src\lib\users.js`
- Test: `src\web\src\components\Browse\browseRoutes.test.js`

**Interfaces:**
- Produces:
  - `buildBrowseUrl({ username, directory, urlBase = '' }): string`
  - `decodeBrowseParams(params): { username: string, directory: string }`
  - `toBrowsePathSegments(directory): string`
  - `fromBrowsePathSegments(path): string`
  - `users.browseIndex({ username }): Promise<BrowseIndexResponse>`

- [ ] **Step 1: Write failing route helper tests**

Create `src\web\src\components\Browse\browseRoutes.test.js`:

```javascript
import {
  buildBrowseUrl,
  decodeBrowseParams,
  fromBrowsePathSegments,
  toBrowsePathSegments,
} from './browseRoutes';

describe('browseRoutes', () => {
  it('builds a username-only browse URL', () => {
    expect(buildBrowseUrl({ username: 'some user' })).toBe(
      '/browse/some%20user',
    );
  });

  it('builds a canonical browse URL with directory path segments', () => {
    expect(
      buildBrowseUrl({
        directory: 'Music\\Artist Name\\Album/Disc 1',
        username: 'some/user',
      }),
    ).toBe('/browse/some%2Fuser/Music/Artist%20Name/Album/Disc%201');
  });

  it('preserves a configured urlBase', () => {
    expect(
      buildBrowseUrl({
        directory: 'Music\\Artist',
        urlBase: '/ui',
        username: 'alice',
      }),
    ).toBe('/ui/browse/alice/Music/Artist');
  });

  it('decodes browse params from react-router params', () => {
    expect(
      decodeBrowseParams({
        directory: 'Music/Artist%20Name/Album',
        username: 'some%2Fuser',
      }),
    ).toEqual({
      directory: 'Music\\Artist Name\\Album',
      username: 'some/user',
    });
  });

  it('round-trips directory separators through URL path segments', () => {
    const directory = 'A\\B/C';

    expect(fromBrowsePathSegments(toBrowsePathSegments(directory))).toBe(
      'A\\B\\C',
    );
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
Set-Location .\src\web; npm test -- --watchAll=false browseRoutes.test.js
```

Expected: FAIL because `browseRoutes.js` does not exist.

- [ ] **Step 3: Add route helpers**

Create `src\web\src\components\Browse\browseRoutes.js`:

```javascript
const trimSlashes = (value = '') => value.replace(/^\/+|\/+$/g, '');

export const toBrowsePathSegments = (directory = '') =>
  trimSlashes(directory)
    .split(/[\\/]+/)
    .filter(Boolean)
    .map((segment) => encodeURIComponent(segment))
    .join('/');

export const fromBrowsePathSegments = (path = '') =>
  trimSlashes(path)
    .split('/')
    .filter(Boolean)
    .map((segment) => decodeURIComponent(segment))
    .join('\\');

export const buildBrowseUrl = ({ directory = '', urlBase = '', username }) => {
  if (!username) {
    return `${urlBase}/browse`;
  }

  const encodedUsername = encodeURIComponent(username);
  const encodedDirectory = toBrowsePathSegments(directory);

  return `${urlBase}/browse/${encodedUsername}${
    encodedDirectory ? `/${encodedDirectory}` : ''
  }`;
};

export const decodeBrowseParams = (params = {}) => ({
  directory: fromBrowsePathSegments(params.directory),
  username: params.username ? decodeURIComponent(params.username) : '',
});
```

- [ ] **Step 4: Add the browse-index API helper**

Modify `src\web\src\lib\users.js` by adding this function after `browse`:

```javascript
export const browseIndex = async ({ username }) => {
  return (
    await api.get(`/users/${encodeURIComponent(username)}/browse/index`)
  ).data;
};
```

- [ ] **Step 5: Run tests to verify they pass**

Run:

```powershell
Set-Location .\src\web; npm test -- --watchAll=false browseRoutes.test.js
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src\web\src\components\Browse\browseRoutes.js src\web\src\components\Browse\browseRoutes.test.js src\web\src\lib\users.js
git commit -m "Add browse route helpers" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: Browse Page Lazy Directory Loading

**Files:**
- Modify: `src\web\src\components\App.jsx`
- Modify: `src\web\src\components\Browse\Browse.jsx`
- Modify: `src\web\src\components\Browse\Directory.jsx`
- Test: `src\web\src\components\Browse\browseRoutes.test.js`

**Interfaces:**
- Consumes:
  - `users.browseIndex({ username })`
  - `users.getDirectoryContents({ username, directory })`
  - `buildBrowseUrl({ username, directory, urlBase })`
  - `decodeBrowseParams(match.params)`
- Produces:
  - Route-aware Browse UI where `/browse/:username/*directory` auto-loads index and selected directory files.
  - In-memory `directoryFilesByName` cache for selected directory files.

- [ ] **Step 1: Update the App route**

In `src\web\src\components\App.jsx`, replace the Browse route at lines 589-594 with the more specific optional route:

```jsx
                    <Route
                      path={[
                        `${urlBase}/browse/:username/:directory*`,
                        `${urlBase}/browse/:username`,
                        `${urlBase}/browse`,
                      ]}
                      render={(props) =>
                        this.withTokenCheck(<Browse {...props} />)
                      }
                    />
```

- [ ] **Step 2: Replace Browse state shape and imports**

In `src\web\src\components\Browse\Browse.jsx`, remove the `lz-string` import and add route helper imports:

```javascript
import {
  buildBrowseUrl,
  decodeBrowseParams,
} from './browseRoutes';
```

Replace `initialState` with:

```javascript
const initialState = {
  browseError: undefined,
  browseState: 'idle',
  browseStatus: 0,
  directoryError: undefined,
  directoryFilesByName: {},
  directoryState: 'idle',
  info: {
    directories: 0,
    files: 0,
    lockedDirectories: 0,
    lockedFiles: 0,
  },
  interval: undefined,
  selectedDirectory: {},
  separator: '\\',
  tree: [],
  username: '',
};
```

- [ ] **Step 3: Add route synchronization methods**

Inside `Browse` before `componentDidMount`, add:

```javascript
  getRouteState = () => decodeBrowseParams(this.props.match.params);

  syncFromRoute = () => {
    const { directory, username } = this.getRouteState();

    if (!username) {
      this.setState(initialState, () => this.saveState());
      return;
    }

    if (username !== this.state.username || this.state.browseState === 'idle') {
      this.setState(
        {
          ...initialState,
          interval: this.state.interval,
          username,
        },
        () => this.browse(username, directory),
      );
      return;
    }

    if (directory && directory !== this.state.selectedDirectory.name) {
      this.selectDirectoryByName(directory);
    }
  };
```

- [ ] **Step 4: Update lifecycle methods**

Replace `componentDidMount` and add `componentDidUpdate`:

```javascript
  componentDidMount() {
    this.loadState();
    this.setState(
      {
        interval: window.setInterval(this.fetchStatus, 500),
      },
      () => {
        this.syncFromRoute();
        this.saveState();
      },
    );

    document.addEventListener('keyup', this.keyUp, false);
  }

  componentDidUpdate(previousProps) {
    if (this.props.location.pathname !== previousProps.location.pathname) {
      this.syncFromRoute();
    }
  }
```

Keep `componentWillUnmount`, but ensure it still clears the interval and removes the key listener.

- [ ] **Step 5: Replace browse, saveState, and loadState**

Replace the existing `browse`, `saveState`, and `loadState` methods with:

```javascript
  browse = (usernameOverride, directoryToSelect) => {
    const username = usernameOverride || this.inputtext.inputRef.current.value;

    if (!usernameOverride) {
      this.props.history.push(buildBrowseUrl({ username }));
      return;
    }

    this.setState(
      {
        browseError: undefined,
        browseState: 'pending',
        directoryError: undefined,
        directoryFilesByName: {},
        directoryState: 'idle',
        selectedDirectory: {},
        username,
      },
      () => {
        users
          .browseIndex({ username })
          .then((response) => {
            let { directories } = response;
            const { lockedDirectories } = response;

            let separator;
            const allDirectories = directories.concat(
              lockedDirectories.map((d) => ({ ...d, locked: true })),
            );

            allDirectories.forEach((directory) => {
              if (!separator) {
                if (directory.name.includes('\\')) separator = '\\';
                else if (directory.name.includes('/')) separator = '/';
              }
            });

            separator = separator || '\\';

            this.setState(
              {
                browseError: undefined,
                browseState: 'complete',
                info: response.info,
                separator,
                tree: this.getDirectoryTree({
                  directories: allDirectories,
                  separator,
                }),
              },
              () => {
                this.saveState();
                if (directoryToSelect) {
                  this.selectDirectoryByName(directoryToSelect);
                }
              },
            );
          })
          .catch((error) =>
            this.setState({ browseError: error, browseState: 'error' }),
          );
      },
    );
  };

  saveState = () => {
    this.inputtext.inputRef.current.value = this.state.username;
    this.inputtext.inputRef.current.disabled =
      this.state.browseState !== 'idle' && this.state.browseState !== 'complete';

    localStorage.setItem(
      'soulseek-example-browse-state',
      JSON.stringify({
        selectedDirectoryName: this.state.selectedDirectory.name || '',
        username: this.state.username,
      }),
    );
  };

  loadState = () => {
    const routeState = this.getRouteState();
    if (routeState.username) {
      return;
    }

    try {
      const saved = JSON.parse(
        localStorage.getItem('soulseek-example-browse-state') || '{}',
      );

      if (saved.username) {
        this.props.history.replace(
          buildBrowseUrl({
            directory: saved.selectedDirectoryName,
            username: saved.username,
          }),
        );
      }
    } catch (error) {
      console.error(error);
    }
  };
```

- [ ] **Step 6: Add selected-directory file loading**

Add these methods after `getChildDirectories`:

```javascript
  findDirectoryByName = (directories, directoryName) => {
    for (const directory of directories || []) {
      if (directory.name === directoryName) {
        return directory;
      }

      const child = this.findDirectoryByName(directory.children, directoryName);
      if (child) {
        return child;
      }
    }

    return undefined;
  };

  selectDirectoryByName = (directoryName) => {
    const directory = this.findDirectoryByName(this.state.tree, directoryName);

    if (!directory) {
      this.setState({
        directoryError: `Directory '${directoryName}' was not found in this browse response.`,
        directoryState: 'error',
        selectedDirectory: { name: directoryName },
      });
      return;
    }

    this.selectDirectory(directory, { updateHistory: false });
  };

  loadDirectoryFiles = (directory) => {
    if (this.state.directoryFilesByName[directory.name]) {
      this.setState({ directoryError: undefined, directoryState: 'complete' });
      return;
    }

    this.setState(
      { directoryError: undefined, directoryState: 'pending' },
      async () => {
        try {
          const allDirectories = await users.getDirectoryContents({
            directory: directory.name,
            username: this.state.username,
          });
          const rootDirectory = allDirectories?.[0];

          if (!rootDirectory) {
            throw new Error('No directories were included in the response');
          }

          this.setState((previousState) => ({
            directoryFilesByName: {
              ...previousState.directoryFilesByName,
              [directory.name]: (rootDirectory.files || []).map((file) => ({
                ...file,
                filename: `${directory.name}${previousState.separator}${file.filename}`,
              })),
            },
            directoryState: 'complete',
          }));
        } catch (error) {
          console.error(error);
          this.setState({
            directoryError: error?.response?.data ?? error?.message ?? error,
            directoryState: 'error',
          });
        }
      },
    );
  };
```

- [ ] **Step 7: Replace directory selection and clear handlers**

Replace `selectDirectory` and `handleDeselectDirectory`:

```javascript
  selectDirectory = (directory, { updateHistory = true } = {}) => {
    this.setState(
      {
        directoryError: undefined,
        selectedDirectory: { ...directory, children: [] },
      },
      () => {
        if (updateHistory) {
          this.props.history.push(
            buildBrowseUrl({
              directory: directory.name,
              username: this.state.username,
            }),
          );
        }

        this.loadDirectoryFiles(directory);
        this.saveState();
      },
    );
  };

  handleDeselectDirectory = () => {
    this.props.history.push(buildBrowseUrl({ username: this.state.username }));
  };
```

Replace `clear`:

```javascript
  clear = () => {
    this.setState(initialState, () => {
      localStorage.removeItem('soulseek-example-browse-state');
      this.props.history.push(buildBrowseUrl({}));
      this.inputtext.focus();
    });
  };
```

- [ ] **Step 8: Update render to use selected-directory file state**

In `render`, include `directoryError`, `directoryFilesByName`, and `directoryState` from state:

```javascript
      directoryError,
      directoryFilesByName,
      directoryState,
```

Replace the existing `files` calculation with:

```javascript
    const files = directoryFilesByName[name] || [];
```

Replace the `<Directory />` props block with:

```jsx
                  <Directory
                    error={directoryError}
                    files={files}
                    loading={directoryState === 'pending'}
                    locked={locked}
                    marginTop={-20}
                    name={name}
                    onClose={this.handleDeselectDirectory}
                    username={username}
                  />
```

- [ ] **Step 9: Update Directory panel loading and error rendering**

In `src\web\src\components\Browse\Directory.jsx`, update imports:

```javascript
import { Button, Card, Icon, Label, Loader, Message } from 'semantic-ui-react';
```

Update the `componentDidUpdate` guard:

```javascript
    if (
      this.props.name !== previousProps.name ||
      this.props.files !== previousProps.files
    ) {
```

Update the prop destructuring in `render`:

```javascript
    const { error, loading, locked, marginTop, name, onClose, username } =
      this.props;
```

Insert this block after the `<FileList />` wrapper and before `</Card.Content>`:

```jsx
          {loading && (
            <Loader
              active
              inline="centered"
            >
              Loading directory files
            </Loader>
          )}
          {error && (
            <Message
              content={error}
              negative
            />
          )}
```

- [ ] **Step 10: Run targeted web build/test**

Run:

```powershell
Set-Location .\src\web; npm test -- --watchAll=false browseRoutes.test.js
Set-Location .\src\web; npm run build
```

Expected: tests PASS and build succeeds.

- [ ] **Step 11: Commit**

```powershell
git add src\web\src\components\App.jsx src\web\src\components\Browse\Browse.jsx src\web\src\components\Browse\Directory.jsx
git commit -m "Load browse directory files on demand" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Search Result Browse Links

**Files:**
- Modify: `src\web\src\components\Search\Response.jsx`
- Test: `src\web\src\components\Search\Response.test.js`

**Interfaces:**
- Consumes: `buildBrowseUrl({ username, directory })` from Task 3.
- Produces: a link from each search result directory to the canonical Browse route for that response username and directory.

- [ ] **Step 1: Add a small link helper test**

Create `src\web\src\components\Search\Response.test.js`:

```javascript
import { buildBrowseUrl } from '../Browse/browseRoutes';

describe('search response browse links', () => {
  it('builds a Browse link for a search result directory', () => {
    expect(
      buildBrowseUrl({
        directory: 'Music\\Artist Name\\Album',
        username: 'some user',
      }),
    ).toBe('/browse/some%20user/Music/Artist%20Name/Album');
  });
});
```

- [ ] **Step 2: Run test to verify it passes before UI wiring**

Run:

```powershell
Set-Location .\src\web; npm test -- --watchAll=false Response.test.js
```

Expected: PASS because Task 3 already implemented `buildBrowseUrl`.

- [ ] **Step 3: Add Browse links to directory footers**

In `src\web\src\components\Search\Response.jsx`, add imports:

```javascript
import { Link } from 'react-router-dom';
import { buildBrowseUrl } from '../Browse/browseRoutes';
```

Replace the footer in the `FileList` render with a two-action footer:

```jsx
                <div>
                  <button
                    disabled={fetchingDirectoryContents}
                    onClick={() =>
                      this.getFullDirectory(response.username, directory)
                    }
                    style={{
                      backgroundColor: 'transparent',
                      border: 'none',
                      cursor: 'pointer',
                      width: '100%',
                    }}
                    type="button"
                  >
                    <Icon
                      loading={fetchingDirectoryContents}
                      name={fetchingDirectoryContents ? 'circle notch' : 'search'}
                    />
                    Search for Additional Files in This Directory
                  </button>
                  <Link
                    to={buildBrowseUrl({
                      directory,
                      username: response.username,
                    })}
                  >
                    <Icon name="folder open" />
                    Browse This Directory
                  </Link>
                </div>
```

- [ ] **Step 4: Run targeted web tests and build**

Run:

```powershell
Set-Location .\src\web; npm test -- --watchAll=false Response.test.js browseRoutes.test.js
Set-Location .\src\web; npm run build
```

Expected: tests PASS and build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add src\web\src\components\Search\Response.jsx src\web\src\components\Search\Response.test.js
git commit -m "Link search directories to browse" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: Final Verification and Cleanup

**Files:**
- Modify only if verification exposes a bug in files changed by Tasks 1-5.

**Interfaces:**
- Consumes all previous task outputs.
- Produces a verified implementation branch ready for review.

- [ ] **Step 1: Run backend tests covering changed backend code**

Run:

```powershell
dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj --filter "FullyQualifiedName~BrowseIndex"
```

Expected: PASS.

- [ ] **Step 2: Run web tests covering changed frontend helpers**

Run:

```powershell
Set-Location .\src\web; npm test -- --watchAll=false browseRoutes.test.js Response.test.js
```

Expected: PASS.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
Set-Location .\src\web; npm run build
```

Expected: build succeeds.

- [ ] **Step 4: Run backend build**

Run:

```powershell
dotnet build .\src\slskd\slskd.csproj
```

Expected: build succeeds.

- [ ] **Step 5: Inspect git status**

Run:

```powershell
git --no-pager status --short
```

Expected: no unstaged or uncommitted implementation changes. The plan file may remain uncommitted if the implementer is only using it as a session artifact; commit it if the team wants plans versioned.

---

## Self-Review

- Spec coverage: Task 1 covers file omission and summary metadata; Task 2 covers the new endpoint and preserved backend semantics; Task 3 covers URL generation/parsing and API helper; Task 4 covers route-sourced Browse state, lazy file loading, localStorage reduction, and directory-level loading/error states; Task 5 covers search-result links; Task 6 covers verification.
- Placeholder scan: no unresolved placeholder markers or unspecified edge-case instructions remain.
- Type consistency: `BrowseIndexResponse`, `BrowseIndexDirectory`, `BrowseIndexInfo`, `buildBrowseUrl`, `decodeBrowseParams`, `toBrowsePathSegments`, `fromBrowsePathSegments`, and `browseIndex` names are used consistently across tasks.
