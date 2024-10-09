# Contributing to Cute

We welcome pull requests for new features, application enhancements, bug fixes and documentation. We strongly encourage you to take some time to familiarise yourself with ***cute*** and its stated objectives. This will ultimately assist you in putting your proposed contribution in the appropriate context.

## Choosing an issue

All contributions should address an [open issue](https://github.com/andresharpe/cute/issues) in the [cute repo](https://github.com/andresharpe/cute).

### Bugs versus enhancements

Issues are typically labeled with [Enhancement](https://github.com/andresharpe/cute/issues?q=is%3Aopen+is%3Aissue+label%3AEnhancement) or [Bug](https://github.com/andresharpe/cute/issues?q=is%3Aopen+is%3Aissue+label%3ABug).

- Bugs are places where ***cute*** is doing something that it was 
t designed to.
- Enhancements are suggestions to improve ***cute*** by changing existing or adding new functionality.

### Create an issue

If there is no existing issue tracking the change you want to make, then [create one](https://github.com/andresharpe/cute/issues/new/choose)! PRs that don't get merged are often those that are created without any prior discussion with the team. An issue is the best place to have that discussion, ideally before the PR is submitted.

### Fixing typos

An issue is not required for simple non-code changes like fixing a typo in documentation. In fact, these changes can often be submitted as a PR directly from the browser, avoiding the need to fork and clone.

## Workflow

The typical workflow for contributing to ***cute*** is outlined below. This is not a set-in-stone process, but rather guidelines to help ensure a quality PR that we can merge efficiently.

1. Start by [setting up your development environment](./docs/README_github_full.md#Prerequisites) so that you can build and test the code. Don't forget to [create a fork](https://github.com/andresharpe/cute#creating-a-project) for your work.
2. Make sure all tests are passing. (This is typically done by running `test` at a command prompt.)
3. Choose an issue (see above), understand it, and **comment on the issue** indicating what you intend to do to fix it. **This communication with the team is very important and often helps avoid throwing away lots of work caused by taking the wrong approach.**
4. Create and check out a [branch](https://docs.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-and-deleting-branches-within-your-repository) in your local clone. You will use this branch to prepare your PR.
5. Make appropriate code and test changes. Follow the patterns and code style that you see in the existing code. Make sure to add tests that fail without the change and then pass with the change.
6. Consider other scenarios where your change may have an impact and add more testing. We always prefer having too many tests to having not enough of them.
7. When you are done with changes, make sure _all_ existing tests are still passing. (Again, typically by running `test` at a command prompt.)
8. Commit changes to your branch and push the branch to your GitHub fork.
9. Go to the main [cute repo](https://github.com/andresharpe/cute/pulls) and you should see a yellow box suggesting you create a PR from your fork. Do this, or [create the PR by some other mechanism](https://docs.github.com/en/github/collaborating-with-issues-and-pull-requests/about-pull-requests).
10. Wait for the feedback from the team and for the continuous integration (C.I.) checks to pass.
11. Add and push new commits to your branch to address any issues.

The PR will be merged by a member of the ***cute*** team once the C.I. checks have passed and the code has been approved.