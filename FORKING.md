# Forking

This document explains what you are required to do if you intend to distribute or publish a fork.  These requirements come from the [Additional Terms](https://github.com/slskd/slskd/blob/master/LICENSE) section of the LICENSE, which supplements the AGPL 3.0 and is binding on all forks.

The requirements exist for two reasons, and both are about the people who use the software.

The first is to make sure users always know they are using software licensed under the AGPL.  That matters because the AGPL gives users meaningful rights: the right to know that the source code exists, the right to access it, and the right to understand what they are running.  Those rights only mean something if users are actually informed of them.  Requiring that the full LICENSE be included with every distribution, and that license notices be preserved everywhere they appear, ensures that no user ever ends up with a copy of this software that hides or obscures the terms under which it was released.

The second is to make sure users understand who made the software they are using.  They should be able to tell where it came from, who maintains it, what has been changed and by whom, and whether it is the original project or a fork.  A user who installs a fork deserves to know it is a fork.  The requirements around naming, branding, source file headers, and identification notices all serve this goal.  They are not intended to discourage forking — they are intended to make sure that anyone who uses a fork has an accurate picture of what they have.

Read the LICENSE in full.  This document is a plain-language guide, not a substitute for it.

## Identity and Branding

Your fork is a different project; make sure it looks and behaves like one.

* Choose a name that is not likely to be confused with "slskd".  Names that incorporate "slskd" as a recognizable component, sound similar, or imply maintenance or endorsement by this project are not permitted.
* Remove or replace all user-facing references to "slskd", including the project name, logos, ASCII art, terminal startup output, documentation, support URLs, and any website or domain through which the fork is distributed.
* Display a clear and prominent notice in the user interface and terminal startup output stating that the work is a modified version and is not the original program.  The original terminal branding must be removed, replaced, or made visually distinct; verbatim reproduction of it is prohibited.
* Include the following notice in your README or equivalent documentation, or in a file named NOTICE in the root of the project if no README exists:

  > "This is a modified version of slskd.  It is not maintained by, endorsed by, or affiliated with the slskd project or its author(s)."

* Do not claim or imply that your fork is maintained, endorsed, or affiliated with the original author(s) without their express written authorization.

Internal source code references such as class names, namespaces, configuration keys, and internal filenames do not need to be renamed, as long as they are not visible to end users in normal operation.

## Soulseek Version Identifier

Your fork must transmit a unique client version identifier when connecting to the Soulseek network.  Do not use an identifier already associated with slskd or any other known Soulseek client.  If you are notified that your chosen identifier conflicts with an existing client, you must change it promptly and stop distributing the fork with the conflicting identifier.

Change the value assigned by the `NetworkMinorVersion` property accordingly:

```c#
        /// <summary>
        ///     Gets the minor version to identify slskd on the network.
        /// </summary>
        /// <remarks>
        ///     NOTICE: If you have forked slskd, change this number to something else.
        /// </remarks>
        public static int NetworkMinorVersion { get; } = 760;
```

## Source File Headers

* For each source file you modify, add a copyright notice and a brief description of the nature of your changes to the file header, below all existing copyright notices and above the license notice.  For example:

  ```
  Copyright (c) <year> <your name>
  Modified: <brief description of changes>
  ```

* Do not remove or modify any existing copyright notices or license notices in source files.
* For newly created source files, use a single copyright notice with your name and the year.  The remainder of the file header, including the license notice and reference to the Additional Terms, must match the format used in the existing source files.

## Preserving the License

* Do not remove, obscure, or modify any attribution notices or legal notices displayed in the user interface, including terminal startup output and web-based dashboards.
* Any README, NOTICE, or equivalent documentation included with your fork must preserve all existing license notices and attribution statements without modification.
* Any distribution of the fork in binary or executable form, including archive files and installer packages, must include the complete and unmodified LICENSE file in the root directory.

## Package Registries and Container Images

* Do not publish your fork under the name "slskd" or any confusingly similar name in any package registry, container registry, or software distribution platform.
* When publishing through a package registry, identify the work as licensed under the GNU Affero General Public License v3.0 ("AGPLv3"), include a link to the source code, and include a link to the full LICENSE.
* When distributing a container image, include the following OCI image annotations:
  * `org.opencontainers.image.licenses` set to `AGPL-3.0-only`
  * `org.opencontainers.image.source` set to the URL of the source code repository for the version in the image

## Permitted Uses of "slskd"

The name "slskd" may appear in your fork where necessary for:

* Verbatim reproduction of the LICENSE
* Preservation of original copyright headers in source files
* The required identification notices described above
* Purely descriptive references to the software's origin, such as "based on slskd" or "compatible with slskd", provided these do not appear as primary branding elements or in the project name itself
