# NauticalCharts_MissionPlanner

This plugin allows you to overlay nautical charts onto Mission Planner.

![Alt text](/images/Mission_Planner_sat.png "S-57 Nautical charts overlayed on satellite imagery")

**Usage**

Place ENCs_for_Mission_Planner_handled_layers.dll in the plugin folder of Mission Planner located in C:\Program Files (x86)\Mission Planner\plugins. Place the folders icons, iconsV2 and ENCs in the plugin folder.

Add any S-57 nautical charts you would like to use in the ENC folder. These should be .000 files, you can obtain them from various sources such as 
https://charts.noaa.gov/InteractiveCatalog/nrnc.s

html https://ienccloud.us/ienc/web/main/ienc_002.htm (US)

https://www.elwis.de/DE/dynamisch/IENC/ (Germany)

https://diffusion.shom.fr/cartes-marines-s57.html (France)

Open Mission Planner, right click on the map and chose 'Read S57 layers', navigate to the plugins\ENC folder. Mission Planner will then convert the S57 files to geojson files that the plugin uses. Wait until it finishes (you can enable the console to see the progress but it shouldn't take too long). Restart Mission Planner and your nautical charts should be there! You can get rid of any base map by chosing 'custom' in the 'change current map type' menu in the plan tab.

You can click on any area or marker on the map to get details of what the object is. This is in S-57 language (see https://desktop.arcgis.com/en/arcmap/latest/extensions/maritime-charting/s-57-object-finder.htm)


![Alt text](/images/Object_query.png "S-57 Nautical charts overlayed on satellite imagery")

**Unhandled layers**

This plugin doesn't yet display all objects. Pull requests are welcome. In the mean time you can still display all point objects, lines and polygons in an ENC but the unhandled items will appear in red. To do this, instead of using ENCs_for_Mission_Planner_handled_layers.dll, you use ENCs_for_Mission_Planner_all_layers.dll
Charts will then look like so:

![Alt text](/images/Mission_Planner_all_layers.png "")

The spanner symbols indicate unhandled objects. You can find out what they are by using the middle mouse button.


![Alt text](/images/Mission_Planner_query.png "")


**Editing the code**

Open ENCs_for_Mission_Planner.csproj in Visual Studio 2022. When adding dependencies (eg GDAL and GeoJSON.NET), make sure they match those used in Mission Planner, which you can find here: https://github.com/ArduPilot/MissionPlanner/blob/master/MissionPlannerCore.csproj



The geojson files all have the names of the layer in them. For example, 9H7EL612_BOYLAT.js has all of the lateral buoys (BOYLAT) of the 9H7EL612 ENC cell. This section:
```csharp
  else if (filePath.Contains("BOYLAT"))
 {
     try
     {
         Console.WriteLine("Starting to process buoy features.");
        ProcessBOYLATFeatures(featureCollection, BOYLAToverlay, "BOYLAT");
         Console.WriteLine("Finished processing buoy features.");
     }
```csharp

Deals with matching each buoy with the correct feature according to its attributes. In order to add another layer type, you would create a new function called Process*new_feature*Features. The details of how to display everything is in the S-52 standard (https://iho.int/en/enc-portrayal) which is rather extensive. The files 'icons' and 'iconsV2' contain the symbols to be used (they are from the OpenCPN project). You can use OpenCPN to check that the plugin is displaying things correctly (https://opencpn.org/).





 
