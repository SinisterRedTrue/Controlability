# Contributing

Controlability is at the scaffold stage. Open an issue before starting a substantial feature to discuss its scope.

Use Windows and the .NET 10 SDK. Follow the commands in README.md and run the Release build and tests before submitting a pull request. Describe the behavior changed and the verification performed. For hardware changes, include the controller model, connection type, and Windows version.

Keep Core independent of Windows APIs. Place device access in Input, Output, or Platform, orchestration in Engine, and presentation in App. Follow .editorconfig and add meaningful tests for new behavior.

Do not commit credentials, private planning notes, generated output, or third-party binaries. Use code and dependencies with licenses compatible with this MIT-licensed project and preserve required notices.

Keep pull requests focused and document limitations. The existing placeholder tests only establish that the test projects run.
