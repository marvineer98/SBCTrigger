:: dotnet publish files for release
dotnet publish -c Release -r linux-arm -p:Platform=linux-arm
if ERRORLEVEL 1 exit
dotnet publish -c Release -r linux-arm64 -p:Platform=linux-arm64
if ERRORLEVEL 1 exit

:: make final zip for plugin installation
:: -a auto compress
:: -cf create archive-name [filenames...]
:: see https://ss64.com/nt/tar.html for further docs
CALL tar.exe -a -cf SBCTrigger.zip dsf sd plugin.json
