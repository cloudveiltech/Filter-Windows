param (
    [string]$version
)

$arr = Get-ChildItem AssemblyInfo.cs -Recurse

foreach($file in $arr) {
	((Get-Content -path $file -Raw) -replace '\[assembly:\s(AssemblyVersion|AssemblyFileVersion)\("(.*)"\)\]', ('[assembly: $1("' + $version + '")]')) | Set-Content -Path $file
}