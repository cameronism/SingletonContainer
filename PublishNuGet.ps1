[CmdletBinding()]
Param(
	[Parameter(Mandatory=$True,Position=1)]
	[string]$version
)

pushd "$PSScriptRoot\SingletonContainer"

. "C:\Users\cjordan\Downloads\NuGet.exe" pack SingletonContainer.csproj -Build -Prop Configuration=Release -Version $version
. "C:\Users\cjordan\Downloads\NuGet.exe" push "SingletonContainer.$version.nupkg"

popd
