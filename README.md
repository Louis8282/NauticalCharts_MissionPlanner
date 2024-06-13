# NauticalCharts_MissionPlanner

This plugin allows you to overlay nautical charts onto Mission Planner.

![Alt text](/images/Mission_Planner_sat.png "S-57 Nautical charts overlayed on satellite imagery")

#**Usage**

Place ENCs_for_Mission_Planner_handled_layers.dll in the plugin folder of Mission Planner located in C:\Program Files (x86)\Mission Planner\plugins. Place the folders icons, iconsV2 and ENCs in the plugin folder.

Add any S-57 nautical charts you would like to use in the ENC folder. These should be .000 files, you can obtain them from various sources such as 
https://charts.noaa.gov/InteractiveCatalog/nrnc.s

html https://ienccloud.us/ienc/web/main/ienc_002.htm (US)

https://www.elwis.de/DE/dynamisch/IENC/ (Germany)

https://diffusion.shom.fr/cartes-marines-s57.html (France)

Open Mission Planner, right click on the map and chose 'Read S57 layers', navigate to the plugins\ENC folder. Mission Planner will then convert the S57 files to geojson files that the plugin uses. Wait until it finishes (you can enable the console to see the progress but it shouldnt take too long). Restart Mission Planner and your nautical charts should be there! You can get rid of any base map by chosing 'custom' in the 'change current map type' menu in the plan tab.

You can click on any area or marker on the map to get details of what the object is. This is in S-57 language (see https://desktop.arcgis.com/en/arcmap/latest/extensions/maritime-charting/s-57-object-finder.htm)


![Alt text](/images/Object_query.png "S-57 Nautical charts overlayed on satellite imagery")






 
