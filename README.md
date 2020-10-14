# DockerComposer

Wrapper for bringing up docker compose which uses the [FluentDocker](https://github.com/mariotoffia/FluentDocker) project, it simplifies interacting with docker compose from within integration tests. 

## Getting Started

Make sure docker is up and running.

To spin up an instance of DockerCompose with the default values do:

```csharp
using var _ = DockerComposer.Up();
```

All the containers will be killed once the using statement completes but can be kept alive by checking for certain environment variables or using a custom check.

Full example can be viewed [here](./DockerComposer.Integration.Tests/IntegrationTest.cs)

Versions:
1.0.0.3
