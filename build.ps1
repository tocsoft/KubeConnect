dotnet publish ./KubeConnect/KubeConnect.csproj --runtime win10-x64 -o publish/win -c Release --framework net5.0
dotnet pack ./KubeConnect/KubeConnect.csproj -o publish/tool -c Release
