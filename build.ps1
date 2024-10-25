dotnet publish ./KubeConnect/KubeConnect.csproj --runtime win-x64 -o publish/win -c Release --framework net8.0  --self-contained
dotnet pack ./KubeConnect/KubeConnect.csproj -o publish/tool -c Release
