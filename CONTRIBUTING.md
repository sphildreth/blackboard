# Contributing to Blackboard

Thank you for your interest in contributing to Blackboard! This document provides guidelines and information for contributors to help maintain code quality and project consistency.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)
- [Documentation](#documentation)
- [License](#license)

## Code of Conduct

This project adheres to a code of conduct that we expect all contributors to follow. Please be respectful, inclusive, and constructive in all interactions.

## Getting Started

### Prerequisites

- **.NET 8 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Git** - For version control
- **IDE/Editor** - Visual Studio, VS Code, or JetBrains Rider recommended
- **Linux/Windows/macOS** - Cross-platform support

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/blackboard.git
   cd blackboard
   ```
3. Add the upstream remote:
   ```bash
   git remote add upstream https://github.com/ORIGINAL_OWNER/blackboard.git
   ```

## Development Setup

### Building the Project

1. Restore dependencies:
   ```bash
   dotnet restore
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run tests:
   ```bash
   dotnet test
   ```

4. Run the application:
   ```bash
   dotnet run --project src/Blackboard/Blackboard.csproj
   ```

### Configuration

The application uses YAML configuration with hot-reload support. Copy `blackboard.yml` to your working directory and modify as needed for development.

## Project Structure

```
src/
├── Blackboard/           # Main console application
├── Blackboard.Core/      # Core business logic and services
└── Blackboard.Data/      # Data access layer and database management

tests/
└── Blackboard.Core.Tests/ # Unit and integration tests

docs/                     # Documentation
├── PRD.md               # Product Requirements Document
├── DOOR_SYSTEM_GUIDE.md # Door system documentation
└── TASKS.md             # Project tasks and roadmap
```

### Key Components

- **Telnet Server** - Custom telnet protocol implementation with ANSI/VT100 support
- **Terminal GUI** - Administration interface using Terminal.Gui
- **User Management** - Authentication, profiles, and access control
- **Messaging System** - Private messaging and public message boards
- **Door System** - DOS BBS door game support via DOSBox
- **Database Layer** - SQLite with Entity Framework Core

## Coding Standards

### C# Style Guidelines

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise (prefer < 50 lines)
- Use dependency injection for service dependencies

### Naming Conventions

- **Classes**: PascalCase (`UserService`, `MessageBoard`)
- **Methods**: PascalCase (`GetUser`, `SendMessage`)
- **Properties**: PascalCase (`UserName`, `IsOnline`)
- **Fields**: camelCase with underscore prefix (`_userService`, `_logger`)
- **Constants**: PascalCase (`MaxUserNameLength`)
- **Namespaces**: `Blackboard.{Component}.{Feature}`

### Code Organization

- One class per file
- Organize using statements alphabetically
- Place private fields at the top of classes
- Group related functionality together
- Use regions sparingly, prefer small classes

### Example Code Style

```csharp
using System;
using System.Threading.Tasks;
using Blackboard.Core.Models;
using Microsoft.Extensions.Logging;

namespace Blackboard.Core.Services
{
    /// <summary>
    /// Manages user authentication and session handling.
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IUserRepository _userRepository;

        public AuthenticationService(
            ILogger<AuthenticationService> logger,
            IUserRepository userRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        /// <summary>
        /// Authenticates a user with the provided credentials.
        /// </summary>
        /// <param name="username">The username to authenticate.</param>
        /// <param name="password">The password to verify.</param>
        /// <returns>The authenticated user, or null if authentication fails.</returns>
        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            // Implementation here...
        }
    }
}
```

## Testing Guidelines

### Test Organization

- Write unit tests for all public methods
- Use integration tests for database operations
- Place tests in the `tests/` directory
- Mirror the source structure in test projects

### Testing Frameworks

- **xUnit** - Primary testing framework
- **Moq** - Mocking framework for dependencies
- **FluentAssertions** - For readable assertions

### Test Naming

Use descriptive test method names that explain the scenario:

```csharp
[Fact]
public async Task AuthenticateAsync_WithValidCredentials_ReturnsUser()
{
    // Arrange
    var username = "testuser";
    var password = "validpassword";
    
    // Act
    var result = await _authService.AuthenticateAsync(username, password);
    
    // Assert
    result.Should().NotBeNull();
    result.Username.Should().Be(username);
}
```

### Test Requirements

- All new features must include tests
- Maintain > 80% code coverage
- Tests should be fast and reliable
- Mock external dependencies
- Use realistic test data

## Pull Request Process

### Before Submitting

1. **Sync with upstream**:
   ```bash
   git fetch upstream
   git checkout main
   git merge upstream/main
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes** following the coding standards

4. **Write/update tests** for your changes

5. **Run the full test suite**:
   ```bash
   dotnet test
   ```

6. **Update documentation** if needed

### Pull Request Guidelines

- **Title**: Use a clear, descriptive title
- **Description**: Explain what the PR does and why
- **Link Issues**: Reference any related issues
- **Breaking Changes**: Clearly document any breaking changes
- **Screenshots**: Include screenshots for UI changes

### PR Template

```markdown
## Description
Brief description of the changes made.

## Type of Change
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that causes existing functionality to change)
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing completed

## Checklist
- [ ] Code follows the style guidelines
- [ ] Self-review completed
- [ ] Code is commented, particularly in hard-to-understand areas
- [ ] Documentation updated
- [ ] No new warnings introduced
```

### Review Process

1. All PRs require at least one review
2. Address all review feedback
3. Ensure CI/CD passes
4. Squash commits if requested
5. Merge only after approval

## Issue Reporting

### Bug Reports

When reporting bugs, please include:

- **Environment**: OS, .NET version, terminal type
- **Steps to Reproduce**: Clear, numbered steps
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Screenshots/Logs**: If applicable
- **Configuration**: Relevant config settings

### Feature Requests

For new features, please provide:

- **Use Case**: Why is this needed?
- **Proposed Solution**: How should it work?
- **Alternatives**: Other approaches considered
- **Implementation**: Technical considerations

### Issue Labels

- `bug` - Something isn't working
- `enhancement` - New feature or request
- `documentation` - Improvements to documentation
- `good first issue` - Good for newcomers
- `help wanted` - Extra attention needed
- `priority:high` - Critical issues
- `priority:low` - Nice to have

## Documentation

### Types of Documentation

- **Code Comments**: Explain complex logic
- **XML Documentation**: Public API documentation
- **README Files**: Project overviews and setup
- **Technical Docs**: Architecture and design decisions
- **User Guides**: End-user documentation

### Documentation Standards

- Write clear, concise explanations
- Use proper grammar and spelling
- Include code examples where helpful
- Keep documentation up-to-date with code changes
- Use Markdown formatting consistently

### API Documentation

Document all public classes, methods, and properties:

```csharp
/// <summary>
/// Represents a user session in the BBS system.
/// </summary>
public class UserSession
{
    /// <summary>
    /// Gets or sets the unique session identifier.
    /// </summary>
    public string SessionId { get; set; }
    
    /// <summary>
    /// Gets or sets the user associated with this session.
    /// </summary>
    public User User { get; set; }
    
    /// <summary>
    /// Terminates the user session and performs cleanup.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task TerminateAsync()
    {
        // Implementation...
    }
}
```

## Performance Considerations

- Profile code for performance bottlenecks
- Use async/await for I/O operations
- Minimize memory allocations in hot paths
- Consider connection pooling for database access
- Monitor resource usage in the terminal interface

## Security Guidelines

- Validate all user inputs
- Use parameterized queries for database access
- Implement proper authentication and authorization
- Store passwords using BCrypt hashing
- Log security-relevant events
- Follow OWASP guidelines for web security

## Getting Help

- **Documentation**: Check the `docs/` directory
- **Issues**: Search existing issues before creating new ones
- **Discussions**: Use GitHub Discussions for questions
- **Code Review**: Ask for help in PR comments

## Recognition

Contributors will be recognized in the project documentation. Significant contributions may be highlighted in release notes.

## License

By contributing to Blackboard, you agree that your contributions will be licensed under the [MIT License](LICENSE).

---

Thank you for contributing to Blackboard! Your efforts help make this project better for everyone. 🚀
