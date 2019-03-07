
$version = Read-Host 'Please enter a version to update projects: '

$arr = Get-ChildItem AssemblyInfo.cs -Recurse

foreach($file in $arr) {
// TODO: THIS IS UNFINISHED!!!
	$regex = ''
}