dotnet restore
dotnet publish -c Debug -r win10-x64
Sc create @winux binPath="%CD%/bin/debug/netcoreapp2.1/win10-x64/publish/winux.exe" DisplayName=@winux type=own start=auto