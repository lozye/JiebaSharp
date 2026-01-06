@echo off
if exist "bin\.pack\*.nupkg"  del "bin\.pack\*.nupkg"

dotnet pack "JiebaSharp\JiebaSharp.csproj" ^
-p:Nuget=true ^
--configuration Release ^
--output "bin\.pack"


echo ============== pack completed ==============

pause