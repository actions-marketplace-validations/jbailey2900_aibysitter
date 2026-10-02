## Commands
- Build: `dotnet build <App>.slnx -warnaserror`
- Test: `dotnet test <App>.slnx`
- Run: `dotnet run --project src/<App>.Web`
- New migration: `dotnet ef migrations add <Name> --project src/<App>.Data --startup-project src/<App>.Web`
