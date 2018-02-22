set objArgs = WScript.Arguments
If objArgs.Count = 0 then
    WScript.echo "Missing argument."
    WScript.Quit
End if
set o_installer = CreateObject("WindowsInstaller.Installer")
set o_database = o_Installer.OpenDatabase(objArgs(0), 1)
set o_MSIView = o_DataBase.OpenView("INSERT INTO Property (Property, Value) Values( 'REINSTALLMODE', 'amus')")
o_MSIView.Execute
set o_MSIView = o_DataBase.OpenView("UPDATE InstallExecuteSequence SET Sequence=1525 WHERE Action='RemoveExistingProducts'")
o_MSIView.Execute
o_DataBase.Commit