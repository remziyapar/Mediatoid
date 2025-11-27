# Mediatoid Samples

This folder contains small, focused sample applications that demonstrate
how to use Mediatoid.

- `BasicUsage` – minimal Send/Publish/Stream usage with in-process handlers.
- `BehaviorsDemo` – shows how to enable built-in behaviors (Logging, Validation).
- `SourceGenDemo` – demonstrates using the Mediatoid.SourceGen package with
  `MediatoidRootAttribute` to move handler discovery to build time.

Each sample is a standalone .NET 8 console application. You can run them with:

```bash
dotnet run --project samples/BasicUsage/BasicUsage.csproj
``` 

(and similarly for the other sample projects).
