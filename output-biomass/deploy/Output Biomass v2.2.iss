#define PackageName      "Biomass Ouput"
#define PackageNameLong  "Biomass Output Extension"
#define Version          "2.2"
#define ReleaseType      "official"
#define ReleaseNumber    "2"

#define CoreVersion      "6"
#define CoreReleaseAbbr  ""

#include "package (Setup section) v6.0.iss"
#define ExtDir "C:\Program Files\LANDIS-II\v6\bin\extensions"
#define AppDir "C:\Program Files\LANDIS-II\v6"

[Files]
; Cohort Libraries
Source: ..\src\bin\Debug\Landis.Library.AgeOnlyCohorts.dll; DestDir: {#ExtDir}; Flags: replacesameversion
Source: ..\src\bin\Debug\Landis.Library.Biomass-v1.dll; DestDir: {#ExtDir}; Flags: replacesameversion
Source: ..\src\bin\Debug\Landis.Library.BiomassCohorts-v2.dll; DestDir: {#ExtDir}; Flags: replacesameversion
Source: ..\src\bin\Debug\Landis.Library.Cohorts.dll; DestDir: {#ExtDir}; Flags: replacesameversion
Source: ..\src\bin\Debug\Landis.Library.Metadata.dll; DestDir: {#ExtDir}; Flags:replacesameversion

; Biomass Output v1.2 plug-in
Source: ..\src\bin\debug\Landis.Extension.Output.Biomass.dll; DestDir: {#ExtDir}

; Docs, etc.
;; Source: docs\LANDIS-II Biomass Output v2.2 User Guide.pdf; DestDir: {#AppDir}\docs
Source: examples\*.txt; DestDir: {#AppDir}\examples\output-biomass
Source: examples\*.bat; DestDir: {#AppDir}\examples\output-biomass
Source: examples\ecoregions.gis; DestDir: {#AppDir}\examples\output-biomass
Source: examples\initial-communities.gis; DestDir: {#AppDir}\examples\output-biomass


#define BiomassOutput "Output Biomass 2.2.txt"
Source: {#BiomassOutput}; DestDir: {#LandisPlugInDir}

[Run]
;; Run plug-in admin tool to add an entry for the plug-in
#define PlugInAdminTool  CoreBinDir + "\Landis.PlugIns.Admin.exe"

Filename: {#PlugInAdminTool}; Parameters: "remove ""Output Biomass"" "; WorkingDir: {#LandisPlugInDir}
Filename: {#PlugInAdminTool}; Parameters: "add ""{#BiomassOutput}"" "; WorkingDir: {#LandisPlugInDir}

[UninstallRun]

[Code]
#include "package (Code section) v3.iss"

//-----------------------------------------------------------------------------

function InitializeSetup_FirstPhase(): Boolean;
begin
  Result := True
end;
