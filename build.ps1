dotnet publish ./KubeConnect/KubeConnect.csproj -r win10-x64 -o publish/win -c Release -p:PublishSingleFile=true -f netcoreapp3.1
dotnet pack ./KubeConnect/KubeConnect.csproj -o publish/tool -c Release
