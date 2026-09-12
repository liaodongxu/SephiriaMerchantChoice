param([Parameter(Mandatory=$true)][string]$GameRoot)
$ErrorActionPreference = 'Stop'
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$managed = Join-Path $GameRoot 'Sephiria_Data\Managed'
$core = Join-Path $GameRoot 'BepInEx\core'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Requires Windows .NET Framework 4 C# compiler.' }
$build = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$output = Join-Path $build 'SephiriaMerchantChoice.dll'
$compilerArgs = @('/nologo','/noconfig','/target:library','/optimize+','/nostdlib+','/langversion:5','/define:MERCHANT_CHOICE',('/out:' + $output))
foreach ($name in @('mscorlib','System','System.Core','netstandard','Assembly-CSharp','Mirror','UnityEngine','UnityEngine.CoreModule','UnityEngine.UIModule','UnityEngine.UI','Unity.TextMeshPro')) {
    $compilerArgs += '/reference:' + (Join-Path $managed ($name + '.dll'))
}
foreach ($name in @('BepInEx.Core','BepInEx.Unity.Common','BepInEx.Unity.Mono','0Harmony')) {
    $compilerArgs += '/reference:' + (Join-Path $core ($name + '.dll'))
}
foreach ($name in @('Plugin.cs','ShopCatalog.cs','FavoriteSupport.cs')) { $compilerArgs += Join-Path $PSScriptRoot ('src\' + $name) }
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Add-Type -LiteralPath (Join-Path $core 'Mono.Cecil.dll')
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$resolver.AddSearchDirectory($core)
$parameters = New-Object Mono.Cecil.ReaderParameters
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($output, $parameters)
try {
    foreach ($reference in $assembly.MainModule.GetTypeReferences()) { [void]$reference.Resolve() }
    foreach ($reference in $assembly.MainModule.GetMemberReferences()) { [void]$reference.Resolve() }
    if (@($assembly.MainModule.AssemblyReferences | Where-Object {$_.Name -like 'Sephiria*'}).Count) { throw 'Unexpected dependency on another mod' }
    Write-Output 'PASS: compiled and validated against installed game; no other mod required.'
} finally { $assembly.Dispose(); $resolver.Dispose() }
