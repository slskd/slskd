# Contributing

All contributions must first have been discussed within an Issue or Discussion.

Pull Requests opened without an accompanying Issue are considered to be unsolicited, and unsolicited Pull Requests may be rejected without review or converted into an Issue.

## Copyright

By submitting a contribution to this project, you agree to the following:

* You have the legal right to make this contribution and to assign its copyright.
* You irrevocably assign all copyright and related rights in your contribution to the project maintainer(s).
* You grant the project maintainer(s) the right to use, modify, distribute, and relicense your contribution under any license of their choosing.

This application is currently released under the [AGPL 3.0](https://github.com/slskd/slskd/blob/master/LICENSE) license.

## Contribution Workflow

1. Comment on the Issue you'll be working on so others know you're working on it.  You may also ask a maintainer to assign it to you.
1. Fork the repository, then clone it and run `git checkout master` to ensure you are on the master branch.
1. Create a new branch for your change with `git checkout -b <your-branch-name>` be descriptive, but terse.
1. Make your changes.  When finished, push your branch with `git push origin --set-upstream <your-branch-name>`.
1. Create a Pull Request in the source repository to merge `<your-branch-name>` into `master`.
1. A maintainer will review your Pull Request and may make comments, ask questions, or request changes.  When all
   feedback has been addressed the Pull Request will be approved, and after all checks have passed it will be merged by
   a maintainer, or you may merge it yourself if you have the necessary access.
1. Delete your branch, unless you plan to submit additional pull request from it.

Note that we require that all branches are up to date with target branch prior to merging.  If you see a message about this
on your pull request, use `git fetch` to retrieve the latest changes,  `git merge origin/master` to merge the changes from master
into your local branch, then `git push` to update your branch.

## Environment Setup

You'll need [.NET 8.0](https://dotnet.microsoft.com/en-us/download) to build and run the back end (slskd), and you'll 
need [Nodejs](https://nodejs.org/en/) to build and debug the front end (web).

You're free to use whichever development tools you prefer.  If you don't yet have a preference, we recommend the following:

[Visual Studio Code](https://code.visualstudio.com/) for front or back end development.

[Visual Studio](https://visualstudio.microsoft.com/downloads/) for back end development.

## Debugging

### Back End

Run `./bin/watch` from the root of the repository.

### Front End

Run `./bin/watch --web` from the root of the directory.  Make sure the back end is running first.
