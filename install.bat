dotnet restore
dotnet publish -c Debug -r win10-x64
Sc create @winux binPath="dotnet %CD%/bin/debug/netcoreapp2.1/win10-x64/winux.exe" DisplayName=@winux type=own start=auto