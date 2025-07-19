# Git Commit Message Conventions

## Overview

This project follows the [Conventional Commits](https://www.conventionalcommits.org/) specification for commit messages. This ensures consistent, readable commit history and enables automatic generation of changelogs.

## Commit Message Format

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

## Types

| Type | Description | Example |
|------|-------------|---------|
| `feat` | A new feature | `feat(core): add semantic similarity matching` |
| `fix` | A bug fix | `fix(gui): resolve memory leak in MainWindow` |
| `docs` | Documentation only changes | `docs: update README with API examples` |
| `style` | Code style changes (formatting, etc.) | `style: format code according to guidelines` |
| `refactor` | Code refactoring | `refactor(api): improve error handling structure` |
| `perf` | Performance improvements | `perf(core): optimize embedding vector calculations` |
| `test` | Adding or updating tests | `test: add unit tests for ContentMatchingService` |
| `chore` | Build process or tooling changes | `chore: update NuGet packages to latest versions` |
| `ci` | CI/CD changes | `ci: add GitHub Actions workflow` |
| `build` | Build system changes | `build: update .NET SDK version` |
| `revert` | Revert previous commit | `revert: revert "feat: add new feature"` |

## Scopes

Scopes are optional but recommended for this project:

| Scope | Description | Example |
|-------|-------------|---------|
| `core` | Core library changes | `feat(core): add new matching algorithm` |
| `gui` | GUI application changes | `fix(gui): fix button click handler` |
| `api` | API service changes | `refactor(api): improve Gemini service error handling` |
| `config` | Configuration changes | `feat(config): add new similarity threshold setting` |
| `test` | Test-related changes | `test: add integration tests for CSV processing` |
| `docs` | Documentation changes | `docs: add API documentation` |
| `build` | Build system changes | `build: update MSBuild configuration` |

## Breaking Changes

Use `!` after the type/scope to indicate breaking changes:

```
feat(core)!: change similarity threshold default value
```

Include `BREAKING CHANGE:` in the footer for detailed explanation:

```
feat(core)!: change similarity threshold default value

BREAKING CHANGE: The default similarity threshold has been changed from 0.35 to 0.4.
This may affect existing matching results.
```

## Examples

### Simple Feature
```
feat(core): add batch processing for large datasets
```

### Feature with Body
```
feat(core): implement batch processing for large datasets

This change introduces batch processing capabilities to handle large CSV files
more efficiently by processing content in configurable batch sizes.

- Add BatchProcessor class with configurable batch size
- Implement progress reporting for batch operations
- Add memory optimization for large file processing
```

### Bug Fix with Issue Reference
```
fix(gui): resolve memory leak in MainWindow

The MainWindow was not properly disposing of event handlers, causing
memory leaks during long-running operations.

- Dispose event handlers in Window_Closed event
- Add proper cleanup in ViewModel disposal
- Implement IDisposable pattern for MainWindow

Fixes #123
```

### Breaking Change
```
feat(core)!: change default similarity threshold

BREAKING CHANGE: The default similarity threshold has been increased from 0.35
to 0.4 to improve match quality. This may result in fewer matches but
higher quality alignments.

Closes #456
```

## Best Practices

1. **Use imperative mood**: "add" not "added" or "adds"
2. **Keep first line under 72 characters**
3. **Use body for complex changes**
4. **Reference issues when applicable**
5. **Be specific and descriptive**
6. **Use consistent terminology**

## Tools

### Template Setup
The project includes a `.gitmessage` template. To use it:

```bash
git config commit.template .gitmessage
```

### Commit Message Validation
Consider using commit message linters like:
- [commitlint](https://commitlint.js.org/)
- [husky](https://typicode.github.io/husky/)

### Changelog Generation
With conventional commits, you can automatically generate changelogs using:
- [Conventional Changelog](https://github.com/conventional-changelog/conventional-changelog)
- [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/automatically-generated-release-notes)

## Migration from Current Commits

The current commit history uses Vietnamese descriptions. For future commits, use English following these conventions. Consider using `git rebase` to update recent commits if needed. 