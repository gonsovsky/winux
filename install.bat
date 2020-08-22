dotnet restore
dotnet publish -r win-x64 -c debug
Sc create winux binPath="%CD%/bin/debug/netcoreapp2.1/win-x64/publish/winux.exe" DisplayName=winux type=own start=auto