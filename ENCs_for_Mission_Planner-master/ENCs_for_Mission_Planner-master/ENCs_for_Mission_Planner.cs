using MissionPlanner;
using MissionPlanner.Plugin;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using MissionPlanner.Controls.PreFlight;
using MissionPlanner.Controls;
using System.Linq;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Maps;
using GMap.NET;
using GMap.NET.WindowsForms;
using System.Globalization;
using System.Drawing;
using Microsoft.Win32;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET;
using System.Drawing;
using System.Collections.Generic;
using Newtonsoft.Json;
using GMap.NET.WindowsForms;
using GMap.NET;
using MissionPlanner.Utilities.nfz;
using static MissionPlanner.Controls.myGMAP;
using GeoJSON.Net.Geometry;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using GMap.NET;
using GMap.NET.WindowsForms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using GMap.NET.WindowsForms.ToolTips;
using static MissionPlanner.Utilities.LTM;
using static MissionPlanner.Controls.MainSwitcher;
using System.Threading.Tasks;
using Core.Geometry;
using System.Drawing.Drawing2D;
using MissionPlanner.Controls.Icon;
using static System.Net.WebRequestMethods;
using MissionPlanner.ArduPilot;
using static RFD.RFD900.TSetting;
using MissionPlanner.Log;
using OSGeo.OGR;

namespace ENCs_for_Mission_Planner
{
   
    public static class GlobalResources
    {
        public static Dictionary<int, LightSECTRInfo> LIGHTS_SECTR = new Dictionary<int, LightSECTRInfo>();
        public static Dictionary<int, LightSECTRInfo> LIGHTS_SECTR_ALONE = new Dictionary<int, LightSECTRInfo>();
        public static Dictionary<int, LightSECTRInfo> LIGHTS_SECTR_SMALL = new Dictionary<int, LightSECTRInfo>();
        public static Dictionary<int, LightSECTRInfo> LIGHTS_SECTR_LARGE = new Dictionary<int, LightSECTRInfo>();
        public static Dictionary<int, LightSECTRInfo_properties> LIGHTS_SECTR_PROPERTIES = new Dictionary<int, LightSECTRInfo_properties>();
        public static Dictionary<int, LightNO_SECTR> LightNO_SECTR = new Dictionary<int, LightNO_SECTR>();
    }
    public class LightNO_SECTR
    {
        public PointLatLng Position { get; }

        public Dictionary<string, object> properties_dictionary { get; }

        public LightNO_SECTR(PointLatLng position, Dictionary<string, object> PROPERTIES_DICTIONARY)
        {
            Position = position;
            properties_dictionary = PROPERTIES_DICTIONARY;

        }
    }

    public class LightSECTRInfo
    {
        public PointLatLng Position { get; }
        public int Sector1 { get; }
        public int Sector2 { get; }

        public string Colour_arc { get; }

        public LightSECTRInfo(PointLatLng position, int SECTR1, int SECTR2, string COLOUR_ARC)
        {
            Position = position;
            Sector1 = SECTR1;
            Sector2 = SECTR2;
            Colour_arc = COLOUR_ARC;
        }
    }
    public class LightSECTRInfo_properties
    {
        public PointLatLng Position { get; }
        public int Sector1 { get; }
        public int Sector2 { get; }

        public string Colour_arc { get; }
        //var cleanedProperties = new Dictionary<string, object>();
        public Dictionary<string, object> properties_dictionary { get; }

        public LightSECTRInfo_properties(PointLatLng position, int SECTR1, int SECTR2, string COLOUR_ARC, Dictionary<string, object> PROPERTIES_DICTIONARY)
        {
            Position = position;
            Sector1 = SECTR1;
            Sector2 = SECTR2;
            Colour_arc = COLOUR_ARC;
            properties_dictionary = PROPERTIES_DICTIONARY;

        }
    }

    public static class LightProcessing
    {
        public static void ClassifyLights()
        {
          //  Console.WriteLine("classifying the lights");
            // Dictionary to keep track of points and their corresponding RCIDs
            Dictionary<PointLatLng, List<int>> pointMap = new Dictionary<PointLatLng, List<int>>();

            // Populate the pointMap with RCIDs for each point
            foreach (var item in GlobalResources.LIGHTS_SECTR)
            {
                if (!pointMap.ContainsKey(item.Value.Position))
                    pointMap[item.Value.Position] = new List<int>();

                pointMap[item.Value.Position].Add(item.Key);
            }

            // Analyze the pointMap to categorize lights
            foreach (var pointEntry in pointMap)
            {
                if (pointEntry.Value.Count == 1) // Only one light at this point
                {
                    int rcid = pointEntry.Value.First();
                    GlobalResources.LIGHTS_SECTR_ALONE.Add(rcid, GlobalResources.LIGHTS_SECTR[rcid]);
                }
                else // Multiple lights at the same point
                {
                    // We need to compare the sectors of each light at this point
                    CompareAndCategorizeLights(pointEntry.Value);
                }
            }
        }

        private static void CompareAndCategorizeLights(List<int> rcids)
        {
            // Retrieve LightSECTRInfo for each RCID and calculate modified sectors
            var sectors = rcids.Select(rcid => new
            {
                RCID = rcid,
                StartOfSector = (GlobalResources.LIGHTS_SECTR[rcid].Sector1 + 180) % 360,
                EndOfSector = (GlobalResources.LIGHTS_SECTR[rcid].Sector2 + 180) % 360
            }).ToList();

            // Now compare each light with every other light at the same point
            for (int i = 0; i < sectors.Count; i++)
            {
                bool isSmaller = true;
                for (int j = 0; j < sectors.Count; j++)
                {
                    if (i == j) continue; // Don't compare with itself

                    // Check if sectors[i] is within sectors[j]
                    if (IsWithinRange(sectors[i].StartOfSector, sectors[i].EndOfSector, sectors[j].StartOfSector, sectors[j].EndOfSector))
                    {
                        isSmaller = false;
                        break;
                    }
                }

                if (isSmaller)
                    GlobalResources.LIGHTS_SECTR_SMALL.Add(sectors[i].RCID, GlobalResources.LIGHTS_SECTR[sectors[i].RCID]);
                else
                    GlobalResources.LIGHTS_SECTR_LARGE.Add(sectors[i].RCID, GlobalResources.LIGHTS_SECTR[sectors[i].RCID]);
            }
        }
        private static bool IsWithinRange(int start, int end, int rangeStart, int rangeEnd)
        {
            // Normalize sectors to handle wrap-around at 360 degrees
            start = start % 360;
            end = end % 360;
            rangeStart = rangeStart % 360;
            rangeEnd = rangeEnd % 360;

            if (start <= end)
            {
                // The sector does not cross the 360-degree boundary
                return (rangeStart <= end && start <= rangeEnd);
            }
            else
            {
                // The sector crosses the 360-degree boundary
                return (rangeStart <= end || start <= rangeEnd);
            }
        }

    }


//var properties_dictionary = new Dictionary<string, object>();


public class IconDetails
    {
        public string IconName { get; set; }
        public string ObjectName { get; set; }
        public List<Tuple<string, string>> Properties { get; set; }

        public IconDetails(string iconName, string objectName, List<Tuple<string, string>> properties)
        {
            IconName = iconName;
            ObjectName = objectName;
            Properties = properties;
        }

        public override string ToString()
        {
            string props = Properties.Count > 0 ? string.Join(", ", Properties.ConvertAll(p => $"{p.Item1}={p.Item2}")) : "empty list";
            return $"Icon_name={IconName}, object_name={ObjectName}, [{props}]";
        }
    }
    // This could be part of a utility class or directly within one of your plugin's main classes

    public class DepthSettings
    {
        public double ShallowContour { get; set; } = 3.0;
        public double SafetyContour { get; set; } = 5.0;
        public double SafetyDepth { get; set; } = 5.0;
        public double DeepContour { get; set; } = 20.0;

        public static int[] RgbFromXyY(Tuple<double, double, double> xyY)
        {
            double x = xyY.Item1;
            double y = xyY.Item2;
            double Y = xyY.Item3;
            double z = 1.0 - x - y;

            double X = Y / y * x;
            double Z = Y / y * z;

            double R = X * 1.6117500 + Y * -0.2028050 + Z * -0.302298;
            double G = X * -0.509057 + Y * 1.4119100 + Z * 0.0660705;
            double B = X * 0.0260848 + Y * -0.0723524 + Z * 0.9620860;

            Func<double, double> ungamma = i => i <= 0.0031308 ? i * 12.92 : Math.Pow(i, 1.0 / 2.4) * 1.055 - 0.055;
            R = ungamma(R);
            G = ungamma(G);
            B = ungamma(B);

            Func<double, int> denormalize = i => (int)Math.Round(255.0 * Math.Max(0, Math.Min(1, i)));
            return new int[] { denormalize(R), denormalize(G), denormalize(B) };
        }

        // Usage
        //  Color myColor = ConvertXYLtoColor(0.26, 0.35, 35);

        public Color GetColorFromDepth(double depth)
        {
            // Console.WriteLine("Choosing a color for depth: " + depth);

            if (depth <= 0)
            {
                int R = 155;//DEPIT
                int G = 200;
                int B = 163;

                return Color.FromArgb(255, R, G, B);  //53, 103, 95;
                                                      // int[] rgb = RgbFromXyY(new Tuple<double, double, double>(0.26, 0.35, 35)); // Including Luminance value '35'

                // return Color.FromArgb(255, rgb[0], rgb[1], rgb[2]); // Assuming RGB values are within [0, 255]
            }
            if (0 <= depth && depth <= ShallowContour)
            {//DEPVS  .22 .24 45

                int R = 145;
                int G = 202;
                int B = 243;

                return Color.FromArgb(255, R, G, B);
                //    int[] rgb = RgbFromXyY(new Tuple
                //    <double, double, double>(0.21, 0.22, 45)); // Including Luminance value '35'
                //   return Color.FromArgb(255, rgb[0], rgb[1], rgb[2]); // Assuming RGB values are within [0, 255]
            }
            else if (ShallowContour < depth && depth <= SafetyContour)
            {
                int R = 145;//DEPMS  .24 .26, 55
                int G = 202;
                int B = 243;

                return Color.FromArgb(255, R, G, B);
                // int[] rgb = RgbFromXyY(new Tuple<dou
                // ble, double, double>(0.23, 0.25, 55)); // Including Luminance value '35'
                //return Color.FromArgb(255, rgb[0], rgb[1], rgb[2]); // Assuming RGB values are within [0, 255]
            }
            else if (SafetyContour < depth && depth <= DeepContour)
            {
                int R = 180;//DEPMD
                int G = 218;
                int B = 247;

                return Color.FromArgb(255, R, G, B);
                //int[] rgb = RgbFromXyY(new Tuple<double, double, doub
                //le>(0.26, 0.29, 65)); // Including Luminance value '35'
                //               return Color.FromArgb(255, rgb[0], rgb[1], rgb[2]); // Assuming RGB values are within [0, 255]
            }
            else if (depth > DeepContour)
            {

                int R = 234;//DEPDW
                int G = 255;
                int B = 243;

                return Color.FromArgb(255, R, G, B);
                //  int[] rgb = RgbFromXyY(new
                //  Tuple<double, double, double>(0.28, 0.31, 80)); // Including Luminance value '35'
                //    return Color.FromArgb(255, rgb[0], rgb[1], rgb[2]); // Assuming RGB values are within [0, 255]
            }
            return Color.Gray; // Default color if none of the conditions match
        }
    }
    public class myOverlay : GMapOverlay
    {


        public myOverlay(string id) : base(id) { }

        public void AddPolygon(double zoom, double metersperPixel, double center)
        {
         //   Console.WriteLine("zoom level is");
            //Console.WriteLine(zoom);
            List<PointLatLng> points = new List<PointLatLng>
        {
            new PointLatLng(51.5074, 0.0), // Example coordinates for London
            new PointLatLng(51.5074, -10.0),
            new PointLatLng(55.0,-10.0),
            new PointLatLng(55.0, 0.0)
        };

            GMapPolygon polygon = new GMapPolygon(points, "mypolygon")
            {
                Stroke = new Pen(Color.Red, 2),
                Fill = new SolidBrush(Color.FromArgb(50, Color.Red))
            };

            this.Polygons.Add(polygon);
             


            PointLatLng point1 = new PointLatLng(51, 0);
            int pixelLength = 100;
            int bearingDegrees = 45;
     
//            Console.WriteLine("metersPerPixel");
//            Console.WriteLine(metersperPixel);
            // Calculate the geographic distance in meters that corresponds to the pixel length
            double distanceInMeters = pixelLength * metersperPixel;
  //          Console.WriteLine("distance in meters of line");
   //         Console.WriteLine(distanceInMeters);//correct

            // Convert distance in meters to geographic coordinates
            double R = 6378137; // Earth’s radius in meters  

        // Convert latitude and longitude from degrees to radians
        double lat1Rad = (Math.PI / 180) * (point1.Lat);
            double lon1Rad = (Math.PI / 180) * (point1.Lng); 
            double bearingRad = (Math.PI / 180) * bearingDegrees;

            // Calculate the angular distance
            double angularDistance = distanceInMeters / R;


            // Calculate the latitude of the second point
            double lat2Rad = Math.Asin(Math.Sin(lat1Rad) * Math.Cos(angularDistance) +
                                       Math.Cos(lat1Rad) * Math.Sin(angularDistance) * Math.Cos(bearingRad));

            // Calculate the longitude of the second point
            double lon2Rad = lon1Rad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(angularDistance) * Math.Cos(lat1Rad),
                                                 Math.Cos(angularDistance) - Math.Sin(lat1Rad) * Math.Sin(lat2Rad));

            // Convert the final points to degrees deg=rad × 180/π
            double lat2 = (180/Math.PI)*lat2Rad;
            double lon2 = (180 / Math.PI) * lon2Rad;




            PointLatLng point2 = new PointLatLng(lat2, lon2);

            List<PointLatLng> points2 = new List<PointLatLng>();
            points2.Add(point1);
            points2.Add(point2);

            GMapRoute route = new GMapRoute(points2, "My Route");
            route.Stroke = new Pen(Color.Red, 2);
            this.Routes.Add(route);
            

        }
        //            if (Light_sector_type== "LIGHTS_SECTR_ALONE") { LightSECTRInfo light_sector_to_use = GlobalResources.LIGHTS_SECTR_ALONE; }

        public void AddSECTR_lines(double zoom, double metersperPixel, double center, string Light_sector_type) //redundqnt cqlculqtions in the loop, we could optimize this
        {  //first we need to go through all of these and find any with the same positions, then we must ensure that the one with the smaller sector gets place on top of the
            //larger one 
   //         Console.WriteLine("zoom level is");
    //        Console.WriteLine(zoom);

            Dictionary<int, LightSECTRInfo> selecteddictionary;
            if (Light_sector_type == "lights_sectr_alone")
                selecteddictionary = GlobalResources.LIGHTS_SECTR_ALONE;
            else if (Light_sector_type == "lights_sectr_small")
                selecteddictionary = GlobalResources.LIGHTS_SECTR_SMALL;
            else
                selecteddictionary = GlobalResources.LIGHTS_SECTR_LARGE; // default or fallback


            foreach (KeyValuePair<int, LightSECTRInfo> entry in selecteddictionary)//previously used LIGHTS_SECTR, now we need to write seperate cases
                                                                                                   //for getting multiple lights at a single location to overlap properly
            {
                int rcid = entry.Key;
                LightSECTRInfo lightInfo = entry.Value;
                //Console.WriteLine("inside AddSECTR_lines");
                //Console.WriteLine($"RCID: {rcid}, Position: {lightInfo.Position.Lat}, {lightInfo.Position.Lng}, Sector1: {lightInfo.Sector1}, Sector2: {lightInfo.Sector2}, Colour: {lightInfo.Colour_arc} ");
                PointLatLng point1 = lightInfo.Position;
                int pixelLength = 100;
                //Console.WriteLine("metersPerPixel");
                //Console.WriteLine(metersperPixel);
                // Calculate the geographic distance in meters that corresponds to the pixel length
                double distanceInMeters = pixelLength * metersperPixel;
  //              Console.WriteLine("distance in meters of line");
   //             Console.WriteLine(distanceInMeters);//correct

                // Convert distance in meters to geographic coordinates
                double R = 6378137; // Earth’s radius in meters  

                // Convert latitude and longitude from degrees to radians
                double lat1Rad = (Math.PI / 180) * (point1.Lat);
                double lon1Rad = (Math.PI / 180) * (point1.Lng);



                //int SecondbearingDegrees = (lightInfo.Sector2 + 180) % 360;
                //double SecondbearingRad = (Math.PI / 180) * SecondbearingDegrees;
                //// Calculate the latitude of the second point
                //double Secondlat2Rad = Math.Asin(Math.Sin(lat1Rad) * Math.Cos(angularDistance) +
                //                           Math.Cos(lat1Rad) * Math.Sin(angularDistance) * Math.Cos(SecondbearingRad));
                int bearingDegrees = (lightInfo.Sector1 + 180) % 360;
    //            Console.WriteLine($"bearingDegrees to use in the calculations, calculated from sectr1: {bearingDegrees}");
                double bearingRad = (Math.PI / 180) * bearingDegrees;
                // Calculate the angular distance
                double angularDistance = distanceInMeters / R;
                // Calculate the latitude of the second point
                double lat2Rad = Math.Asin(Math.Sin(lat1Rad) * Math.Cos(angularDistance) +
                                           Math.Cos(lat1Rad) * Math.Sin(angularDistance) * Math.Cos(bearingRad));
                // Calculate the longitude of the second point
                double lon2Rad = lon1Rad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(angularDistance) * Math.Cos(lat1Rad),
                                                     Math.Cos(angularDistance) - Math.Sin(lat1Rad) * Math.Sin(lat2Rad));
                // Convert the final points to degrees deg=rad × 180/π
                double lat2 = (180 / Math.PI) * lat2Rad;
                double lon2 = (180 / Math.PI) * lon2Rad;
                PointLatLng point2 = new PointLatLng(lat2, lon2);
                List<PointLatLng> points2 = new List<PointLatLng>();
                points2.Add(point1);
                points2.Add(point2);
                GMapRoute route = new GMapRoute(points2, "My Route");
                //Pen mydashedPen = new Pen(Color.Black, 2);
                //mydashedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                //mydashedPen.StartCap = System.Drawing.Drawing2D.LineCap.Flat;
                //mydashedPen.EndCap = System.Drawing.Drawing2D.LineCap.Flat;
                //// Set the DashPattern property
                //// The numbers in the array represent the lengths of the dashes and spaces respectively
                //// For example, {5, 2} would mean a pattern of 5 pixels dashed, followed by 2 pixels space
                //mydashedPen.DashPattern = new float[] { 4, 4 };
                Pen routePen;
                if (lightInfo.Colour_arc == "LITGN")
                {
                    routePen = new Pen(Color.Green, 2)
                    {
                        DashStyle = DashStyle.Dash,
                        CustomEndCap = new AdjustableArrowCap(0.0f, 0.0f, true)
                    };
                }
                else
                {
                    routePen = new Pen(Color.Black, 2)
                    {
                        DashStyle = DashStyle.Dash,
                        CustomEndCap = new AdjustableArrowCap(0.0f, 0.0f, true)
                    };
                }


                route.Stroke = routePen;
                this.Routes.Add(route);
                //now the second line
                int SecondbearingDegrees = (lightInfo.Sector2 + 180) % 360;
 //               Console.WriteLine($"bearingDegrees to use in the calculations, calculated from sectr2: {SecondbearingDegrees}");
                double SecondbearingRad = (Math.PI / 180) * SecondbearingDegrees;
                // Calculate the latitude of the second point
                double Secondlat2Rad = Math.Asin(Math.Sin(lat1Rad) * Math.Cos(angularDistance) +
                                           Math.Cos(lat1Rad) * Math.Sin(angularDistance) * Math.Cos(SecondbearingRad));
                // Calculate the longitude of the second point
                double Secondlon2Rad = lon1Rad + Math.Atan2(Math.Sin(SecondbearingRad) * Math.Sin(angularDistance) * Math.Cos(lat1Rad),
                                                     Math.Cos(angularDistance) - Math.Sin(lat1Rad) * Math.Sin(Secondlat2Rad));
                // Convert the final points to degrees deg=rad × 180/π
                double Secondlat2 = (180 / Math.PI) * Secondlat2Rad;
                double Secondlon2 = (180 / Math.PI) * Secondlon2Rad;
                PointLatLng Secondpoint2 = new PointLatLng(Secondlat2, Secondlon2);
                List<PointLatLng> Secondpoints2 = new List<PointLatLng>();
                Secondpoints2.Add(point1);
                Secondpoints2.Add(Secondpoint2);
                GMapRoute Secondroute = new GMapRoute(Secondpoints2, "My Route");
                Secondroute.Stroke = routePen;
                this.Routes.Add(Secondroute);
                //GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                //marker.Tag = properties_dictionary;
                //overlay.Markers.Add(marker);
                //pointCount++;




                //   AddSectorArc(new PointLatLng(point1.Lat, point1.Lng), lightInfo.Sector1, lightInfo.Sector2, angularDistance, metersperPixel);
                if (lightInfo.Colour_arc == "LITGN")
                {
                    AddThickArc(new PointLatLng(point1.Lat, point1.Lng), bearingDegrees, SecondbearingDegrees, angularDistance * 0.5, angularDistance * 0.58, metersperPixel, lightInfo.Colour_arc);
                }

                else if (lightInfo.Colour_arc == "LITRD")
                {
                    AddThickArc(new PointLatLng(point1.Lat, point1.Lng), bearingDegrees, SecondbearingDegrees, angularDistance * 0.6, angularDistance * 0.68, metersperPixel, lightInfo.Colour_arc);
                }
                else if (lightInfo.Colour_arc == "LITYW")
                {
                    AddThickArc(new PointLatLng(point1.Lat, point1.Lng), bearingDegrees, SecondbearingDegrees, angularDistance * 0.7, angularDistance * 0.78, metersperPixel, lightInfo.Colour_arc);
                }
                else
                {
                    AddThickArc(new PointLatLng(point1.Lat, point1.Lng), bearingDegrees, SecondbearingDegrees, angularDistance, angularDistance , metersperPixel, lightInfo.Colour_arc);  // this should never happen
                }

               // AddThickArc(new PointLatLng(point1.Lat, point1.Lng), lightInfo.Sector1, lightInfo.Sector2, angularDistance*0.5, angularDistance*0.58, metersperPixel, lightInfo.Colour_arc);
            }



        }
        public void AddThickArc(PointLatLng center, int startAngle, int endAngle, double innerRadius, double outerRadius, double metersPerPixel, string colour_arc)
        {
            // Normalize and adjust angles
            //   startAngle = (startAngle + 180) % 360;
            //  endAngle = (endAngle + 180) % 360;
  //          Console.WriteLine($"start angle to use in the calculations,: {startAngle}");
   //         Console.WriteLine($"start angle to use in the calculations: {endAngle}");
            if (endAngle < startAngle) endAngle += 360;  // handle wrap-around at 360 degrees

            var outerPoints = new List<PointLatLng>();
            var innerPoints = new List<PointLatLng>();

            // Calculate points along the arc's edge for the 'thickness' effect
            for (int angle = startAngle; angle <= endAngle; angle += 1)  // Increment by 1 degree for smoothness
            {
                // Outer radius points
                outerPoints.Add(CalculatePointAtAngle(center, angle, outerRadius));
                // Inner radius points in reverse for proper polygon drawing
                innerPoints.Insert(0, CalculatePointAtAngle(center, angle, innerRadius));
            }

            // Combine points to form a closed loop
            outerPoints.AddRange(innerPoints);
            GMapPolygon arcPolygon = new GMapPolygon(outerPoints, "Thick Arc");
            arcPolygon.Stroke = new Pen(Color.Black, 2);  // Border of the arc

            if (colour_arc == "LITGN")
            {
                arcPolygon.Fill = new SolidBrush(Color.Green);  // Fill color of the arc
            }

            else if (colour_arc == "LITRD")
            {
                arcPolygon.Fill = new SolidBrush(Color.Red);  // Fill color of the arc
            }
            else if (colour_arc == "LITYW")
            {
                arcPolygon.Fill = new SolidBrush(Color.Yellow);  // Fill color of the arc
            }
            else 
            {
                arcPolygon.Fill = new SolidBrush(Color.White);  // this should never happen
            }

            this.Polygons.Add(arcPolygon);


            //    GMapPolygon polygon = new GMapPolygon(points, "Sector Arc");
            //    polygon.Fill = new SolidBrush(Color.FromArgb(50, Color.Red)); // Semi-transparent red fill
            //    polygon.Stroke = new Pen(Color.Black, 2); // Black border
            //     this.Polygons.Add(polygon);
        }

        private PointLatLng CalculatePointAtAngle(PointLatLng center, int angle, double radius)
        {
            double angleRad = (Math.PI / 180) * angle;
            double latRad = (Math.PI / 180) * center.Lat;
            double lonRad = (Math.PI / 180) * center.Lng;

            double lat2Rad = Math.Asin(Math.Sin(latRad) * Math.Cos(radius) + Math.Cos(latRad) * Math.Sin(radius) * Math.Cos(angleRad));
            double lon2Rad = lonRad + Math.Atan2(Math.Sin(angleRad) * Math.Sin(radius) * Math.Cos(latRad),
                                                Math.Cos(radius) - Math.Sin(latRad) * Math.Sin(lat2Rad));

            return new PointLatLng((180 / Math.PI) * lat2Rad, (180 / Math.PI) * lon2Rad);
        }


        public void AddSectorArc(PointLatLng center, int startAngle, int endAngle, double radius, double metersPerPixel)
        {
            var points = new List<PointLatLng>();
            points.Add(center); // Start at the center for the polygon

            // Normalize angles
            startAngle = (startAngle + 180) % 360;
            endAngle = (endAngle + 180) % 360;

            if (endAngle < startAngle) endAngle += 360; // Ensure the arc goes around correctly

            // Calculate points along the arc
            for (int angle = startAngle; angle <= endAngle; angle++)
            {
                double bearingRad = (Math.PI / 180) * (angle % 360);
                double latRad = (Math.PI / 180) * center.Lat;
                double lonRad = (Math.PI / 180) * center.Lng;

                double lat2Rad = Math.Asin(Math.Sin(latRad) * Math.Cos(radius) + Math.Cos(latRad) * Math.Sin(radius) * Math.Cos(bearingRad));
                double lon2Rad = lonRad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(radius) * Math.Cos(latRad), Math.Cos(radius) - Math.Sin(latRad) * Math.Sin(lat2Rad));

                double lat2 = (180 / Math.PI) * lat2Rad;
                double lon2 = (180 / Math.PI) * lon2Rad;

                points.Add(new PointLatLng(lat2, lon2));
            }

            points.Add(center); // Return to center to close the polygon

            // Create and style the polygon
            GMapPolygon polygon = new GMapPolygon(points, "Sector Arc");
            polygon.Fill = new SolidBrush(Color.FromArgb(50, Color.Red)); // Semi-transparent red fill
            polygon.Stroke = new Pen(Color.Black, 2); // Black border
            this.Polygons.Add(polygon);
            // Add to overlay
            //GMapOverlay overlay = new GMapOverlay("arc_overlay");
            //overlay.Polygons.Add(polygon);
            //this.Overlays.Add(overlay);
        }
    }

    //Assign_markers_NO_SECTR
    public class GeoJsonHandler
    {

        public double SafetyDepth { get; set; } = 3.0;
        public GeoJsonHandler()
        {
            
            this.LndareOverlay = new GMapOverlay("lndareOverlay");
            this.DepareOverlay = new GMapOverlay("depareOverlay");
            this.DRGAREOverlay = new GMapOverlay("DRGAREOverlay");
            this.depthSettings = new DepthSettings(); // Initialize with default depth settings
            this.BOYLAToverlay = new GMapOverlay("BOYLAToverlay"); // Initialize the buoys overlay
            this.BOYSPPoverlay = new GMapOverlay("BOYSPPoverlay");
            this.NOTMRKoverlay = new GMapOverlay("NOTMRKoverlay");
            this.LIGHTSoverlay = new GMapOverlay("LIGHTSoverlay");
            this.BOYSAWoverlay = new GMapOverlay("BOYSAWoverlay");
            this.SOUNDGoverlay = new GMapOverlay("SOUNDGoverlay");
            this.UNHANDLEDoverlay = new GMapOverlay("BOYSAWoverlay");



            this.Generic_markers_overlay = new GMapOverlay("Generic_markers_overlay");

        }  // this is a constructor function (or method as OOP fags call it) When you call it, it creates a bunch of overlays
        public void LoadAndParseGeoJson() // this function opens the folder were ENCs are, figures out which layer they should be in and sends them to be processed in (eg) ProcessLndareFeatures for land area features
        {
            Console.WriteLine("LoadAndParseGeoJson is being run ---------------------------------------------------------------------------------");
            try
            {
                string directoryPath = "C:\\Program Files (x86)\\Mission Planner\\plugins\\ENCs"; //put your ENCs in here, by that I mean the geojson layers created from .000 files
                string[] geoJsonFiles = Directory.GetFiles(directoryPath, "*.js");

                foreach (string filePath in geoJsonFiles)
                {
                    Console.WriteLine($"Processing file: {filePath}");
                    string geoJson = System.IO.File.ReadAllText(filePath);
                    FeatureCollection featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(geoJson);

                    if (featureCollection != null)
                    {
                        if (filePath.Contains("LNDARE"))
                        {
                            try
                            {
                                ProcessLndareFeatures(featureCollection, LndareOverlay, Color.Tan);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in ProcessLndareFeatures: " + ex.Message);
                            }

                            
                        }
                        else if (filePath.Contains("MORFAC"))
                        {
                            try
                            {
                                Console.WriteLine("Starting to process MORFAC features.");
                                ProcessMORFACFeatures(featureCollection, Generic_markers_overlay, "MORFAC");
                                Console.WriteLine("Finished processing MORFAC features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in MORFAC: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("OFSPLF"))
                        {
                            try
                            {
                                Console.WriteLine("Starting to process OFSPLF features.");
                                ProcessOFSPLFFeatures(featureCollection, Generic_markers_overlay, "OFSPLF");
                                Console.WriteLine("Finished processing OFSPLF features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in OFSPLF: " + ex.Message);
                            }
                        }


                        else if (filePath.Contains("DAYMAR"))
                        {
                            try
                            {
                                Console.WriteLine("Starting to process DAYMAR features.");
                                ProcessDAYMARFeatures(featureCollection, Generic_markers_overlay, "DAYMAR");
                                Console.WriteLine("Finished processing DAYMAR features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in DAYMAR: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("BOYCAR"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process BOYCAR features.");
                                ProcessBOYCARFeatures(featureCollection, Generic_markers_overlay, "BOYCAR");
                                Console.WriteLine("Finished processing BOYCAR features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in DAYMAR: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("PILPNT"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process PILPNT features.");
                               ProcessPILPNTFeatures(featureCollection, Generic_markers_overlay, "PILPNT");
                                Console.WriteLine("Finished processing PILPNT features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in PILPNT: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("BCNSPP"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process BCNSPP features.");
                                ProcessBCNSPPFeatures(featureCollection, Generic_markers_overlay, "BCNSPP");
                                Console.WriteLine("Finished processing BCNSPP features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in BCNSPP: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("LNDMRK"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process LNDMRK features.");
                               ProcessLNDMRKFeatures(featureCollection, Generic_markers_overlay, "LNDMRK");
                                Console.WriteLine("Finished processing LNDMRK features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in LNDMRK: " + ex.Message);
                            }
                        }

                        else if (filePath.Contains("BCNLAT"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process BCNLAT features.");
                                ProcessBCNLATFeatures(featureCollection, Generic_markers_overlay, "BCNLAT");
                                Console.WriteLine("Finished processing BCNLAT features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in BCNLAT: " + ex.Message);
                            }
                        }

                        else if (filePath.Contains("DEPARE"))
                        {
                            try
                            {
                                ProcessDepareFeatures(featureCollection, DepareOverlay);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in ProcessDepareFeatures: " + ex.Message);
                            }

                        }
                        else if (filePath.Contains("DRGARE"))
                        {
                            try
                            {
                                ProcessDRGAREFeatures(featureCollection, DRGAREOverlay);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in ProcessDepareFeatures: " + ex.Message);
                            }

                        }
                        else if (filePath.Contains("LIGHTS"))
                        {
                            // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                            Console.WriteLine("Starting to process lights features.");
                           ProcessLIGHTSFeatures2(featureCollection, LIGHTSoverlay, "LIGHTS");
                            Console.WriteLine("Finished processing lights features.");
                        }
                        else if (filePath.Contains("SOUNDG"))
                        {
                           // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                            Console.WriteLine("Starting to process soundgins features.");
                          ProcessSOUNDGFeatures(featureCollection, SOUNDGoverlay, "SOUNDG");
                            Console.WriteLine("Finished processing soundings features.");
                        }
                        else if (filePath.Contains("BOYSPP"))
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                              Console.WriteLine("Starting to process BOYSPP features.");
                               ProcessBOYSPPFeatures(featureCollection, BOYSPPoverlay, "BOYSPP");
                                Console.WriteLine("Finished processing BOYSPP features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in ProcessBOYSPPFeatures: " + ex.Message);

                            }
                        else if (filePath.Contains("NOTMRK"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process NOTMRK features.");
                               ProcessNOTMRKFeatures(featureCollection, NOTMRKoverlay, "NOTMRK");
                                Console.WriteLine("Finished processing NOTMRK features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in ProcessNOTMRKFeatures: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("BOYLAT"))
                        {
                            try
                            {
                                // string nameOfMarkerType = filePath.Contains("BCNSPP") ? "BCNSPP" : "BOYLAT";
                                Console.WriteLine("Starting to process buoy features.");
                               ProcessBOYLATFeatures(featureCollection, BOYLAToverlay, "BOYLAT");
                                Console.WriteLine("Finished processing buoy features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error in ProcessBOYLATFeatures: " + ex.Message);
                            }
                        }
                        else if (filePath.Contains("BOYSAW"))
                        {
                            
                            Console.WriteLine("Starting to process BOYSAW safe water buoy features.");
                            ProcessBOYSAWFeatures(featureCollection, BOYSAWoverlay, "BOYSAW");
                            Console.WriteLine("Finished processing safe water buoy features.");
                        }
                        else
                        {
                            // This block will handle file names that do not match any predefined categories
                            try
                            {
                                string name_of_layer = Path.GetFileNameWithoutExtension(filePath).Split('_').Last().ToUpper(); // Extracts the last part after '_', removes the file extension and converts it to uppercase
                                Console.WriteLine($"Starting to process {name_of_layer} features.");
                              //  Process_unhandled_Features(featureCollection, UNHANDLEDoverlay, name_of_layer);
                                Console.WriteLine($"Finished processing {name_of_layer} features.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing unhandled features " + ex.Message);
                            }
                        }

                    }
                }


           //     Console.WriteLine($"LNDARE polygons: {LndareOverlay.Polygons.Count}");
            //    Console.WriteLine($"DEPARE polygons: {DepareOverlay.Polygons.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in LoadAndParseGeoJson: " + ex.Message);
            }
        }
        private void ProcessLndareFeatures(FeatureCollection featureCollection, GMapOverlay overlay, Color color)
        {
            string Name_of_featureCollection = "LNDARE (land  area)";
            int polygonCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                GeoJSON.Net.Geometry.Polygon polygon = feature.Geometry as GeoJSON.Net.Geometry.Polygon;
                if (polygon != null)
                {
                    List<PointLatLng> points = polygon.Coordinates[0].Coordinates.Select(coord => new PointLatLng(coord.Latitude, coord.Longitude)).ToList();
                    GMapPolygon mapPolygon = new GMapPolygon(points, "polygon" + polygonCount)
                    {
                        Stroke = new Pen(color, 1),
                        Fill = new SolidBrush(Color.FromArgb(150, color))
                    };
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                                                                               // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    mapPolygon.Tag = properties_dictionary;
                    overlay.Polygons.Add(mapPolygon);
                    polygonCount++;
                }
            }
        } //there can be Land Area points, write code to deal with that case
        private void ProcessDepareFeatures(FeatureCollection featureCollection, GMapOverlay overlay)
        {
            Console.WriteLine("processing depth areas");
            string Name_of_featureCollection = "DEPARE (depth area)";

            int polygonCount = 0;
            foreach (var feature in featureCollection.Features)
            {

                GeoJSON.Net.Geometry.Polygon polygon = feature.Geometry as GeoJSON.Net.Geometry.Polygon;
                if (polygon != null)
                {
                    // Check if "DRVAL1" exists in the feature's properties
                    if (feature.Properties.ContainsKey("DRVAL1"))
                    {
                        List<PointLatLng> points = polygon.Coordinates[0].Coordinates
                            .Select(coord => new PointLatLng(coord.Latitude, coord.Longitude))
                            .ToList();

                        // Get the depth from DRVAL1
                        double depth = Convert.ToDouble(feature.Properties["DRVAL1"]);

                        // Determine the color based on depth
                        Color color = depthSettings.GetColorFromDepth(depth);

                        // Create the GMap.NET polygon
                        GMapPolygon mapPolygon = new GMapPolygon(points, "depare" + polygonCount)
                        {
                            Stroke = new Pen(color, 1), // Stroke color and thickness
                            Fill = new SolidBrush(Color.FromArgb(150, color)) // Fill with semi-transparent color
                        };
                        var properties_dictionary = new Dictionary<string, object>();
                        properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                        // Then add other properties
                        foreach (var kvp in feature.Properties)
                        {
                            properties_dictionary[kvp.Key] = kvp.Value;
                        }
                        mapPolygon.Tag = properties_dictionary;
                        overlay.Polygons.Add(mapPolygon); // Add the polygon to the overlay


                        polygonCount++; // Increment the count
                    }
                    else
                    {
                        Console.WriteLine(" 'DRVAL1' is missing. is deepest depth present?"); // Log if key is missing
                        List<PointLatLng> points = polygon.Coordinates[0].Coordinates
                           .Select(coord => new PointLatLng(coord.Latitude, coord.Longitude))
                           .ToList();

                        // Get the depth from DRVAL1
                        double depth = Convert.ToDouble(feature.Properties["DRVAL2"]);

                        // Determine the color based on depth
                        Color color = depthSettings.GetColorFromDepth(depth);

                        // Create the GMap.NET polygon
                        GMapPolygon mapPolygon = new GMapPolygon(points, "depare" + polygonCount)
                        {
                            Stroke = new Pen(color, 1), // Stroke color and thickness
                            Fill = new SolidBrush(Color.FromArgb(150, color)) // Fill with semi-transparent color
                        };
                        var properties_dictionary = new Dictionary<string, object>();
                        properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                        // Then add other properties
                        foreach (var kvp in feature.Properties)
                        {
                            properties_dictionary[kvp.Key] = kvp.Value;
                        }
                        mapPolygon.Tag = properties_dictionary;
                        overlay.Polygons.Add(mapPolygon); // Add the polygon to the overlay
                        polygonCount++; // Increment the count
                    }
                }
            }
        }

        private void ProcessDRGAREFeatures(FeatureCollection featureCollection, GMapOverlay overlay) // dredged areasDRGARE
        {

            //var cleanedProperties = new Dictionary<string, object>();
            //foreach (var kvp in feature.Properties)
            //{   //make the attribute names upper case
            //    string cleanedKey = kvp.Key.ToUpper();
            //    string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value
            //    cleanedProperties[cleanedKey] = cleanedValue;
            //}
            Console.WriteLine("processing dredged areas");
            int polygonCount = 0;
            string Name_of_featureCollection = "DRGARE (dredged area)";
            foreach (var feature in featureCollection.Features)
            {
                GeoJSON.Net.Geometry.Polygon polygon = feature.Geometry as GeoJSON.Net.Geometry.Polygon;
                if (polygon != null)
                {
                    // Check if "DRVAL1" exists in the feature's properties
                    if (feature.Properties.ContainsKey("DRVAL1"))
                    {
                        var cleanedProperties = new Dictionary<string, object>();
                        foreach (var kvp in feature.Properties)
                        {   //make the attribute names upper case
                            string cleanedKey = kvp.Key.ToUpper();
                            string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value
                            cleanedProperties[cleanedKey] = cleanedValue;
                        }
                        List<PointLatLng> points = polygon.Coordinates[0].Coordinates
                            .Select(coord => new PointLatLng(coord.Latitude, coord.Longitude))
                            .ToList();
                        // Get the depth from DRVAL1
                        double depth = Convert.ToDouble(feature.Properties["DRVAL1"]);
                        // Determine the color based on depth
                        Color color = depthSettings.GetColorFromDepth(depth);
                        GMapPolygon mapPolygon = new GMapPolygon(points, "drgare" + polygonCount)                       
                        {
                            Stroke = new Pen(color, 1), // Stroke color and thickness
                            Fill = new SolidBrush(Color.FromArgb(150, color)) // Fill with semi-transparent color
                        };
                        var properties_dictionary = new Dictionary<string, object>();
                        properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                        // Then add other properties
                        foreach (var kvp in feature.Properties)
                        {
                            properties_dictionary[kvp.Key] = kvp.Value;
                        }                                                                                       //into a C# dictionary with keys and values (keys

                        // Add the name of the FeatureCollection to the dictionary 
                  //      properties_dictionary["Name"] = Name_of_featureCollection;
                        mapPolygon.Tag = properties_dictionary;
                        overlay.Polygons.Add(mapPolygon); // Add the polygon to the overlay
                        polygonCount++; // Increment the count
                    }
                    else
                    {
                        Console.WriteLine(" 'DRVAL1' is missing. is deepest depth present?"); // Log if key is missing
                        List<PointLatLng> points = polygon.Coordinates[0].Coordinates
                           .Select(coord => new PointLatLng(coord.Latitude, coord.Longitude))
                           .ToList();

                        // Get the depth from DRVAL2
                        double depth = Convert.ToDouble(feature.Properties["DRVAL2"]);

                        // Determine the color based on depth
                        Color color = depthSettings.GetColorFromDepth(depth);
                        Color greySemiTransparent = Color.FromArgb(150, Color.Gray); // 150 is the alpha value for transparency
                        GMapPolygon mapPolygon = new GMapPolygon(points, "drgare" + polygonCount)                        {
                            Stroke = new Pen(greySemiTransparent, 1), // Stroke color and thickness
                            Fill = new SolidBrush(Color.FromArgb(150, color)) // Fill with semi-transparent color
                        };
                        var properties_dictionary = feature.Properties.ToDictionary(p => p.Key, p => p.Value); //this converts the properties of our geojson feature
                        mapPolygon.Tag = properties_dictionary;
                        overlay.Polygons.Add(mapPolygon); // Add the polygon to the overlay
                        polygonCount++; // Increment the count
                    }
                }
            }
        }

        private void ProcessSOUNDGFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
  //              Console.WriteLine("ANOTHER SOUNDG FEATURE IS BEING READ");

                if (feature.Geometry is GeoJSON.Net.Geometry.MultiPoint multiPoint)
                {
                    foreach (var point in multiPoint.Coordinates)
                    {
                        if (point is GeoJSON.Net.Geometry.Point geoPoint)
                        {
                            // Get Longitude, Latitude, and Altitude (if available)
                            double x = geoPoint.Coordinates.Longitude; // Longitude (x)
                            double y = geoPoint.Coordinates.Latitude;  // Latitude (y)
                                                                       // Check for Altitude (z) and handle null
                            double z = geoPoint.Coordinates.Altitude.HasValue
                                ? (double)geoPoint.Coordinates.Altitude // Explicit cast to double
                                : double.NaN; // If Altitude is null, set to NaN or a default value

                            //   Console.WriteLine($"x: {x}, y: {y}, z: {z}");
                            PointLatLng pointLatLng = new PointLatLng(y, x);

                            //string iconFilename;
                            if (z < SafetyDepth)
                            {
                                ////////////// iconFilename = $"{Math.Floor(z)}_B.png"; // Black icon for shallow depths
                                // Get the integral part of z (integer before the decimal point)
                                int integralPart = (int)Math.Floor(z); // This should give you the whole number

                                // Get the decimal part of z (fractional part)
                                int decimalPart = (int)((z - integralPart) * 10); // Getting the first decimal digit

                                // Creating markers for tens and units (left-aligned)
                                if (integralPart > 9) // If greater than 9, separate into tens and units
                                {
                                    int tens = integralPart / 10; // Tens digit
                                    int units = integralPart % 10; // Units digit

                                    // Marker for tens (slightly to the left)
                                    Bitmap tensBitmap = new Bitmap($"C:\\\\Program Files (x86)\\\\Mission Planner\\\\plugins\\\\icons\"{tens}_B.png");
                                    GMarkerGoogle tensMarker = new GMarkerGoogle(pointLatLng, tensBitmap);
                                    tensMarker.Offset = new System.Drawing.Point(-10, 0); // Slightly left
                                    SOUNDGoverlay.Markers.Add(tensMarker);

                                    // Marker for units (centered)
                                    Bitmap unitsBitmap = new Bitmap($"C:\\\\Program Files (x86)\\\\Mission Planner\\\\plugins\\\\icons\\\\{units}_B.png");
                                    GMarkerGoogle unitsMarker = new GMarkerGoogle(pointLatLng, unitsBitmap);
                                    unitsMarker.Offset = new System.Drawing.Point(0, 0); // No offset
                                    SOUNDGoverlay.Markers.Add(unitsMarker);
                                }
                                else // If single-digit, create a marker for the units
                                {
                                    Bitmap unitsBitmap = new Bitmap($"C:\\\\Program Files (x86)\\\\Mission Planner\\\\plugins\\\\icons\\\\{integralPart}_B.png");
                                    GMarkerGoogle unitsMarker = new GMarkerGoogle(pointLatLng, unitsBitmap);
                                    unitsMarker.Offset = new System.Drawing.Point(0, 0); // No offset
                                    SOUNDGoverlay.Markers.Add(unitsMarker);
                                }

                                // Marker for the decimal part (slightly to the right and down)
                                Bitmap decimalBitmap = new Bitmap($"C:\\\\Program Files (x86)\\\\Mission Planner\\\\plugins\\\\icons\\\\{decimalPart}_B.png");
                                GMarkerGoogle decimalMarker = new GMarkerGoogle(pointLatLng, decimalBitmap);
                                decimalMarker.Offset = new System.Drawing.Point(10, 5); // Right and downward
                                SOUNDGoverlay.Markers.Add(decimalMarker);
                            }
                            else
                            {
                                //    iconFilename = $"gray_{Math.Floor(z)}_B.png"; // Gray icon for deeper depths CHANGE THE NAME ONCE YOU MAKE THE ICONS
                            }
                        }

                        //   var position = (GeoJSON.Net.Geometry.Position)coordinate;
                        //var Latitude = (GeoJSON.Net.Geometry.MultiPoint)coordinate.Coordinates.Latitude;
                        //    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                        //var properties_dictionary = feature.Properties.ToDictionary(p => p.Key, p => p.Value); //this converts the properties of our geojson feature
                        //                                                                                       //into a C# dictionary with keys and values (keys are the propertiy names)
                        //foreach (var thing in properties_dictionary)//PRINTING TO CONSOLE
                        //{//PRINTING TO CONSOLE
                        //    Console.WriteLine(thing); //PRINTING TO CONSOLE
                        //}//PRINTING TO CONSOLE
                        //Console.WriteLine("HERE WE ASSIGN THE iconFilename, the png");//PRINTING TO CONSOLE

                        //string iconFilename = GetIconForFeature(properties_dictionary, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                        ////Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                        //string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                        //Bitmap iconBitmap;
                        //if (File.Exists(iconPath))
                        //{
                        //    iconBitmap = new Bitmap(iconPath);
                        //}
                        //else
                        //{
                        //    Console.WriteLine("USING DEFAULT IMAGE");
                        //    Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        //    Console.WriteLine("Icon file not found, using default: " + iconPath);
                        //    string default_iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "LIGHTS_COLOUR6.png");
                        //    //      iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "LIGHTS_COLOUR6.png"));
                        //    iconBitmap = new Bitmap(default_iconPath);
                        //    // iconBitmap = new Bitmap(iconBitmap, new Size(16, 16)); // Resize the default icon
                        //}
                        ////        marker.Offset = new Point(0, iconBitmap.Height / 2); // This will center the marker's anchor point


                        //GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                        //marker.Tag = properties_dictionary;
                        //marker.Offset = new System.Drawing.Point(0, -5);
                        //overlay.Markers.Add(marker);
                        //pointCount++;
                    }
                }
            }

            //Console.WriteLine($"Processed {pointCount} lights features for {nameOfMarkerType}.");
        }
        private void ProcessMORFACFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "MORFAC";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {   //make the attribute names upper case
                        string cleanedKey = kvp.Key.ToUpper();
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value
                        cleanedProperties[cleanedKey] = cleanedValue;
                    }

                    List<string> relevant_attributes = new List<string> { "BOYSHP", "CATMOR" };


                    List<string> availableIcons = new List<string> {
                        "MORFAC_CATMOR1.png", "MORFAC_CATMOR2.png", "MORFAC_CATMOR3.png", "MORFAC_CATMOR5.png", "MORFAC_CATMOR7.png", "MORFAC_.png", "MORFAC_CATMOR7_BOYSHP3.png", "MORFAC_CATMOR7_BOYSHP6.png"                    };
                   //nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    //Console.WriteLine("We chose this icon");//PRINTING TO CONSOLE
                    //Console.WriteLine(iconFilename);//PRINTING TO CONSOLE
                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        // iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                    //foreach (var kvp in cleanedProperties)
                    //{
                    //    Console.WriteLine(kvp.Key);
                    //    Console.WriteLine(kvp.Value);

                    //    //    string cleanedKey = kvp.Key;
                    //    //    string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                    //    //    cleanedProperties[cleanedKey] = cleanedValue;
                    //}
                    //foreach (var kvp2 in properties_dictionary)
                    //{
                    //    Console.WriteLine("looking for CATCAM 1");

                    //    Console.WriteLine(kvp2.Key);
                    //    Console.WriteLine(kvp2.Value);

                    //    if (kvp2.Key == "CATCAM" && kvp2.Value.ToString() == "1") // 
                    //    {
                    //        Console.WriteLine("CATCAM = 1 object");
                    //        string iconFilename2 = "TOPMAR05.png";
                    //        string iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", iconFilename2);
                    //        Bitmap iconBitmap2;

                    //        if (System.IO.File.Exists(iconPath2))
                    //        {
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected variable name
                    //        }
                    //        else
                    //        {
                    //            Console.WriteLine("USING DEFAULT IMAGE");
                    //            Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                    //            Console.WriteLine("Icon file not found, using default: " + iconPath2);
                    //            iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected to use iconPath2
                    //        }

                    //        GMarkerGoogle marker2 = new GMarkerGoogle(point, iconBitmap2);
                    //        marker2.Offset = new System.Drawing.Point(-3, -35);
                    //        overlay.Markers.Add(marker2);
                    //    }
                    //}




                }
            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }
        private void ProcessOFSPLFFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "OFSPLF";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }



                    //List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR", "CATCAM" };


                    //List<string> availableIcons = new List<string> {
                    //    "BOYCAR_CATCAM1.png", "BOYCAR_CATCAM2.png", "BOYCAR_CATCAM3.png", "BOYCAR_CATCAM4.png", "BOYCAR_.png", "BOYCAR_BOYSHP1_COLOUR2-6-2.png", "BOYCAR_BOYSHP1_COLOUR6-2-6.png", "BOYCAR_BOYSHP2_COLOUR2-6-2.png", "BOYCAR_BOYSHP2_COLOUR6-2-6.png", "BOYCAR_BOYSHP3_COLOUR2-6-2.png", "BOYCAR_BOYSHP3_COLOUR6-2-6.png", "BOYCAR_BOYSHP4_COLOUR2-6-2.png", "BOYCAR_BOYSHP4_COLOUR6-2-6.png", "BOYCAR_BOYSHP5_COLOUR2-6-2.png", "BOYCAR_BOYSHP5_COLOUR6-2-6.png", "BOYCAR_BOYSHP1_COLOUR2-6.png", "BOYCAR_BOYSHP1_COLOUR6-2.png", "BOYCAR_BOYSHP2_COLOUR2-6.png", "BOYCAR_BOYSHP2_COLOUR6-2.png", "BOYCAR_BOYSHP3_COLOUR2-6.png", "BOYCAR_BOYSHP3_COLOUR6-2.png", "BOYCAR_BOYSHP4_COLOUR2-6.png", "BOYCAR_BOYSHP4_COLOUR6-2.png", "BOYCAR_BOYSHP5_COLOUR2-6.png", "BOYCAR_BOYSHP5_COLOUR6-2.png", "BOYCAR_BOYSHP4_CATCAM1.png", "BOYCAR_BOYSHP4_CATCAM2.png", "BOYCAR_BOYSHP4_CATCAM3.png", "BOYCAR_BOYSHP4_CATCAM4.png", "BOYCAR_BOYSHP5_CATCAM1.png", "BOYCAR_BOYSHP5_CATCAM2.png", "BOYCAR_BOYSHP5_CATCAM3.png", "BOYCAR_BOYSHP5_CATCAM4.png", "BOYCAR_BOYSHP1.png", "BOYCAR_BOYSHP2.png", "BOYCAR_BOYSHP3.png", "BOYCAR_BOYSHP4.png", "BOYCAR_BOYSHP5.png", "BOYCAR_BOYSHP6.png", "BOYCAR_BOYSHP7.png", "BOYCAR_BOYSHP8.png"
                    //};
                    //nameOfMarkerType = nameOfMarkerType.ToLower();
                    //string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                    //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));
                    string iconFilename = "OFSPLF_.png";
                    //string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);


                    //  string iconFilename = GetIconForFeature(properties_dictionary, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        // iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                    //foreach (var kvp in cleanedProperties)
                    //{
                    //    Console.WriteLine(kvp.Key);
                    //    Console.WriteLine(kvp.Value);

                    //    //    string cleanedKey = kvp.Key;
                    //    //    string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                    //    //    cleanedProperties[cleanedKey] = cleanedValue;
                    //}
                    //foreach (var kvp2 in properties_dictionary)
                    //{
                    //    Console.WriteLine("looking for CATCAM 1");

                    //    Console.WriteLine(kvp2.Key);
                    //    Console.WriteLine(kvp2.Value);

                    //    if (kvp2.Key == "CATCAM" && kvp2.Value.ToString() == "1") // 
                    //    {
                    //        Console.WriteLine("CATCAM = 1 object");
                    //        string iconFilename2 = "TOPMAR05.png";
                    //        string iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", iconFilename2);
                    //        Bitmap iconBitmap2;

                    //        if (System.IO.File.Exists(iconPath2))
                    //        {
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected variable name
                    //        }
                    //        else
                    //        {
                    //            Console.WriteLine("USING DEFAULT IMAGE");
                    //            Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                    //            Console.WriteLine("Icon file not found, using default: " + iconPath2);
                    //            iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected to use iconPath2
                    //        }

                    //        GMarkerGoogle marker2 = new GMarkerGoogle(point, iconBitmap2);
                    //        marker2.Offset = new System.Drawing.Point(-3, -35);
                    //        overlay.Markers.Add(marker2);
                    //    }
                    //}




                }
            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }

        private void ProcessBOYCARFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "BOYCAR";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }



                    List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR","CATCAM" };


                    List<string> availableIcons = new List<string> {
                        "BOYCAR_CATCAM1.png", "BOYCAR_CATCAM2.png", "BOYCAR_CATCAM3.png", "BOYCAR_CATCAM4.png", "BOYCAR_.png", "BOYCAR_BOYSHP1_COLOUR2-6-2.png", "BOYCAR_BOYSHP1_COLOUR6-2-6.png", "BOYCAR_BOYSHP2_COLOUR2-6-2.png", "BOYCAR_BOYSHP2_COLOUR6-2-6.png", "BOYCAR_BOYSHP3_COLOUR2-6-2.png", "BOYCAR_BOYSHP3_COLOUR6-2-6.png", "BOYCAR_BOYSHP4_COLOUR2-6-2.png", "BOYCAR_BOYSHP4_COLOUR6-2-6.png", "BOYCAR_BOYSHP5_COLOUR2-6-2.png", "BOYCAR_BOYSHP5_COLOUR6-2-6.png", "BOYCAR_BOYSHP1_COLOUR2-6.png", "BOYCAR_BOYSHP1_COLOUR6-2.png", "BOYCAR_BOYSHP2_COLOUR2-6.png", "BOYCAR_BOYSHP2_COLOUR6-2.png", "BOYCAR_BOYSHP3_COLOUR2-6.png", "BOYCAR_BOYSHP3_COLOUR6-2.png", "BOYCAR_BOYSHP4_COLOUR2-6.png", "BOYCAR_BOYSHP4_COLOUR6-2.png", "BOYCAR_BOYSHP5_COLOUR2-6.png", "BOYCAR_BOYSHP5_COLOUR6-2.png", "BOYCAR_BOYSHP4_CATCAM1.png", "BOYCAR_BOYSHP4_CATCAM2.png", "BOYCAR_BOYSHP4_CATCAM3.png", "BOYCAR_BOYSHP4_CATCAM4.png", "BOYCAR_BOYSHP5_CATCAM1.png", "BOYCAR_BOYSHP5_CATCAM2.png", "BOYCAR_BOYSHP5_CATCAM3.png", "BOYCAR_BOYSHP5_CATCAM4.png", "BOYCAR_BOYSHP1.png", "BOYCAR_BOYSHP2.png", "BOYCAR_BOYSHP3.png", "BOYCAR_BOYSHP4.png", "BOYCAR_BOYSHP5.png", "BOYCAR_BOYSHP6.png", "BOYCAR_BOYSHP7.png", "BOYCAR_BOYSHP8.png"
                    };
                    //nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    //string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);


                    //  string iconFilename = GetIconForFeature(properties_dictionary, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                       // iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
          //          Console.WriteLine("BOYCAR object is being added");
                    foreach (var kvp in cleanedProperties)
                    {
             //           Console.WriteLine(kvp.Key);
              //          Console.WriteLine(kvp.Value);

                        //    string cleanedKey = kvp.Key;
                        //    string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        //    cleanedProperties[cleanedKey] = cleanedValue;
                    }
                    foreach (var kvp2 in properties_dictionary)
                    {
         //               Console.WriteLine("looking for CATCAM 1");

//                        Console.WriteLine(kvp2.Key);
 //                       Console.WriteLine(kvp2.Value);

                        if (kvp2.Key == "CATCAM" && kvp2.Value.ToString() == "1") // 
                        {
           //                 Console.WriteLine("CATCAM = 1 object");
                            string iconFilename2 = "TOPMAR05.png";
                            string iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", iconFilename2);
                            Bitmap iconBitmap2;

                            if (System.IO.File.Exists(iconPath2))
                            {
                                iconBitmap2 = new Bitmap(iconPath2); // Corrected variable name
                            }
                            else
                            {
                                Console.WriteLine("USING DEFAULT IMAGE");
                                Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                                Console.WriteLine("Icon file not found, using default: " + iconPath2);
                                iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                                iconBitmap2 = new Bitmap(iconPath2); // Corrected to use iconPath2
                            }

                            GMarkerGoogle marker2 = new GMarkerGoogle(point, iconBitmap2);
                            marker2.Offset = new System.Drawing.Point(-3, -35);
                            overlay.Markers.Add(marker2);
                        }
                    }




                }
            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }

        private void ProcessPILPNTFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)
        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //     Console.WriteLine("ANOTHER BOYSPP FEATURE IS BEING READ");

                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);
                    string Name_of_featureCollection = "PILPNT";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {   //make the attribute names upper case
                        string cleanedKey = kvp.Key.ToUpper();
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value
                        cleanedProperties[cleanedKey] = cleanedValue;
                    }

                    //     Console.WriteLine("Cleaned properties:");
                    foreach (var kvp in cleanedProperties)

                    {
                        //     Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                    }
                    List<string> availableIcons = new List<string>
    {
         "BOYSPP_CATSPM19_BOYSHP1.png","BOYSPP_BOYSHP1_COLOUR11-1.png","BOYSPP_COLOUR1.png", "BOYSPP_CATSPM19_BOYSHP2.png", "BOYSPP_CATSPM54_BOYSHP1.png", "BOYSPP_CATSPM54_BOYSHP2.png", "BOYSPP_CATSPM54_BOYSHP4.png", "BOYSPP_CATSPM54_BOYSHP5.png", "BOYSPP_CATSPM15.png", "BOYSPP_CATSPM52.png", "BOYSPP_BOYSHP1.png", "BOYSPP_BOYSHP2.png", "BOYSPP_BOYSHP3.png", "BOYSPP_BOYSHP4.png", "BOYSPP_BOYSHP5.png", "BOYSPP_BOYSHP6.png", "BOYSPP_BOYSHP7.png", "BOYSPP_BOYSHP8.png", "BOYSPP_CATSPM9.png", "BOYSPP_.png", "BOYSPP_BOYSHP1_COLOUR4-1-4-1-4_COLPAT1.png", "BOYSPP_BOYSHP1_COLOUR5-3-1-5_COLPAT1-2.png", "BOYSPP_BOYSHP1_COLOUR1-11-1_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR1-11-1_COLPAT1.png", "BOYSPP_BOYSHP3_COLOUR3-4-3_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR1-11_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR11-1_COLPAT1.png", "BOYSPP_BOYSHP3_COLOUR1-11_COLPAT1.png", "BOYSPP_BOYSHP4_COLOUR1-11_COLPAT1.png", "BOYSPP_BOYSHP1_COLOUR4-1_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR3-1_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR3-4_COLPAT1.png", "BOYSPP_BOYSHP4_COLOUR3-1_COLPAT2.png", "BOYSPP_BOYSHP4_COLOUR3-1_COLPAT4.png", "BOYSPP_BOYSHP7_COLPAT1_COLOUR3-1.png", "BOYSPP_BOYSHP7_COLPAT2_COLOUR3-1.png", "BOYSPP_CATSPM14_BOYSHP2_COLOUR1.png", "BOYSPP_CATSPM8_BOYSHP2_COLOUR3.png", "BOYSPP_BOYSHP1_COLOUR1-11.png", "BOYSPP_BOYSHP2_COLOUR1-11.png", "BOYSPP_BOYSHP3_COLOUR1-11.png", "BOYSPP_BOYSHP4_COLOUR1-11.png", "BOYSPP_BOYSHP4_COLOUR4-3.png", "BOYSPP_BOYSHP2_COLOUR11.png", "BOYSPP_BOYSHP4_COLOUR11.png", "BOYSPP_CATSPM14_BOYSHP2.png", "BOYSPP_BOYSHP1_COLOUR3.png", "BOYSPP_BOYSHP1_COLOUR6.png", "BOYSPP_BOYSHP2_COLOUR1.png", "BOYSPP_BOYSHP2_COLOUR2.png", "BOYSPP_BOYSHP2_COLOUR3.png", "BOYSPP_BOYSHP2_COLOUR4.png", "BOYSPP_BOYSHP2_COLOUR6.png", "BOYSPP_BOYSHP3_COLOUR1.png", "BOYSPP_BOYSHP3_COLOUR3.png", "BOYSPP_BOYSHP3_COLOUR6.png", "BOYSPP_BOYSHP4_COLOUR3.png", "BOYSPP_BOYSHP4_COLOUR4.png", "BOYSPP_BOYSHP4_COLOUR6.png", "BOYSPP_BOYSHP5_COLOUR6.png", "BOYSPP_BOYSHP6_COLOUR3.png", "BOYSPP_BOYSHP6_COLOUR4.png", "BOYSPP_BOYSHP6_COLOUR6.png", "BOYSPP_BOYSHP7_COLOUR6.png", "BOYSPP_COLOUR6.png"    };

                    List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR", "COLPAT", "CATSPM" };

                    //      Console.WriteLine("HERE WE ASSIGN THE iconFilename, that is to say, we find the name of the png");
                    nameOfMarkerType = nameOfMarkerType.ToUpper();
                    //PASS nameOfMarkerType as upper case, relevant_attributes as upper case
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                // string iconFilename = GetIconForFeature(cleanedProperties, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "PILPNT_.png");

                    //    Console.WriteLine($"Icon path: {iconPath}");

                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        //    Console.WriteLine("USING DEFAULT IMAGE");
                        //      Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(cleanedProperties, Formatting.Indented));
                        //      Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                   //     iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);

                    pointCount++;
                }
            }

        }


        private void ProcessBOYLATFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
               // Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "BOYLAT";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }



                    List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR", "COLPAT", "CATLAM"};


                    List<string> availableIcons = new List<string> {
                      "BOYLAT_BOYSHP1_COLOUR3-4-3.png", "BOYLAT_BOYSHP1_COLOUR4-3-4.png", "BOYLAT_BOYSHP2_COLOUR3-4-3.png", "BOYLAT_BOYSHP2_COLOUR4-3-4.png", "BOYLAT_CATLAM3_COLOUR3-4-3.png", "BOYLAT_CATLAM3_COLOUR4-3-4.png", "BOYLAT_CATLAM4_COLOUR3-4-3.png", "BOYLAT_CATLAM4_COLOUR4-3-4.png", "BOYLAT_BOYSHP1_COLOUR3.png", "BOYLAT_BOYSHP1_COLOUR4.png", "BOYLAT_BOYSHP2_COLOUR3.png", "BOYLAT_BOYSHP2_COLOUR4.png", "BOYLAT_CATLAM1_COLOUR3.png", "BOYLAT_CATLAM1_COLOUR4.png", "BOYLAT_CATLAM2_COLOUR3.png", "BOYLAT_CATLAM2_COLOUR4.png", "BOYLAT_.png", "BOYLAT_BOYSHP2_CATLAM7_COLOUR3-1-3-1-3.png", "BOYLAT_BOYSHP2_CATLAM7_COLOUR3-1-3-1.png", "BOYLAT_BOYSHP1_COLOUR3-4-3_COLPAT1.png", "BOYLAT_BOYSHP2_COLOUR4-3-4_COLPAT1.png", "BOYLAT_BOYSHP4_COLOUR3-4-3_COLPAT1.png", "BOYLAT_BOYSHP4_COLOUR4-3-4_COLPAT1.png", "BOYLAT_BOYSHP3_COLOUR3-4_COLPAT1.png", "BOYLAT_BOYSHP3_COLOUR4-3_COLPAT1.png", "BOYLAT_BOYSHP4_COLOUR4-3_COLPAT1.png", "BOYLAT_BOYSHP3_CATLAM23_COLOUR6.png", "BOYLAT_BOYSHP1_COLOUR4-1-4-1-4.png", "BOYLAT_BOYSHP2_COLOUR3-1-3-1-3.png", "BOYLAT_BOYSHP3_COLOUR3-4-3-4-3.png", "BOYLAT_BOYSHP4_COLOUR3-4-3.png", "BOYLAT_BOYSHP4_COLOUR4-3-4.png", "BOYLAT_BOYSHP1_COLOUR4-1.png", "BOYLAT_BOYSHP1_COLOUR4-3.png", "BOYLAT_BOYSHP2_COLOUR3-1.png", "BOYLAT_BOYSHP2_COLOUR3-4.png", "BOYLAT_BOYSHP3_COLOUR3-4.png", "BOYLAT_BOYSHP1_COLOUR2.png", "BOYLAT_BOYSHP1_COLOUR6.png", "BOYLAT_BOYSHP2_COLOUR6.png", "BOYLAT_BOYSHP4_COLOUR3.png", "BOYLAT_BOYSHP4_COLOUR4.png", "BOYLAT_BOYSHP5_COLOUR1.png", "BOYLAT_BOYSHP5_COLOUR3.png", "BOYLAT_BOYSHP5_COLOUR4.png", "BOYLAT_COLOUR3_CATLAM2.png", "BOYLAT_COLOUR4_CATLAM1.png", "BOYLAT_BOYSHP1.png", "BOYLAT_BOYSHP2.png", "BOYLAT_BOYSHP3.png", "BOYLAT_BOYSHP4.png", "BOYLAT_BOYSHP5.png", "BOYLAT_BOYSHP6.png", "BOYLAT_BOYSHP7.png", "BOYLAT_BOYSHP8.png", "BOYLAT_BOYSHP5_COLOUR4-1-4-1.png", "BOYLAT_BOYSHP5_COLOUR3-1-3-1.png", "BOYLAT_BOYSHP5_COLOUR4-3.png", "BOYLAT_BOYSHP5_COLOUR3-4.png", "BOYLAT_BOYSHP5_COLOUR3-1_COLPAT1.png", "BOYLAT_BOYSHP5_COLOUR4-1_COLPAT1.png", "BOYLAT_BOYSHP5_COLOUR4-3-4.png", "BOYLAT_BOYSHP5_COLOUR3-4-3.png"
                    };
                   //nameOfMarkerType = nameOfMarkerType.ToLower();
                   string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                    //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    //string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);


                  //  string iconFilename = GetIconForFeature(properties_dictionary, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                    //foreach (var kvp2 in properties_dictionary)
                    //{
                    //    Console.WriteLine("looking for CATCAM 1");

                    //    Console.WriteLine(kvp2.Key);
                    //    Console.WriteLine(kvp2.Value);

                    //    if (kvp2.Key == "CATCAM" && kvp2.Value.ToString() == "1") // 
                    //    {
                    //        Console.WriteLine("CATCAM = 1 object");
                    //        string iconFilename2 = "TOPMAR05.png";
                    //        string iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", iconFilename2);
                    //        Bitmap iconBitmap2;

                    //        if (System.IO.File.Exists(iconPath2))
                    //        {
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected variable name
                    //        }
                    //        else
                    //        {
                    //            Console.WriteLine("USING DEFAULT IMAGE");
                    //            Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                    //            Console.WriteLine("Icon file not found, using default: " + iconPath2);
                    //            iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected to use iconPath2
                    //        }

                    //        GMarkerGoogle marker2 = new GMarkerGoogle(point, iconBitmap2);
                    //        marker2.Offset = new System.Drawing.Point(-3, -35);
                    //        overlay.Markers.Add(marker2);
                    //    }
                    //}
                }

            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }

        private void ProcessBOYSAWFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {

                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use
                    string Name_of_featureCollection = "BOYSAW";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();



                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }
               //     Console.WriteLine("below are the properties");//PRINTING TO CONSOLE
                    foreach (var thing in properties_dictionary)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
               //         Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE

                //    Console.WriteLine("below are the cleaned properties");//PRINTING TO CONSOLE

                    foreach (var thing in cleanedProperties)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
    //                    Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE
                    //Console.WriteLine("HERE WE ASSIGN THE iconFilename, the png");//PRINTING TO CONSOLE

                    List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR", "COLPAT" };

                    List<string> availableIcons = new List<string> {
                    "BOYSAW_.png", "BOYSAW_BOYSHP3_COLOUR3-1_COLPAT2.png", "BOYSAW_BOYSHP4_COLOUR3-1_COLPAT2.png", "BOYSAW_BOYSHP1_COLOUR3-1.png", "BOYSAW_BOYSHP3_COLOUR3-1.png", "BOYSAW_BOYSHP4_COLOUR3-1.png", "BOYSAW_BOYSHP4_COLOUR3.png", "BOYSAW_BOYSHP3.png", "BOYSAW_BOYSHP4.png", "BOYSAW_BOYSHP5.png", "BOYSAW_BOYSHP6.png", "BOYSAW_BOYSHP7.png", "BOYSAW_BOYSHP8.png"
                    };
                    ////nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename

                    //string iconFilename = GetIconForFeature(properties_dictionary, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        // iconBitmap = new Bitmap(iconBitmap, new Size(16, 16)); // Resize the default icon
                    }
                    //        marker.Offset = new Point(0, iconBitmap.Height / 2); // This will center the marker's anchor point


                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    //marker.Offset = new System.Drawing.Point(0, -5);
                    overlay.Markers.Add(marker);
                    pointCount++;
                    //foreach (var kvp2 in properties_dictionary)
                    //{
                    //    Console.WriteLine("looking for CATCAM 1");

                    //    Console.WriteLine(kvp2.Key);
                    //    Console.WriteLine(kvp2.Value);

                    //    if (kvp2.Key == "CATCAM" && kvp2.Value.ToString() == "1") // 
                    //    {
                    //        Console.WriteLine("CATCAM = 1 object");
                    //        string iconFilename2 = "TOPMAR05.png";
                    //        string iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", iconFilename2);
                    //        Bitmap iconBitmap2;

                    //        if (System.IO.File.Exists(iconPath2))
                    //        {
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected variable name
                    //        }
                    //        else
                    //        {
                    //            Console.WriteLine("USING DEFAULT IMAGE");
                    //            Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                    //            Console.WriteLine("Icon file not found, using default: " + iconPath2);
                    //            iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected to use iconPath2
                    //        }

                    //        GMarkerGoogle marker2 = new GMarkerGoogle(point, iconBitmap2);
                    //        marker2.Offset = new System.Drawing.Point(-3, -35);
                    //        overlay.Markers.Add(marker2);
                    //    }
                    //}
                }
            }

            Console.WriteLine($"Processed {pointCount} safe water buoy (BOYSAW) features for {nameOfMarkerType}.");
        }
        private void ProcessBOYSPPFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)
        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //     Console.WriteLine("ANOTHER BOYSPP FEATURE IS BEING READ");

                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);
                    string Name_of_featureCollection = "BOYSPP";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {   //make the attribute names upper case
                        string cleanedKey = kvp.Key.ToUpper();
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value
                        cleanedProperties[cleanedKey] = cleanedValue;
                    }

                    //     Console.WriteLine("Cleaned properties:");
                    foreach (var kvp in cleanedProperties)

                    {
                   //     Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                    }
                    List<string> availableIcons = new List<string>
    {
         "BOYSPP_CATSPM19_BOYSHP1.png","BOYSPP_BOYSHP1_COLOUR11-1.png","BOYSPP_COLOUR1.png", "BOYSPP_CATSPM19_BOYSHP2.png", "BOYSPP_CATSPM54_BOYSHP1.png", "BOYSPP_CATSPM54_BOYSHP2.png", "BOYSPP_CATSPM54_BOYSHP4.png", "BOYSPP_CATSPM54_BOYSHP5.png", "BOYSPP_CATSPM15.png", "BOYSPP_CATSPM52.png", "BOYSPP_BOYSHP1.png", "BOYSPP_BOYSHP2.png", "BOYSPP_BOYSHP3.png", "BOYSPP_BOYSHP4.png", "BOYSPP_BOYSHP5.png", "BOYSPP_BOYSHP6.png", "BOYSPP_BOYSHP7.png", "BOYSPP_BOYSHP8.png", "BOYSPP_CATSPM9.png", "BOYSPP_.png", "BOYSPP_BOYSHP1_COLOUR4-1-4-1-4_COLPAT1.png", "BOYSPP_BOYSHP1_COLOUR5-3-1-5_COLPAT1-2.png", "BOYSPP_BOYSHP1_COLOUR1-11-1_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR1-11-1_COLPAT1.png", "BOYSPP_BOYSHP3_COLOUR3-4-3_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR1-11_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR11-1_COLPAT1.png", "BOYSPP_BOYSHP3_COLOUR1-11_COLPAT1.png", "BOYSPP_BOYSHP4_COLOUR1-11_COLPAT1.png", "BOYSPP_BOYSHP1_COLOUR4-1_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR3-1_COLPAT1.png", "BOYSPP_BOYSHP2_COLOUR3-4_COLPAT1.png", "BOYSPP_BOYSHP4_COLOUR3-1_COLPAT2.png", "BOYSPP_BOYSHP4_COLOUR3-1_COLPAT4.png", "BOYSPP_BOYSHP7_COLPAT1_COLOUR3-1.png", "BOYSPP_BOYSHP7_COLPAT2_COLOUR3-1.png", "BOYSPP_CATSPM14_BOYSHP2_COLOUR1.png", "BOYSPP_CATSPM8_BOYSHP2_COLOUR3.png", "BOYSPP_BOYSHP1_COLOUR1-11.png", "BOYSPP_BOYSHP2_COLOUR1-11.png", "BOYSPP_BOYSHP3_COLOUR1-11.png", "BOYSPP_BOYSHP4_COLOUR1-11.png", "BOYSPP_BOYSHP4_COLOUR4-3.png", "BOYSPP_BOYSHP2_COLOUR11.png", "BOYSPP_BOYSHP4_COLOUR11.png", "BOYSPP_CATSPM14_BOYSHP2.png", "BOYSPP_BOYSHP1_COLOUR3.png", "BOYSPP_BOYSHP1_COLOUR6.png", "BOYSPP_BOYSHP2_COLOUR1.png", "BOYSPP_BOYSHP2_COLOUR2.png", "BOYSPP_BOYSHP2_COLOUR3.png", "BOYSPP_BOYSHP2_COLOUR4.png", "BOYSPP_BOYSHP2_COLOUR6.png", "BOYSPP_BOYSHP3_COLOUR1.png", "BOYSPP_BOYSHP3_COLOUR3.png", "BOYSPP_BOYSHP3_COLOUR6.png", "BOYSPP_BOYSHP4_COLOUR3.png", "BOYSPP_BOYSHP4_COLOUR4.png", "BOYSPP_BOYSHP4_COLOUR6.png", "BOYSPP_BOYSHP5_COLOUR6.png", "BOYSPP_BOYSHP6_COLOUR3.png", "BOYSPP_BOYSHP6_COLOUR4.png", "BOYSPP_BOYSHP6_COLOUR6.png", "BOYSPP_BOYSHP7_COLOUR6.png", "BOYSPP_COLOUR6.png"    };

                    List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR", "COLPAT", "CATSPM" };

                    //      Console.WriteLine("HERE WE ASSIGN THE iconFilename, that is to say, we find the name of the png");
                    nameOfMarkerType = nameOfMarkerType.ToUpper();
                    //PASS nameOfMarkerType as upper case, relevant_attributes as upper case
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                // string iconFilename = GetIconForFeature(cleanedProperties, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);

                    //    Console.WriteLine($"Icon path: {iconPath}");

                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        //    Console.WriteLine("USING DEFAULT IMAGE");
                        //      Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(cleanedProperties, Formatting.Indented));
                        //      Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);

                    pointCount++;
                    //foreach (var kvp2 in properties_dictionary)
                    //{
                    //    Console.WriteLine("looking for CATCAM 1");

                    //    Console.WriteLine(kvp2.Key);
                    //    Console.WriteLine(kvp2.Value);

                    //    if (kvp2.Key == "CATCAM" && kvp2.Value.ToString() == "1") // 
                    //    {
                    //        Console.WriteLine("CATCAM = 1 object");
                    //        string iconFilename2 = "TOPMAR05.png";
                    //        string iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", iconFilename2);
                    //        Bitmap iconBitmap2;

                    //        if (System.IO.File.Exists(iconPath2))
                    //        {
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected variable name
                    //        }
                    //        else
                    //        {
                    //            Console.WriteLine("USING DEFAULT IMAGE");
                    //            Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                    //            Console.WriteLine("Icon file not found, using default: " + iconPath2);
                    //            iconPath2 = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                    //            iconBitmap2 = new Bitmap(iconPath2); // Corrected to use iconPath2
                    //        }

                    //        GMarkerGoogle marker2 = new GMarkerGoogle(point, iconBitmap2);
                    //        marker2.Offset = new System.Drawing.Point(-3, -35);
                    //        overlay.Markers.Add(marker2);
                    //    }
                    //}
                }
            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }
        private void ProcessNOTMRKFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use
                    string Name_of_featureCollection = "NOTMRK";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }

                    //Console.WriteLine("Cleaned properties:");
                    //foreach (var kvp in cleanedProperties)
                    //{
                    //    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                    //}
                    List<string> relevant_attributes = new List<string> { "catnmk", "fnctnm", "addmrk" };
                    //   ,notmrk_catnmk72_fnctnm5_addmrk2 - 3.png,notmrk_catnmk72_fnctnm5_addmrk2 - 4.png,notmrk_catnmk100_fnctnm5_addmrk3.png,notmrk_catnmk100_fnctnm5_addmrk4.png,notmrk_catnmk6_fnctnm1_addmrk2 - 3.png,notmrk_catnmk6_fnctnm1_addmrk2 - 4.png,notmrk_catnmk6_fnctnm1_addmrk3 - 4.png,notmrk_catnmk8_fnctnm1_addmrk3 - 4.png,notmrk_catnmk11_fnctnm1_addmrk3.png,notmrk_catnmk11_fnctnm1_addmrk4.png,notmrk_catnmk38_fnctnm3_addmrk2.png,notmrk_catnmk39_fnctnm3_addmrk2.png,notmrk_catnmk52_fnctnm5_addmrk2.png,notmrk_catnmk55_fnctnm5_addmrk2.png,notmrk_catnmk55_fnctnm5_addmrk3.png,notmrk_catnmk55_fnctnm5_addmrk4.png,notmrk_catnmk72_fnctnm5_addmrk3.png,notmrk_catnmk72_fnctnm5_addmrk4.png,notmrk_catnmk95_fnctnm5_addmrk3.png,notmrk_catnmk95_fnctnm5_addmrk4.png,notmrk_catnmk97_fnctnm5_addmrk3.png,notmrk_catnmk97_fnctnm5_addmrk4.png,notmrk_catnmk1_fnctnm1_addmrk3.png,notmrk_catnmk1_fnctnm1_addmrk4.png,notmrk_catnmk6_fnctnm1_addmrk2.png,notmrk_catnmk6_fnctnm1_addmrk3.png,notmrk_catnmk6_fnctnm1_addmrk4.png,notmrk_catnmk8_fnctnm1_addmrk3.png,notmrk_catnmk8_fnctnm1_addmrk4.png,notmrk_catnmk100_fnctnm5.png,notmrk_catnmk101_fnctnm5.png,notmrk_catnmk102_fnctnm5.png,notmrk_catnmk223_fnctnm2.png,notmrk_catnmk235_fnctnm2.png,notmrk_catnmk10_fnctnm1.png,notmrk_catnmk11_fnctnm1.png,notmrk_catnmk12_fnctnm1.png,notmrk_catnmk13_fnctnm1.png,notmrk_catnmk23_fnctnm2.png,notmrk_catnmk31_fnctnm2.png,notmrk_catnmk32_fnctnm2.png,notmrk_catnmk33_fnctnm2.png,notmrk_catnmk34_fnctnm2.png,notmrk_catnmk35_fnctnm2.png,notmrk_catnmk36_fnctnm2.png,notmrk_catnmk37_fnctnm2.png,notmrk_catnmk38_fnctnm3.png,notmrk_catnmk39_fnctnm3.png,notmrk_catnmk41_fnctnm3.png,notmrk_catnmk42_fnctnm3.png,notmrk_catnmk43_fnctnm3.png,notmrk_catnmk44_fnctnm4.png,notmrk_catnmk45_fnctnm4.png,notmrk_catnmk46_fnctnm4.png,notmrk_catnmk47_fnctnm4.png,notmrk_catnmk48_fnctnm4.png,notmrk_catnmk49_fnctnm4.png,notmrk_catnmk50_fnctnm5.png,notmrk_catnmk51_fnctnm5.png,notmrk_catnmk52_fnctnm5.png,notmrk_catnmk53_fnctnm5.png,notmrk_catnmk54_fnctnm5.png,notmrk_catnmk55_fnctnm5.png,notmrk_catnmk58_fnctnm5.png,notmrk_catnmk71_fnctnm5.png,notmrk_catnmk72_fnctnm5.png,notmrk_catnmk73_fnctnm5.png,notmrk_catnmk74_fnctnm5.png,notmrk_catnmk75_fnctnm5.png,notmrk_catnmk76_fnctnm5.png,notmrk_catnmk77_fnctnm5.png,notmrk_catnmk78_fnctnm5.png,notmrk_catnmk79_fnctnm5.png,notmrk_catnmk80_fnctnm5.png,notmrk_catnmk81_fnctnm5.png,notmrk_catnmk90_fnctnm5.png,notmrk_catnmk95_fnctnm5.png,notmrk_catnmk96_fnctnm5.png,notmrk_catnmk97_fnctnm5.png,notmrk_catnmk98_fnctnm5.png,notmrk_catnmk99_fnctnm5.png,notmrk_catnmk1_fnctnm1.png,notmrk_catnmk5_fnctnm1.png,notmrk_catnmk6_fnctnm1.png,notmrk_catnmk8_fnctnm1.png,notmrk_fnctnm1.png,notmrk_fnctnm4.png,notmrk_fnctnm5.png,notmrk_.png

                    List<string> availableIcons = new List<string> {
                        "notmrk_catnmk55_fnctnm5_addmrk2-3-4.png", "notmrk_catnmk6_fnctnm1_addmrk2-3-4.png", "notmrk_catnmk55_fnctnm5_addmrk2-3.png", "notmrk_catnmk55_fnctnm5_addmrk2-4.png", "notmrk_catnmk72_fnctnm5_addmrk2-3.png", "notmrk_catnmk72_fnctnm5_addmrk2-4.png", "notmrk_catnmk100_fnctnm5_addmrk3.png", "notmrk_catnmk100_fnctnm5_addmrk4.png", "notmrk_catnmk6_fnctnm1_addmrk2-3.png", "notmrk_catnmk6_fnctnm1_addmrk2-4.png", "notmrk_catnmk6_fnctnm1_addmrk3-4.png", "notmrk_catnmk8_fnctnm1_addmrk3-4.png", "notmrk_catnmk11_fnctnm1_addmrk3.png", "notmrk_catnmk11_fnctnm1_addmrk4.png", "notmrk_catnmk38_fnctnm3_addmrk2.png", "notmrk_catnmk39_fnctnm3_addmrk2.png", "notmrk_catnmk52_fnctnm5_addmrk2.png", "notmrk_catnmk55_fnctnm5_addmrk2.png", "notmrk_catnmk55_fnctnm5_addmrk3.png", "notmrk_catnmk55_fnctnm5_addmrk4.png", "notmrk_catnmk72_fnctnm5_addmrk3.png", "notmrk_catnmk72_fnctnm5_addmrk4.png", "notmrk_catnmk95_fnctnm5_addmrk3.png", "notmrk_catnmk95_fnctnm5_addmrk4.png", "notmrk_catnmk97_fnctnm5_addmrk3.png", "notmrk_catnmk97_fnctnm5_addmrk4.png", "notmrk_catnmk1_fnctnm1_addmrk3.png", "notmrk_catnmk1_fnctnm1_addmrk4.png", "notmrk_catnmk6_fnctnm1_addmrk2.png", "notmrk_catnmk6_fnctnm1_addmrk3.png", "notmrk_catnmk6_fnctnm1_addmrk4.png", "notmrk_catnmk8_fnctnm1_addmrk3.png", "notmrk_catnmk8_fnctnm1_addmrk4.png", "notmrk_catnmk100_fnctnm5.png", "notmrk_catnmk101_fnctnm5.png", "notmrk_catnmk102_fnctnm5.png", "notmrk_catnmk223_fnctnm2.png", "notmrk_catnmk235_fnctnm2.png", "notmrk_catnmk10_fnctnm1.png", "notmrk_catnmk11_fnctnm1.png", "notmrk_catnmk12_fnctnm1.png", "notmrk_catnmk13_fnctnm1.png", "notmrk_catnmk23_fnctnm2.png", "notmrk_catnmk31_fnctnm2.png", "notmrk_catnmk32_fnctnm2.png", "notmrk_catnmk33_fnctnm2.png", "notmrk_catnmk34_fnctnm2.png", "notmrk_catnmk35_fnctnm2.png", "notmrk_catnmk36_fnctnm2.png", "notmrk_catnmk37_fnctnm2.png", "notmrk_catnmk38_fnctnm3.png", "notmrk_catnmk39_fnctnm3.png", "notmrk_catnmk41_fnctnm3.png", "notmrk_catnmk42_fnctnm3.png", "notmrk_catnmk43_fnctnm3.png", "notmrk_catnmk44_fnctnm4.png", "notmrk_catnmk45_fnctnm4.png", "notmrk_catnmk46_fnctnm4.png", "notmrk_catnmk47_fnctnm4.png", "notmrk_catnmk48_fnctnm4.png", "notmrk_catnmk49_fnctnm4.png", "notmrk_catnmk50_fnctnm5.png", "notmrk_catnmk51_fnctnm5.png", "notmrk_catnmk52_fnctnm5.png", "notmrk_catnmk53_fnctnm5.png", "notmrk_catnmk54_fnctnm5.png", "notmrk_catnmk55_fnctnm5.png", "notmrk_catnmk58_fnctnm5.png", "notmrk_catnmk71_fnctnm5.png", "notmrk_catnmk72_fnctnm5.png", "notmrk_catnmk73_fnctnm5.png", "notmrk_catnmk74_fnctnm5.png", "notmrk_catnmk75_fnctnm5.png", "notmrk_catnmk76_fnctnm5.png", "notmrk_catnmk77_fnctnm5.png", "notmrk_catnmk78_fnctnm5.png", "notmrk_catnmk79_fnctnm5.png", "notmrk_catnmk80_fnctnm5.png", "notmrk_catnmk81_fnctnm5.png", "notmrk_catnmk90_fnctnm5.png", "notmrk_catnmk95_fnctnm5.png", "notmrk_catnmk96_fnctnm5.png", "notmrk_catnmk97_fnctnm5.png", "notmrk_catnmk98_fnctnm5.png", "notmrk_catnmk99_fnctnm5.png", "notmrk_catnmk1_fnctnm1.png", "notmrk_catnmk5_fnctnm1.png", "notmrk_catnmk6_fnctnm1.png", "notmrk_catnmk8_fnctnm1.png", "notmrk_fnctnm1.png", "notmrk_fnctnm4.png", "notmrk_fnctnm5.png", "notmrk_.png", "notmrk_catnmk58_fnctnm5_addmrk2-4.png", "notmrk_catnmk14_fnctnm1_addmrk2.png", "notmrk_catnmk24_fnctnm2.png", "notmrk_catnmk56_fnctnm5.png", "notmrk_catnmk2_fnctnm1.png"
                    };
                    nameOfMarkerType = nameOfMarkerType.ToLower();
                    // string iconFilename = GetIconForFeature(cleanedProperties, nameOfMarkerType);//so here properties is a c# dictionary with keys and values
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);


                    //List<string> relevant_attributes = new List<string> { "BOYSHP", "COLOUR", "COLPAT", "CATSPM" };

                    ////      Console.WriteLine("HERE WE ASSIGN THE iconFilename, that is to say, we find the name of the png");
                    //nameOfMarkerType = nameOfMarkerType.ToUpper();
                    ////PASS nameOfMarkerType as upper case, relevant_attributes as upper case
                    //string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                    //                                                                                                                            // string iconFilename = GetIconForFeature(cleanedProperties, nameOfMarkerType);//so here properties is a c# dictionary with keys and values


                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(cleanedProperties, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                }
            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }
        private void ProcessBCNLATFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
              //  Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "BCNLAT";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }

                    List<string> relevant_attributes = new List<string> { "BCNSHP", "COLOUR", "CONVIS", "CATLAM", "COLPAT", "CATLAM" };


                    List<string> availableIcons = new List<string> {
                        "BCNLAT_COLOUR3-4-3_BCNSHP1.png", "BCNLAT_COLOUR3-4-3_BCNSHP2.png", "BCNLAT_COLOUR3-4-3_BCNSHP3.png", "BCNLAT_COLOUR3-4-3_BCNSHP4.png", "BCNLAT_COLOUR3-4-3_BCNSHP5.png", "BCNLAT_COLOUR3-4-3_BCNSHP7.png", "BCNLAT_COLOUR4-3-4_BCNSHP1.png", "BCNLAT_COLOUR4-3-4_BCNSHP2.png", "BCNLAT_COLOUR4-3-4_BCNSHP3.png", "BCNLAT_COLOUR4-3-4_BCNSHP4.png", "BCNLAT_COLOUR4-3-4_BCNSHP5.png", "BCNLAT_COLOUR4-3-4_BCNSHP6.png", "BCNLAT_COLOUR4-3-4_BCNSHP7.png", "BCNLAT_BCNSHP6_CONVIS1.png", "BCNLAT_COLOUR3_BCNSHP1.png", "BCNLAT_COLOUR3_BCNSHP2.png", "BCNLAT_COLOUR3_BCNSHP3.png", "BCNLAT_COLOUR3_BCNSHP4.png", "BCNLAT_COLOUR3_BCNSHP5.png", "BCNLAT_COLOUR3_BCNSHP7.png", "BCNLAT_COLOUR4_BCNSHP1.png", "BCNLAT_COLOUR4_BCNSHP2.png", "BCNLAT_COLOUR4_BCNSHP3.png", "BCNLAT_COLOUR4_BCNSHP4.png", "BCNLAT_COLOUR4_BCNSHP5.png", "BCNLAT_COLOUR4_BCNSHP7.png", "BCNLAT_COLOUR3-4-3.png", "BCNLAT_COLOUR4-3-4.png", "BCNLAT_BCNSHP6.png", "BCNLAT_COLOUR3.png", "BCNLAT_COLOUR4.png", "BCNLAT_.png", "BCNLAT_BCNSHP5_COLOUR4.png", "BCNLAT_CATLAM11.png", "BCNLAT_CATLAM12.png", "BCNLAT_CATLAM13.png", "BCNLAT_CATLAM14.png", "BCNLAT_CATLAM15.png", "BCNLAT_CATLAM16.png", "BCNLAT_CATLAM17.png", "BCNLAT_CATLAM18.png", "BCNLAT_CATLAM19.png", "BCNLAT_CATLAM20.png", "BCNLAT_CATLAM21.png", "BCNLAT_CATLAM22.png", "BCNLAT_CATLAM5.png", "BCNLAT_CATLAM6.png", "BCNLAT_CATLAM9.png", "BCNLAT_BCNSHP3_CATLAM4_COLPAT1_COLOUR3-4-3.png", "BCNLAT_BCNSHP3_CATLAM1_COLPAT1_COLOUR1-4.png", "BCNLAT_BCNSHP3_CATLAM1_COLPAT1_COLOUR4-1.png", "BCNLAT_BCNSHP3_CATLAM2_COLPAT1_COLOUR1-3.png", "BCNLAT_BCNSHP3_CATLAM2_COLPAT1_COLOUR3-1.png", "BCNLAT_BCNSHP5_CATLAM1_COLOUR4.png", "BCNLAT_BCNSHP5_CATLAM2_COLOUR3.png", "BCNLAT_BCNSHP1_COLOUR1-4-1.png", "BCNLAT_BCNSHP1_COLOUR1-3.png", "BCNLAT_BCNSHP1_COLOUR1-4.png", "BCNLAT_BCNSHP1_COLOUR3.png", "BCNLAT_BCNSHP1_COLOUR4.png", "BCNLAT_BCNSHP2_CATLAM1.png", "BCNLAT_BCNSHP2_CATLAM2.png", "BCNLAT_BCNSHP3_COLOUR3.png", "BCNLAT_BCNSHP3_COLOUR4.png", "BCNLAT_BCNSHP4_COLOUR3.png", "BCNLAT_BCNSHP4_COLOUR4.png", "BCNLAT_BCNSHP5_CATLAM1.png", "BCNLAT_BCNSHP5_CATLAM2.png", "BCNLAT_BCNSHP7_COLOUR1.png", "BCNLAT_BCNSHP7_COLOUR3.png", "BCNLAT_BCNSHP7_COLOUR4.png", "BCNLAT_COLOUR3-1.png", "BCNLAT_COLOUR4-1.png", "BCNLAT_BCNSHP1.png", "BCNLAT_BCNSHP3.png", "BCNLAT_BCNSHP4.png", "BCNLAT_BCNSHP5.png", "BCNLAT_BCNSHP7.png", "BCNLAT_BCNSHP5_COLOUR1-3.png", "BCNLAT_BCNSHP5_COLOUR1-4.png", "BCNLAT_BCNSHP5_COLOUR3.png", "BCNLAT_BCNSHP1_COLOUR3-4.png", "BCNLAT_BCNSHP1_COLOUR4-3.png", "BCNLAT_COLPAT1_COLOUR3-4.png", "BCNLAT_COLPAT1_COLOUR4-3.png"
                    };
                    //nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    //string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);


                    //  string iconFilename = GetIconForFeature(properties_dictionary, nameOfMarkerType);//so here properties is a c# dictionary with keys and values

                    //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the default icon
                    }

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                }
            }

            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }
        private void ProcessLNDMRKFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //  Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "LNDMRK";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }
             //       Console.WriteLine("below are the properties");//PRINTING TO CONSOLE
                    foreach (var thing in properties_dictionary)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
              //          Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE

                 //   Console.WriteLine("below are the cleaned properties");//PRINTING TO CONSOLE

                    foreach (var thing in cleanedProperties)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
               //         Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE
                    List<string> relevant_attributes = new List<string> { "CATLMK", "FUNCTN", "CONVIS", "COLPAT", "COLOUR" };


                    List<string> availableIcons = new List<string> {
                        "LNDMRK_CATLMK17_FUNCTN33_CONVIS1.png","LNDMRK_CATLMK15_FUNCTN20_CONVIS1.png", "LNDMRK_CATLMK15_FUNCTN21_CONVIS1.png", "LNDMRK_CATLMK17_FUNCTN20_CONVIS1.png", "LNDMRK_CATLMK17_FUNCTN21_CONVIS1.png","LNDMRK_CATLMK20_FUNCTN20_CONVIS1.png", "LNDMRK_CATLMK20_FUNCTN21_CONVIS1.png", "LNDMRK_CATLMK20_FUNCTN26_CONVIS1.png", "LNDMRK_CATLMK20_FUNCTN27_CONVIS1.png", "LNDMRK_CATLMK15_FUNCTN20.png", "LNDMRK_CATLMK17_FUNCTN20.png", "LNDMRK_CATLMK17_FUNCTN33.png", "LNDMRK_CATLMK20_FUNCTN20.png", "LNDMRK_CATLMK10_CONVIS1.png", "LNDMRK_CATLMK12_CONVIS1.png", "LNDMRK_CATLMK13_CONVIS1.png", "LNDMRK_CATLMK15_CONVIS1.png", "LNDMRK_CATLMK16_CONVIS1.png", "LNDMRK_CATLMK17_CONVIS1.png", "LNDMRK_CATLMK18_CONVIS1.png", "LNDMRK_CATLMK19_CONVIS1.png", "LNDMRK_CATLMK20_CONVIS1.png", "LNDMRK_CATLMK1_CONVIS1.png", "LNDMRK_CATLMK3_CONVIS1.png", "LNDMRK_CATLMK4_CONVIS1.png", "LNDMRK_CATLMK5_CONVIS1.png", "LNDMRK_CATLMK6_CONVIS1.png", "LNDMRK_CATLMK7_CONVIS1.png", "LNDMRK_CATLMK8_CONVIS1.png", "LNDMRK_CATLMK9_CONVIS1.png", "LNDMRK_CATLMK15.png", "LNDMRK_CATLMK16.png", "LNDMRK_CATLMK17.png", "LNDMRK_CATLMK3.png", "LNDMRK_CATLMK6.png", "LNDMRK_CATLMK7.png", "LNDMRK_CONVIS1.png", "LNDMRK_.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR1-2-1.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR1-3-1.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR2-1-2.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR2-1-7.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR2-3-2.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR1-2.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR1-4.png", "LNDMRK_CATLMK17_COLPAT1_FUNCTN33_COLOUR2-1.png", "LNDMRK_CATLMK17_COLPAT3_FUNCTN33_COLOUR2-1.png", "LNDMRK_CATLMK17_COLPAT4_FUNCTN33_COLOUR3-1.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR3-1-3-1.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR1-6-1.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR1-7-1.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR1-2.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR1-3.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR2-1.png", "LNDMRK_CATLMK17_COLPAT1_COLOUR3-1.png", "LNDMRK_CATLMK17_COLPAT2_COLOUR1-2.png", "LNDMRK_CATLMK17_COLPAT2_COLOUR1-3.png", "LNDMRK_CATLMK17_COLPAT2_COLOUR2-1.png", "LNDMRK_CATLMK17_COLPAT3_COLOUR1-2.png", "LNDMRK_CATLMK17_FUNCTN17_COLOUR7.png", "LNDMRK_CATLMK17_FUNCTN33_COLOUR1.png", "LNDMRK_CATLMK17_FUNCTN33_COLOUR3.png", "LNDMRK_CATLMK17_FUNCTN33_COLOUR4.png", "LNDMRK_CATLMK17_FUNCTN33_COLOUR6.png", "LNDMRK_CATLMK17_FUNCTN33_COLOUR7.png", "LNDMRK_CATLMK17_FUNCTN33_COLOUR8.png", "LNDMRK_CATLMK17_FUNCTN31-33.png", "LNDMRK_CATLMK17_FUNCTN31.png", "LNDMRK_CATLMK17_COLOUR1.png", "LNDMRK_CATLMK10.png", "LNDMRK_CATLMK12.png", "LNDMRK_CATLMK13.png", "LNDMRK_CATLMK18.png", "LNDMRK_CATLMK19.png", "LNDMRK_CATLMK20.png", "LNDMRK_CATLMK1.png", "LNDMRK_CATLMK4.png", "LNDMRK_CATLMK5.png", "LNDMRK_CATLMK8.png", "LNDMRK_CATLMK9.png"
                        };
                    //nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));




                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                        iconBitmap = new Bitmap(iconBitmap, new Size(30, 40)); // Resize the default icon

                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        iconBitmap = new Bitmap(iconBitmap, new Size(5, 5)); // Resize the default icon
                    }
                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                }
            }




            Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
        }
        private void ProcessBCNSPPFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //  Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "BCNSPP";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }
                    //         Console.WriteLine("below are the properties");//PRINTING TO CONSOLE
                    foreach (var thing in properties_dictionary)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
                     //            Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE

                    //      Console.WriteLine("below are the cleaned properties");//PRINTING TO CONSOLE

                    foreach (var thing in cleanedProperties)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
                     //         Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE
                    List<string> relevant_attributes = new List<string> { "BCNSHP", "CATSPM", "COLOUR", "COLPAT", "CATLAM", "CONVIS", };


                    List<string> availableIcons = new List<string> {
                        "BCNSPP_BCNSHP6_CONVIS1.png", "BCNSPP_CATSPM18.png", "BCNSPP_CATSPM44.png", "BCNSPP_CATSPM52.png", "BCNSPP_BCNSHP1.png", "BCNSPP_BCNSHP3.png", "BCNSPP_BCNSHP4.png", "BCNSPP_BCNSHP5.png", "BCNSPP_BCNSHP6.png", "BCNSPP_BCNSHP7.png", "BCNSPP_.png", "BCNSPP_BCNSHP3_COLPAT1_COLOUR2-1-2.png", "BCNSPP_BCNSHP3_COLOUR4-1_COLPAT2.png", "BCNSPP_BCNSHP5_CATLAM1_COLOUR4.png", "BCNSPP_BCNSHP3_COLOUR4-1.png", "BCNSPP_BCNSHP4_COLOUR1-2.png", "BCNSPP_BCNSHP4_COLOUR2-1.png", "BCNSPP_CATSPM18_COLOUR6.png", "BCNSPP_BCNSHP1_COLOUR1.png", "BCNSPP_BCNSHP1_COLOUR3.png", "BCNSPP_BCNSHP1_COLOUR4.png", "BCNSPP_BCNSHP1_COLOUR6.png", "BCNSPP_BCNSHP3_COLOUR1.png", "BCNSPP_BCNSHP3_COLOUR2.png", "BCNSPP_BCNSHP3_COLOUR4.png", "BCNSPP_BCNSHP3_COLOUR6.png", "BCNSPP_BCNSHP4_COLOUR1.png", "BCNSPP_BCNSHP4_COLOUR2.png", "BCNSPP_BCNSHP4_COLOUR3.png", "BCNSPP_BCNSHP4_COLOUR8.png", "BCNSPP_BCNSHP5_COLOUR1.png", "BCNSPP_BCNSHP5_COLOUR6.png", "BCNSPP_BCNSHP7_COLOUR6.png", "BCNSPP_COLOUR3-4-3.png", "BCNSPP_COLOUR4-3-4.png", "BCNSPP_COLOUR3-1.png", "BCNSPP_COLOUR4-1.png", "BCNSPP_COLOUR11.png", "BCNSPP_COLOUR1.png", "BCNSPP_COLOUR2.png", "BCNSPP_COLOUR3.png", "BCNSPP_COLOUR4.png", "BCNSPP_COLOUR6.png"                        };
                    //nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));


                    //    Console.WriteLine("We chose this icon");//PRINTING TO CONSOLE
                    //    Console.WriteLine(iconFilename);//PRINTING TO CONSOLE


                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                     //   iconBitmap = new Bitmap(iconBitmap, new Size(30, 50)); // Resize the default icon

                    }
                    else
                    {
                        //            Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                     //   iconBitmap = new Bitmap(iconBitmap, new Size(5, 5)); // Resize the default icon
                    }
                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Tag = properties_dictionary;
                    overlay.Markers.Add(marker);
                    pointCount++;
                }
            }
        }

        private void ProcessDAYMARFeatures(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //  Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use


                    string Name_of_featureCollection = "DAYMAR";
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }
                  //  Console.WriteLine("below are the properties");//PRINTING TO CONSOLE
                    foreach (var thing in properties_dictionary)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
                     //           Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE

                  //  Console.WriteLine("below are the cleaned properties");//PRINTING TO CONSOLE

                    foreach (var thing in cleanedProperties)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
                     //            Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE
                    List<string> relevant_attributes = new List<string> { "TOPSHP", "COLOUR", "COLPAT", "CATLAM", "CONVIS", };


                    List<string> availableIcons = new List<string> {
                        "DAYMAR_TOPSHP19.png", "DAYMAR_TOPSHP20.png", "DAYMAR_TOPSHP21.png", "DAYMAR_TOPSHP24.png", "DAYMAR_TOPSHP25.png", "DAYMAR_.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-1-2-1.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-1-3-1.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-1-6-1.png", "DAYMAR_TOPSHP21_COLPAT5-2_COLOUR3-1-3-6.png", "DAYMAR_TOPSHP12_COLPAT4_COLOUR3-1-1-3.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-3-1.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-4-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-1-3-1.png", "DAYMAR_TOPSHP19_COLPAT6-1_COLOUR4-3-4.png", "DAYMAR_TOPSHP19_COLPAT6-1_COLOUR4-4-3.png", "DAYMAR_TOPSHP21_COLPAT5-2_COLOUR3-1-3.png", "DAYMAR_TOPSHP24_COLPAT2_COLOUR11-2-11.png", "DAYMAR_TOPSHP24_COLPAT6-1_COLOUR3-3-6.png", "DAYMAR_TOPSHP33_COLPAT6-5_COLOUR1-1-3.png", "DAYMAR_TOPSHP19_COLPAT2_COLOUR1-11-1.png", "DAYMAR_TOPSHP19_COLPAT6-4_COLOUR11-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-3-1.png", "DAYMAR_TOPSHP19_COLPAT1_COLOUR11-11.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR3-4-3.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR4-4-6.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-2-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-3-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR2-1-2.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-1-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-2-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-4-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR4-3-4.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR6-2-6.png", "DAYMAR_TOPSHP33_COLPAT6_COLOUR1-1-3.png", "DAYMAR_TOPSHP33_COLPAT6_COLOUR1-3-1.png", "DAYMAR_TOPSHP12_COLPAT2_COLOUR1-11.png", "DAYMAR_TOPSHP12_COLPAT2_COLOUR11-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-11.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR11-1.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR1-11.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR11-1.png", "DAYMAR_TOPSHP20_COLPAT1_COLOUR11-1.png", "DAYMAR_TOPSHP20_COLPAT6_COLOUR11-1.png", "DAYMAR_TOPSHP22_COLPAT2_COLOUR1-11.png", "DAYMAR_TOPSHP23_COLPAT2_COLOUR1-11.png", "DAYMAR_TOPSHP24_COLPAT2_COLOUR11-2.png", "DAYMAR_TOPSHP25_COLPAT2_COLOUR11-2.png", "DAYMAR_TOPSHP12_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP12_COLPAT2_COLOUR1-3.png", "DAYMAR_TOPSHP12_COLPAT4_COLOUR3-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-3.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR6-6.png", "DAYMAR_TOPSHP19_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP19_COLPAT2_COLOUR3-1.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR1-2.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR1-3.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR2-2.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR4-4.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR6-6.png", "DAYMAR_TOPSHP20_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP20_COLPAT2_COLOUR1-3.png", "DAYMAR_TOPSHP20_COLPAT2_COLOUR3-1.png", "DAYMAR_TOPSHP20_COLPAT6_COLOUR3-1.png", "DAYMAR_TOPSHP21_COLPAT1_COLOUR1-3.png", "DAYMAR_TOPSHP21_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-2.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-4.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-2.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-4.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR4-2.png", "DAYMAR_TOPSHP21_COLPAT6_COLOUR3-1.png", "DAYMAR_TOPSHP22_COLPAT2_COLOUR3-2.png", "DAYMAR_TOPSHP23_COLPAT2_COLOUR3-2.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR3-1.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR3-3.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR3-6.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR4-4.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR6-6.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR11.png", "DAYMAR_TOPSHP3_COLPAT6_COLOUR4-4.png", "DAYMAR_TOPSHP19_COLOUR4-1-2.png", "DAYMAR_TOPSHP10_COLOUR3-4.png", "DAYMAR_TOPSHP19_COLOUR3-3.png", "DAYMAR_TOPSHP19_COLOUR4-4.png", "DAYMAR_TOPSHP24_COLOUR3-3.png", "DAYMAR_TOPSHP12_COLOUR11.png", "DAYMAR_TOPSHP19_COLOUR11.png", "DAYMAR_TOPSHP24_COLOUR11.png", "DAYMAR_TOPSHP26_COLOUR11.png", "DAYMAR_TOPSHP12_COLOUR1.png", "DAYMAR_TOPSHP19_COLOUR1.png", "DAYMAR_TOPSHP19_COLOUR2.png", "DAYMAR_TOPSHP19_COLOUR3.png", "DAYMAR_TOPSHP19_COLOUR4.png", "DAYMAR_TOPSHP19_COLOUR6.png", "DAYMAR_TOPSHP20_COLOUR1.png", "DAYMAR_TOPSHP20_COLOUR2.png", "DAYMAR_TOPSHP21_COLOUR1.png", "DAYMAR_TOPSHP21_COLOUR3.png", "DAYMAR_TOPSHP24_COLOUR3.png", "DAYMAR_TOPSHP24_COLOUR4.png", "DAYMAR_TOPSHP24_COLOUR6.png", "DAYMAR_TOPSHP25_COLOUR3.png", "DAYMAR_TOPSHP1_COLOUR3.png", "DAYMAR_TOPSHP3_COLOUR3.png", "DAYMAR_TOPSHP10.png", "DAYMAR_TOPSHP11.png", "DAYMAR_TOPSHP12.png", "DAYMAR_TOPSHP13.png", "DAYMAR_TOPSHP14.png", "DAYMAR_TOPSHP15.png", "DAYMAR_TOPSHP16.png", "DAYMAR_TOPSHP17.png", "DAYMAR_TOPSHP18.png", "DAYMAR_TOPSHP22.png", "DAYMAR_TOPSHP23.png", "DAYMAR_TOPSHP26.png", "DAYMAR_TOPSHP27.png", "DAYMAR_TOPSHP28.png", "DAYMAR_TOPSHP29.png", "DAYMAR_TOPSHP30.png", "DAYMAR_TOPSHP31.png", "DAYMAR_TOPSHP32.png", "DAYMAR_TOPSHP33.png", "DAYMAR_TOPSHP1.png", "DAYMAR_TOPSHP2.png", "DAYMAR_TOPSHP3.png", "DAYMAR_TOPSHP4.png", "DAYMAR_TOPSHP5.png", "DAYMAR_TOPSHP6.png", "DAYMAR_TOPSHP7.png", "DAYMAR_TOPSHP8.png", "DAYMAR_TOPSHP9.png", "DAYMAR_TOPSHP12_COLOUR1-4_COLPAT6.png", "DAYMAR_TOPSHP7_COLOUR6.png", "DAYMAR_TOPSHP8_COLOUR6.png", "DAYMAR_TOPSHP2_COLOUR3.png", "DAYMAR_TOPSHP1_COLOUR4.png", "DAYMAR_TOPSHP20_COLOUR1-2_COLPAT4.png", "DAYMAR_TOPSHP7_COLOUR2.png"
                    };//nameOfMarkerType = nameOfMarkerType.ToLower();
                    string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));


                    //          Console.WriteLine("We chose this icon");//PRINTING TO CONSOLE
                    //           Console.WriteLine(iconFilename);//PRINTING TO CONSOLE


                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                        //   iconBitmap = new Bitmap(iconBitmap, new Size(30, 50)); // Resize the default icon

                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties_dictionary, Formatting.Indented));
                        Console.WriteLine("Icon file not found, using default: " + iconPath);
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png"));
                        //   iconBitmap = new Bitmap(iconBitmap, new Size(5, 5)); // Resize the default icon
                    }
                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Offset = new System.Drawing.Point(-8,-32);
                    marker.Tag = properties_dictionary;
                    
                    overlay.Markers.Add(marker);
                    pointCount++;

                    //GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    //marker.Offset = new System.Drawing.Point(0, -8);
                }
            }
        }

        private void Process_unhandled_Features(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)

        {
            int polygonCount = 0;
            int pointCount = 0;
            foreach (var feature in featureCollection.Features)
            {
                //  Console.WriteLine("ANOTHER FEATURE IS BEING READ");
                if (feature.Geometry is GeoJSON.Net.Geometry.Point)
                {
                    var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                    PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);//PointLatLng is a format that GMap can use

                    string Name_of_featureCollection =nameOfMarkerType;
                    var properties_dictionary = new Dictionary<string, object>();
                    properties_dictionary[nameOfMarkerType] = Name_of_featureCollection; // Add first
                    // Then add other properties
                    foreach (var kvp in feature.Properties)
                    {
                        properties_dictionary[kvp.Key] = kvp.Value;
                    }
                    var cleanedProperties = new Dictionary<string, object>();
                    foreach (var kvp in feature.Properties)
                    {
                        string cleanedKey = kvp.Key;
                        string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value

                        cleanedProperties[cleanedKey] = cleanedValue;
                    }
                    //  Console.WriteLine("below are the properties");//PRINTING TO CONSOLE
                    foreach (var thing in properties_dictionary)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
                     //           Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE

                    //  Console.WriteLine("below are the cleaned properties");//PRINTING TO CONSOLE

                    foreach (var thing in cleanedProperties)//PRINTING TO CONSOLE
                    {//PRINTING TO CONSOLE
                     //            Console.WriteLine(thing); //PRINTING TO CONSOLE
                    }//PRINTING TO CONSOLE
                  //  List<string> relevant_attributes = new List<string> { "TOPSHP", "COLOUR", "COLPAT", "CATLAM", "CONVIS", };


               //     List<string> availableIcons = new List<string> {
                 //       "DAYMAR_TOPSHP19.png", "DAYMAR_TOPSHP20.png", "DAYMAR_TOPSHP21.png", "DAYMAR_TOPSHP24.png", "DAYMAR_TOPSHP25.png", "DAYMAR_.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-1-2-1.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-1-3-1.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-1-6-1.png", "DAYMAR_TOPSHP21_COLPAT5-2_COLOUR3-1-3-6.png", "DAYMAR_TOPSHP12_COLPAT4_COLOUR3-1-1-3.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-3-1.png", "DAYMAR_TOPSHP12_COLPAT6-4_COLOUR1-4-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-1-3-1.png", "DAYMAR_TOPSHP19_COLPAT6-1_COLOUR4-3-4.png", "DAYMAR_TOPSHP19_COLPAT6-1_COLOUR4-4-3.png", "DAYMAR_TOPSHP21_COLPAT5-2_COLOUR3-1-3.png", "DAYMAR_TOPSHP24_COLPAT2_COLOUR11-2-11.png", "DAYMAR_TOPSHP24_COLPAT6-1_COLOUR3-3-6.png", "DAYMAR_TOPSHP33_COLPAT6-5_COLOUR1-1-3.png", "DAYMAR_TOPSHP19_COLPAT2_COLOUR1-11-1.png", "DAYMAR_TOPSHP19_COLPAT6-4_COLOUR11-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-3-1.png", "DAYMAR_TOPSHP19_COLPAT1_COLOUR11-11.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR3-4-3.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR4-4-6.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-2-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-3-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR2-1-2.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-1-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-2-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-4-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR4-3-4.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR6-2-6.png", "DAYMAR_TOPSHP33_COLPAT6_COLOUR1-1-3.png", "DAYMAR_TOPSHP33_COLPAT6_COLOUR1-3-1.png", "DAYMAR_TOPSHP12_COLPAT2_COLOUR1-11.png", "DAYMAR_TOPSHP12_COLPAT2_COLOUR11-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-11.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR11-1.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR1-11.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR11-1.png", "DAYMAR_TOPSHP20_COLPAT1_COLOUR11-1.png", "DAYMAR_TOPSHP20_COLPAT6_COLOUR11-1.png", "DAYMAR_TOPSHP22_COLPAT2_COLOUR1-11.png", "DAYMAR_TOPSHP23_COLPAT2_COLOUR1-11.png", "DAYMAR_TOPSHP24_COLPAT2_COLOUR11-2.png", "DAYMAR_TOPSHP25_COLPAT2_COLOUR11-2.png", "DAYMAR_TOPSHP12_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP12_COLPAT2_COLOUR1-3.png", "DAYMAR_TOPSHP12_COLPAT4_COLOUR3-1.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR1-3.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR6-6.png", "DAYMAR_TOPSHP19_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP19_COLPAT2_COLOUR3-1.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR1-2.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR1-3.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR2-2.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR4-4.png", "DAYMAR_TOPSHP19_COLPAT6_COLOUR6-6.png", "DAYMAR_TOPSHP20_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP20_COLPAT2_COLOUR1-3.png", "DAYMAR_TOPSHP20_COLPAT2_COLOUR3-1.png", "DAYMAR_TOPSHP20_COLPAT6_COLOUR3-1.png", "DAYMAR_TOPSHP21_COLPAT1_COLOUR1-3.png", "DAYMAR_TOPSHP21_COLPAT1_COLOUR3-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-2.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-3.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR1-4.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-1.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-2.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR3-4.png", "DAYMAR_TOPSHP21_COLPAT2_COLOUR4-2.png", "DAYMAR_TOPSHP21_COLPAT6_COLOUR3-1.png", "DAYMAR_TOPSHP22_COLPAT2_COLOUR3-2.png", "DAYMAR_TOPSHP23_COLPAT2_COLOUR3-2.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR3-1.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR3-3.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR3-6.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR4-4.png", "DAYMAR_TOPSHP24_COLPAT6_COLOUR6-6.png", "DAYMAR_TOPSHP12_COLPAT6_COLOUR11.png", "DAYMAR_TOPSHP3_COLPAT6_COLOUR4-4.png", "DAYMAR_TOPSHP19_COLOUR4-1-2.png", "DAYMAR_TOPSHP10_COLOUR3-4.png", "DAYMAR_TOPSHP19_COLOUR3-3.png", "DAYMAR_TOPSHP19_COLOUR4-4.png", "DAYMAR_TOPSHP24_COLOUR3-3.png", "DAYMAR_TOPSHP12_COLOUR11.png", "DAYMAR_TOPSHP19_COLOUR11.png", "DAYMAR_TOPSHP24_COLOUR11.png", "DAYMAR_TOPSHP26_COLOUR11.png", "DAYMAR_TOPSHP12_COLOUR1.png", "DAYMAR_TOPSHP19_COLOUR1.png", "DAYMAR_TOPSHP19_COLOUR2.png", "DAYMAR_TOPSHP19_COLOUR3.png", "DAYMAR_TOPSHP19_COLOUR4.png", "DAYMAR_TOPSHP19_COLOUR6.png", "DAYMAR_TOPSHP20_COLOUR1.png", "DAYMAR_TOPSHP20_COLOUR2.png", "DAYMAR_TOPSHP21_COLOUR1.png", "DAYMAR_TOPSHP21_COLOUR3.png", "DAYMAR_TOPSHP24_COLOUR3.png", "DAYMAR_TOPSHP24_COLOUR4.png", "DAYMAR_TOPSHP24_COLOUR6.png", "DAYMAR_TOPSHP25_COLOUR3.png", "DAYMAR_TOPSHP1_COLOUR3.png", "DAYMAR_TOPSHP3_COLOUR3.png", "DAYMAR_TOPSHP10.png", "DAYMAR_TOPSHP11.png", "DAYMAR_TOPSHP12.png", "DAYMAR_TOPSHP13.png", "DAYMAR_TOPSHP14.png", "DAYMAR_TOPSHP15.png", "DAYMAR_TOPSHP16.png", "DAYMAR_TOPSHP17.png", "DAYMAR_TOPSHP18.png", "DAYMAR_TOPSHP22.png", "DAYMAR_TOPSHP23.png", "DAYMAR_TOPSHP26.png", "DAYMAR_TOPSHP27.png", "DAYMAR_TOPSHP28.png", "DAYMAR_TOPSHP29.png", "DAYMAR_TOPSHP30.png", "DAYMAR_TOPSHP31.png", "DAYMAR_TOPSHP32.png", "DAYMAR_TOPSHP33.png", "DAYMAR_TOPSHP1.png", "DAYMAR_TOPSHP2.png", "DAYMAR_TOPSHP3.png", "DAYMAR_TOPSHP4.png", "DAYMAR_TOPSHP5.png", "DAYMAR_TOPSHP6.png", "DAYMAR_TOPSHP7.png", "DAYMAR_TOPSHP8.png", "DAYMAR_TOPSHP9.png", "DAYMAR_TOPSHP12_COLOUR1-4_COLPAT6.png", "DAYMAR_TOPSHP7_COLOUR6.png", "DAYMAR_TOPSHP8_COLOUR6.png", "DAYMAR_TOPSHP2_COLOUR3.png", "DAYMAR_TOPSHP1_COLOUR4.png", "DAYMAR_TOPSHP20_COLOUR1-2_COLPAT4.png", "DAYMAR_TOPSHP7_COLOUR2.png"
                  //  };//nameOfMarkerType = nameOfMarkerType.ToLower();
            //        string iconFilename = GetIconForFeature_multiple(cleanedProperties, nameOfMarkerType, availableIcons, relevant_attributes); // Retrieve the icon filename
                                                                                                                                                //                                                                                                                            //Console.WriteLine("Processing feature with properties: " + JsonConvert.SerializeObject(properties, Formatting.Indented));
                   

                    //          Console.WriteLine("We chose this icon");//PRINTING TO CONSOLE
                    //           Console.WriteLine(iconFilename);//PRINTING TO CONSOLE


                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", "default.png");
                    Bitmap iconBitmap;
                    iconBitmap = new Bitmap(iconPath);     
                    iconBitmap = new Bitmap(iconBitmap, new Size(20, 20)); // Resize the default icon

                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    //marker.Offset = new System.Drawing.Point(-8, -32);
                    marker.Tag = properties_dictionary;
                    Console.WriteLine("added a point to the unhandled layer");
                    overlay.Markers.Add(marker);
                    pointCount++;
                    //GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    //marker.Offset = new System.Drawing.Point(0, -8);
                }
                else if (feature.Geometry is GeoJSON.Net.Geometry.Polygon)
                {
                    //not a point
                    string Name_of_featureCollection = nameOfMarkerType;
                    Color color = Color.Red;
                    GeoJSON.Net.Geometry.Polygon polygon = feature.Geometry as GeoJSON.Net.Geometry.Polygon;
                    if (polygon != null)
                    {
                        List<PointLatLng> points = polygon.Coordinates[0].Coordinates.Select(coord => new PointLatLng(coord.Latitude, coord.Longitude)).ToList();
                        GMapPolygon mapPolygon = new GMapPolygon(points, "polygon" + polygonCount)
                       
                        { 
                            Stroke = new Pen(color, 1),
                            Fill = new SolidBrush(Color.FromArgb(0, color))
                        };
                        var properties_dictionary = new Dictionary<string, object>();
                        properties_dictionary["Name"] = Name_of_featureCollection; // Add first
                                                                                   // Then add other properties
                        foreach (var kvp in feature.Properties)
                        {
                            properties_dictionary[kvp.Key] = kvp.Value;
                        }
                        mapPolygon.Tag = properties_dictionary;
                        overlay.Polygons.Add(mapPolygon);
                        Console.WriteLine("added a polygon to the unhandled layer");

                        polygonCount++;
                    }
                }
            }
        }



        private static void ProcessLIGHTSFeatures2(FeatureCollection featureCollection, GMapOverlay overlay, string nameOfMarkerType)
        {
            int pointCount = 0;
            try
            {
                foreach (var feature in featureCollection.Features)
                {
                    // Console.WriteLine("ANOTHER LIGHTS FEATURE IS BEING READ");

                    if (feature.Geometry is GeoJSON.Net.Geometry.Point)

                    {
                        string Name_of_featureCollection = "LIGHTS";
                        var properties_dictionary = new Dictionary<string, object>();
                        properties_dictionary["Name"] = Name_of_featureCollection; // Add first

                        // Then add other properties
                        foreach (var kvp in feature.Properties)
                        {
                            properties_dictionary[kvp.Key] = kvp.Value;
                        }                                                                                       //into a C# dictionary with keys and values (keys

                        // Add the name of the FeatureCollection to the dictionary 
                        var pointGeometry = (GeoJSON.Net.Geometry.Point)feature.Geometry;
                        PointLatLng point = new PointLatLng(pointGeometry.Coordinates.Latitude, pointGeometry.Coordinates.Longitude);

                        var cleanedProperties = new Dictionary<string, object>();
                        foreach (var kvp in feature.Properties)
                        {
                            string cleanedKey = kvp.Key;
                            string cleanedValue = CleanPropertyValue(kvp.Value.ToString()); // Clean the property value
                            cleanedProperties[cleanedKey] = cleanedValue;
                        }
                        string iconFilename = "placeholder";
                        // Console.WriteLine("Cleaned properties:");
                        Boolean has_COLOUR = false;
                        int RCID = 9999;//placeholder, Im pretty sure all objects need an RCID
                                        // Explicitly cast kvp.Value to a string
                        foreach (var kvp in cleanedProperties)
                        {


                            if (kvp.Key == "RCID")
                            {
                                int RCIDtemp; // Temporary variable to hold the parsed integer                           
                                              // Explicitly cast kvp.Value to a string
                                string stringValue = kvp.Value as string;
                                if (stringValue != null && int.TryParse(stringValue, out RCIDtemp))
                                {
                                    RCID = RCIDtemp;//Convoluted way of converting an 'object' to an string, idk if its possible to use up less lines doing this basic task
                                }
                            }
                            if (RCID == 26128)
                            {
   //                             Console.WriteLine($"We found it! ");
    //                            Console.WriteLine("Cleaned properties:");
                                foreach (var kvp2 in cleanedProperties)

                                {
          //                          Console.WriteLine($"{kvp2.Key}: {kvp2.Value}");
                                }

                            }
                        }
                        //can we assign a symbol already?
                        //Do we have an ORIENT value and can thus chose the LS(DASH,1,CHBLK) line?
                        foreach (var kvp in cleanedProperties)
                        {
                            //   Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                            if (kvp.Key == "CATLIT" & kvp.Value == "11") { iconFilename = "LIGHTS82.png)"; }
                            if (kvp.Key == "CATLIT" & kvp.Value == "8") { iconFilename = "LIGHTS82.png)"; }
                            if (kvp.Key == "CATLIT" & kvp.Value == "9") { iconFilename = "LIGHTS81.png))"; }

                            if (kvp.Key == "ORIENT") { Boolean has_ORIENT = true; }
                        }
                        //get values COLOUR LITCHR SECTR1 SECTR2
                        string LIGHT_COLOUR = "99";//assigned the value 99 just so I can print it to the console
                        string LIGHT_COLOUR_ARC; //this is for if this is a sector light.
                        foreach (var kvp in cleanedProperties)
                        {
                            if (kvp.Key == "COLOUR")
                            {
                                has_COLOUR = true;
                                LIGHT_COLOUR = kvp.Value as string; // Directly assign the string value

                                if (string.IsNullOrEmpty(LIGHT_COLOUR))
                                {
           //                         Console.WriteLine("The COLOUR property is null or empty.");
                                }

                            }
                        }
                        if (has_COLOUR == false)
                        {
                            LIGHT_COLOUR = "12";
             //               Console.WriteLine("LIGHT_COLOUR IS MAGENTA");

                        }//12 means "magenta"
                         //     Console.WriteLine("LIGHT_COLOUR IS");
                         //    Console.WriteLine(LIGHT_COLOUR);
                        int LITCHR = 99;//99 is the defqult vqlue in cqse we need to print to the console
                        int SECTR1 = 999;//99 is the defqult vqlue in cqse we need to print to the consol
                        int SECTR2 = 999;//99 is the defqult vqlue in cqse we need to print to the console

                        foreach (var kvp in cleanedProperties)
                        {
                            if (kvp.Key == "SECTR1")
                            {
                                float tempSectr1; // Use float to handle decimal values
                                string stringValue = (kvp.Value as string)?.Replace(',', '.');  // Use null-conditional operator to avoid null reference exceptions

                                if (stringValue != null && float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tempSectr1))
                                {
                                    SECTR1 = (int)Math.Round(tempSectr1); // Round to the nearest integer
                                }
                                else
                                {
                           //         Console.WriteLine("The SECTR1 property is not a valid number or is null.");
              //                      Console.WriteLine(kvp.Value);
                                    foreach (var kvp2 in feature.Properties)
                                    {
                           //             Console.WriteLine(kvp2.Key);
                            //            Console.WriteLine(kvp2.Value);

                                    }
                                    stringValue = (kvp.Value as string)?.Replace('-', '.');
                                    if (stringValue != null && float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tempSectr1))
                                    {
                                        SECTR1 = (int)Math.Round(tempSectr1); // Round to the nearest integer
                                    }
                                    else
                                    {
                                        Console.WriteLine("Check the SECTR part of the code.");

                                    }
                                }
                            }
                            if (kvp.Key == "SECTR2")
                            {
                                float tempSectr2; // Use float to handle decimal values
                                                  //string stringValue = kvp.Value as string;
                                string stringValue = (kvp.Value as string)?.Replace(',', '.');  // Use null-conditional operator to avoid null reference exceptions
                                if (stringValue != null && float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tempSectr2))
                                {
                                    SECTR2 = (int)Math.Round(tempSectr2); // Round to the nearest integer
                                }
                                else
                                {
            //                        Console.WriteLine("The SECTR2 property is not a valid number or is null.");
             //                       Console.WriteLine(kvp.Value);
                                    foreach (var kvp2 in feature.Properties)
                                    {
                               //         Console.WriteLine(kvp2.Key);
                                //        Console.WriteLine(kvp2.Value);

                                    }
                                    stringValue = (kvp.Value as string)?.Replace('-', '.');
                                    if (stringValue != null && float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tempSectr2))
                                    {
                                        SECTR2 = (int)Math.Round(tempSectr2); // Round to the nearest integer

                                    }
                                    else
                                    {
                                        Console.WriteLine("Check the SECTR part of the code.");

                                    }

                                }
                            }
                        }

                        //now check if it is "No sector"  this is all in the presentation library p-131

                        Boolean IS_NO_SECTOR = false;
                        //    Is the attributes 'SECTR1'(sector limit one) or 'SECTR2'(sector limit two) values absent,
                        //    or does their difference equal zero degrees, or do they equal to 0.00 and 360.00 correspondingly in the object which is calling this procedure ?
                        if (SECTR1 == 999 && SECTR2 == 999) { IS_NO_SECTOR = true; }
                        if (SECTR1 - SECTR2 == 0) { IS_NO_SECTOR = true; }
                        if (SECTR1 == 0 && SECTR2 == 360) { IS_NO_SECTOR = true; }
                        if (SECTR1 == 360 && SECTR2 == 0) { IS_NO_SECTOR = true; }
                        //lights06 continuation
                        Boolean full_length_sector_lines = false;// we might need to get this from the GUI (and then reload the lights layer every time this option is changed)
                                                                 //    Console.WriteLine("about to find out if it has secytor lights or not");

                        if (IS_NO_SECTOR == false)
                        {
                            //Console.WriteLine("RCID IS");
                            //Console.WriteLine(RCID);
                            //Console.WriteLine("SECTR1 IS");
                            //Console.WriteLine(SECTR1);
                            //Console.WriteLine("SECTR2 IS");
                            //Console.WriteLine(SECTR2);
                            if (SECTR2 < SECTR1)
                            {
                                SECTR2 += 360;
                            }
                            //We can now add this point to the SECTR dictionary.
                            //it needs to be reloaded each time the zoom is changed  because the IHO wanted the lines showing the sectors illuminated by lights
                            //to stay the same mlength on the screen. easiest way to do this is to each time work out the position of the end of the line in
                            //geographical coordinates and readd the line to the overlay. Messing around with rendering on the screen directly has proved to be annoying.
                            //  GlobalResources.LIGHTS_SECTR.Add(RCID, new LightSECTRInfo(point, SECTR1, SECTR2));
                            //if (full_length_sector_lines == false)
                            //{
                            //    int LEGLEN = 25;//25mm

                            //}
                            Boolean EXTENDED_ARC_RADIUS = false;
                            int LITVIS = 999;
                            LIGHT_COLOUR_ARC = "";
                            foreach (var kvp in cleanedProperties)
                            {
                                if (kvp.Key == "LITVIS")
                                {
                                    int templitvis; // Temporary variable to hold the parsed integer     
                                    string stringValue = kvp.Value as string;

                                    if (stringValue != null && int.TryParse(stringValue, out templitvis))
                                    {
                                        LITVIS = templitvis;
                                    }

                                }
                                if (LITVIS == 7 || LITVIS == 8 || LITVIS == 3)
                                { //do something
                                    LIGHT_COLOUR_ARC = "CHBLK"; //black
                                }
                            }

                            if (LIGHT_COLOUR == "1-3" || LIGHT_COLOUR == "3")
                            { LIGHT_COLOUR_ARC = "LITRD"; }

                            else if (LIGHT_COLOUR == "1-4" || LIGHT_COLOUR == "4")
                            { LIGHT_COLOUR_ARC = "LITGN"; }
                            else if (LIGHT_COLOUR == "11" || LIGHT_COLOUR == "6" || LIGHT_COLOUR == "1")
                            { LIGHT_COLOUR_ARC = "LITYW"; }
                            else { LIGHT_COLOUR_ARC = "CHMGD"; }
                            GlobalResources.LIGHTS_SECTR.Add(RCID, new LightSECTRInfo(point, SECTR1, SECTR2, LIGHT_COLOUR_ARC));
                            GlobalResources.LIGHTS_SECTR_PROPERTIES.Add(RCID, new LightSECTRInfo_properties(point, SECTR1, SECTR2, LIGHT_COLOUR_ARC, properties_dictionary));


                        }
                        if (IS_NO_SECTOR == true)
                        {//No SECTR Lights plus ? SO HERE WE NEED TO CREATE A LIBRARY OF NO_SECTOR LIGHTS WHICH WE WILL CHECK AFTER LOOPING THROUGH ALL THE LIGHTS GEOJSONS.
                         //Logically, we dont need to worry about other geojson feature collections since they will be located in another area. We just need to make sure that only one S57 file per area is processed.. 
                         //or we just extend the thing like OpenCPN so that you can manually select which overlays from which S57 map we want to display and it automatically givves us the relevant maps to chose from
                         //if the area from two or more maps overlap.
                         // Console.WriteLine($"about to add item with RCID {RCID}");

                            // Check if the RCID already exists in the dictionary
                            if (GlobalResources.LightNO_SECTR.ContainsKey(RCID))
                            {
                                // If it exists, retrieve the existing entry
                                LightNO_SECTR existingLight = GlobalResources.LightNO_SECTR[RCID];

                                // Log the existing entry
                                //       Console.WriteLine($"Duplicate RCID detected: {RCID}. Not adding to dictionary.");
                                //        Console.WriteLine("Existing item details:");
                                foreach (var kvp in existingLight.properties_dictionary)
                                {
                                    //    Console.WriteLine($"{kvp.Key}: {kvp.Value}");


                                }
                                //    Console.WriteLine($"latitude and longitude: {point.Lat},  {point.Lng}");


                                // Log the attempted new entry
                                //      Console.WriteLine("Attempted new item details:");
                                foreach (var kvp in properties_dictionary)
                                {
                                    if (kvp.Key == "FIDN")
                                    {
                                        string fidnString = kvp.Value.ToString(); // Extract the value and convert to string
                                        int fidn;
                                        if (int.TryParse(fidnString, out fidn)) // Try to parse the string to an integer
                                        {
                                            RCID = RCID + fidn; // Concatenate RCID with FIDN if parsing is successful
                                        }
                                        else
                                        {
                                            Console.WriteLine("FIDN is not a valid integer: " + fidnString);
                                        }
                                    }
                                    //      Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                                }


                                //            Console.WriteLine($"latitude and longitude: {point.Lat},  {point.Lng}");
                                GlobalResources.LightNO_SECTR.Add(RCID, new LightNO_SECTR(point, properties_dictionary));
                                //     Console.WriteLine($"Item with RCID {RCID} added successfully after changing RCID because of duplicate.");

                            }
                            //add it anyway

                            else
                            {
                                // If the RCID does not exist, add the new light to the dictionary
                                GlobalResources.LightNO_SECTR.Add(RCID, new LightNO_SECTR(point, properties_dictionary));
                                //       Console.WriteLine($"Item with RCID {RCID} added successfully.");
                            }
                            //Console.WriteLine($"about to add item with RCID {RCID}");

                            //GlobalResources.LightNO_SECTR.Add(RCID, new LightNO_SECTR(point, properties_dictionary));

                            //  FLARE_AT_45_DEG'=FALSE ?



                        }
                        string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\icons", iconFilename);
                        //          Console.WriteLine($"Icon path: {iconPath}");


                    }

                }

                //       Console.WriteLine($"Processed {pointCount} buoy features for {nameOfMarkerType}.");
                // we can also call this after processing all the geojsons
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing LIGHTS features: {ex.Message}");
            }
            finally
            {
                Assign_markers_NO_SECTR(overlay);
            }
        }

        public static void Assign_markers_NO_SECTR(GMapOverlay overlay)
        {
   //         Console.WriteLine("classifying the lights");
            Dictionary<PointLatLng, List<int>> pointMap = new Dictionary<PointLatLng, List<int>>();
            // Populate the pointMap with RCIDs for each point
            foreach (var item in GlobalResources.LightNO_SECTR)
            {
                if (!pointMap.ContainsKey(item.Value.Position))
                    pointMap[item.Value.Position] = new List<int>();
                pointMap[item.Value.Position].Add(item.Key);
            }
            string COLOUR_of_MARKER = "12"; //default
            // Analyze the pointMap to categorize lights
            foreach (var pointEntry in pointMap)
            {
  //              Console.WriteLine("found a light");
               // Console.WriteLine(light.properties_dictionary);


                List<int> identifiers = pointEntry.Value;
                if (identifiers.Count == 1) // Only one light at this point
                {
                    int identifier = identifiers.First();
                    LightNO_SECTR light = GlobalResources.LightNO_SECTR[identifier];
                    // Access properties_dictionary

                    if (light.properties_dictionary.ContainsKey("COLOUR"))
                    {
                        object colourValue = light.properties_dictionary["COLOUR"];
                        COLOUR_of_MARKER = CleanPropertyValue(colourValue.ToString()); ;
                    }
                    else
                    {
                        COLOUR_of_MARKER = "12";//default is confirmed
                    }



                    if (light.properties_dictionary.ContainsKey("COLOUR"))
                    {
                        object colourValue = light.properties_dictionary["COLOUR"];
                        //   Console.WriteLine($"Light {identifier} has colour: {colourValue}");
                        COLOUR_of_MARKER = CleanPropertyValue(colourValue.ToString()); ;

                    }
                    else
                    {
                        //Console.WriteLine($"Light {rcid} does not have a defined colour.");
                        COLOUR_of_MARKER = "12";//default is confirmed

                    }
                    foreach (var property in light.properties_dictionary)
                    {
                        string propertyValue = CleanPropertyValue(property.Value.ToString());
                        //         Console.WriteLine($"{property.Key}: {propertyValue}");
                    }
                    //NO SECTOR' Lights plus? NO case
                    Boolean FLARE_AT_45_DEG = false; //CHECK THIS BIT THOUROUGHLY!!!!!!!!!!!
                                                     //please chatgpt  write some short code here to detect whether the numbers 1 or 6 or 11 are included in the string COLOUR_ofMARKER, if they are the boolean FLARE_AT_45_DEG should be set to true


                    // Inside the foreach loop where you process each identifier:
                    //foreach (var property in light.properties_dictionary)
                    //{
                    //    string propertyValue = CleanPropertyValue(property.Value.ToString());
                    //    //    Console.WriteLine($"{property.Key}: {propertyValue}");

                    //    // Check if the current property is "COLOUR" and contains any of "1", "6", or "11"
                    //    if (property.Key.ToUpper() == "COLOUR" && (propertyValue.Contains("1") || propertyValue.Contains("6") || propertyValue.Contains("11")))
                    //    {
                    //        FLARE_AT_45_DEG = true;
                    //    }
                    //}

                    string LIGHTS_SYMBOL = "LITDEF11"; // Default symbol
                    if (COLOUR_of_MARKER.Contains("1") && COLOUR_of_MARKER.Contains("3"))
                    {
                        LIGHTS_SYMBOL = "LIGHTS11";
                    }
                    else if (COLOUR_of_MARKER.Contains("3"))
                    {
                        LIGHTS_SYMBOL = "LIGHTS11";
                    }
                    else if (COLOUR_of_MARKER.Contains("1") && COLOUR_of_MARKER.Contains("4"))
                    {
                        LIGHTS_SYMBOL = "LIGHTS12";
                    }
                    else if (COLOUR_of_MARKER.Contains("4"))
                    {
                        LIGHTS_SYMBOL = "LIGHTS12";
                    }
                    else if (COLOUR_of_MARKER.Contains("11") || COLOUR_of_MARKER.Contains("6") || COLOUR_of_MARKER.Contains("1"))
                    {
                        LIGHTS_SYMBOL = "LIGHTS13";
                    }

                    string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", $"{LIGHTS_SYMBOL}.png");
                    Bitmap iconBitmap;
                    if (System.IO.File.Exists(iconPath))
                    {
                        iconBitmap = new Bitmap(iconPath);
                        //iconBitmap = new Bitmap(iconBitmap, new Size(2, 2)); // Resize the  icon
                    }
                    else
                    {
                        Console.WriteLine("USING DEFAULT IMAGE");
                        iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", "LITDEF11.png"));
                        //iconBitmap = new Bitmap(iconBitmap, new Size(2, 2));
                    }


                    PointLatLng point = new PointLatLng(pointEntry.Key.Lat, pointEntry.Key.Lng);
                    GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                    marker.Offset = new System.Drawing.Point(0, -8);
             //       Console.WriteLine("about to add a tag to the marker?");

                    //so here we will search the existing tags and NOT add another one if there is already a marker with a tag with same RCID
                    string my_RCID = "0";
                    foreach (var property in light.properties_dictionary)
                    {
    //                        Console.WriteLine($"{property.Key}: {property.Value}");
                        // Check if the current property is "COLOUR" and contains any of "1", "6", or "11"
                        if (property.Key == "RCID")
                        {
                            my_RCID = property.Value.ToString();
        //                    Console.WriteLine("new RCID is");
         //                   Console.WriteLine(my_RCID);
                        }
                    }
                    Boolean Add_Tag = true;
                    foreach (var my_marker in overlay.Markers)
                    {
                        var tagDictionary = my_marker.Tag as Dictionary<string, object>;

                        if (tagDictionary != null)
                        {
                 
                            foreach (var kvp in tagDictionary)

                            {
                                if (kvp.Key.ToString() == "RCID")

                                {
                     //               Console.WriteLine(kvp.Value.ToString());
                                }
                                if( kvp.Key.ToString() == "RCID" && kvp.Value.ToString() == my_RCID)

                                {
                                    Add_Tag = false;
                                }
                                //      messageText += $"{kvp.Key}: {kvp.Value}\n";
                            }

                        }

                    }
                    if (Add_Tag == true) { marker.Tag = light.properties_dictionary;
     //                   Console.WriteLine("about to add a tag to the marker");
                    }
                    //  marker.Tag = light.properties_dictionary;///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    overlay.Markers.Add(marker);

                }
                else // Multiple lights at the same point
                {
                    //   Console.WriteLine($"Found multiple lights at the same location: Latitude {pointEntry.Key.Lat}, Longitude {pointEntry.Key.Lng}");
                    //  Console.WriteLine("found a light with friends");
                    // Here you could compare or display more about these lights
                    foreach (int identifier in identifiers)
                    {
                        //   Console.WriteLine($"Here is light  {identifier}");
                        LightNO_SECTR light = GlobalResources.LightNO_SECTR[identifier];
                        if (light.properties_dictionary.ContainsKey("COLOUR"))
                        {
                            object colourValue = light.properties_dictionary["COLOUR"];
                            //   Console.WriteLine($"Light {identifier} has colour: {colourValue}");
                            COLOUR_of_MARKER = CleanPropertyValue(colourValue.ToString()); ;

                        }
                        else
                        {
                            //Console.WriteLine($"Light {rcid} does not have a defined colour.");
                            COLOUR_of_MARKER = "12";//default is confirmed

                        }
                        foreach (var property in light.properties_dictionary)
                        {
                            string propertyValue = CleanPropertyValue(property.Value.ToString());
                            //         Console.WriteLine($"{property.Key}: {propertyValue}");
                        }
                        //NO SECTOR' Lights plus? YES case
                        Boolean FLARE_AT_45_DEG = false;
                        //please chatgpt  write some short code here to detect whether the numbers 1 or 6 or 11 are included in the string COLOUR_ofMARKER, if they are the boolean FLARE_AT_45_DEG should be set to true
                        // Inside the foreach loop where you process each identifier:
                        foreach (var property in light.properties_dictionary)
                        {
                            string propertyValue = CleanPropertyValue(property.Value.ToString());
                            //    Console.WriteLine($"{property.Key}: {propertyValue}");
                            // Check if the current property is "COLOUR" and contains any of "1", "6", or "11"
                            if (property.Key.ToUpper() == "COLOUR" && (propertyValue.Contains("1") || propertyValue.Contains("6") || propertyValue.Contains("11")))
                            {
                                FLARE_AT_45_DEG = true;
                            }
                        }
                        string LIGHTS_SYMBOL = "LITDEF11"; // Default symbol
                        if (COLOUR_of_MARKER.Contains("1") && COLOUR_of_MARKER.Contains("3"))
                        {
                            LIGHTS_SYMBOL = "LIGHTS11";
                        }
                        else if (COLOUR_of_MARKER.Contains("3"))
                        {
                            LIGHTS_SYMBOL = "LIGHTS11";
                        }
                        else if (COLOUR_of_MARKER.Contains("1") && COLOUR_of_MARKER.Contains("4"))
                        {
                            LIGHTS_SYMBOL = "LIGHTS12";
                        }
                        else if (COLOUR_of_MARKER.Contains("4"))
                        {
                            LIGHTS_SYMBOL = "LIGHTS12";
                        }
                        else if (COLOUR_of_MARKER.Contains("11") || COLOUR_of_MARKER.Contains("6") || COLOUR_of_MARKER.Contains("1"))
                        {
                            LIGHTS_SYMBOL = "LIGHTS13";
                        }

                        string iconPath = Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", $"{LIGHTS_SYMBOL}.png");
                        Bitmap iconBitmap;
                        if (System.IO.File.Exists(iconPath))
                        {
                            iconBitmap = new Bitmap(iconPath);
                            //iconBitmap = new Bitmap(iconBitmap, new Size(2, 2));
                        }
                        else
                        {
                            //                 Console.WriteLine("USING DEFAULT IMAGE");
                            iconBitmap = new Bitmap(Path.Combine("C:\\Program Files (x86)\\Mission Planner\\plugins\\iconsV2", "LITDEF11.png"));
                            //iconBitmap = new Bitmap(iconBitmap, new Size(2, 2));
                        }

                        PointLatLng point = new PointLatLng(pointEntry.Key.Lat, pointEntry.Key.Lng);
                        GMarkerGoogle marker = new GMarkerGoogle(point, iconBitmap);
                        marker.Offset = new System.Drawing.Point(0, -8);
     //                   Console.WriteLine("MULTIPLE LIGHTS about to add a tag to the marker?");
                        //so here we will search the existing tags and NOT add another one if there is already a marker with a tag with same RCID
                        // string myRCID=light.properties_dictionary
                        string my_RCID = "0";
                        foreach (var property in light.properties_dictionary)                        {                         
 //                               Console.WriteLine($"{property.Key}: {property.Value}");
                            // Check if the current property is "COLOUR" and contains any of "1", "6", or "11"
                            if (property.Key.ToString() == "RCID")
                            {
                                 my_RCID = property.Value.ToString();
       //                         Console.WriteLine("new RCID is");
        //                        Console.WriteLine(my_RCID);

                            }
                        }
                        Boolean Add_Tag = true;
                        foreach (var my_marker in overlay.Markers)
                        {
                            var tagDictionary = my_marker.Tag as Dictionary<string, object>;

                            if (tagDictionary != null)
                            {
                                // Log or display marker properties
                             //   string messageText = "Marker properties:\n";
                                foreach (var kvp in tagDictionary)
                                {
                                    if (kvp.Key.ToString() == "RCID")

                                    {
                    //                    Console.WriteLine(kvp.Value.ToString());
                                    }
                                    if (kvp.Key.ToString() == "RCID" && kvp.Value.ToString() == my_RCID)

                                    {
                                        Add_Tag = false;
                                    }
                              //      messageText += $"{kvp.Key}: {kvp.Value}\n";
                                }

                               }

                        }
                        if (Add_Tag == true) { marker.Tag = light.properties_dictionary;
                //            Console.WriteLine("about to add a tag to the marker");
                        }                        //   LIGHTSoverlay
                                                 ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        overlay.Markers.Add(marker);



                    }
                }
             

            }
        }
      

        private static string CleanPropertyValue(string value)
        {
            // Trim leading and trailing brackets, quotes, spaces, and carriage return/line feed
            value = value.Trim(new char[] { '[', ']', '"', ' ', '\n', '\r' }).Trim();

            // Split the value by commas and trim each part, then join with hyphens
            var parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(part => part.Trim(new char[] { ' ', '\n', '\r', '"' }))
                             .Where(part => !string.IsNullOrEmpty(part))
                             .ToArray();

            return string.Join("-", parts);
        }

     
    
 


        private static string GetIconForFeature_multiple(Dictionary<string, object> properties, string nameOfMarkerType, List<string> availableIcons, List<string> relevant_attributes)
        {
            // THIS NEEDS TO BE UPPER CASE
            string markerType = nameOfMarkerType;

            // Create a dictionary to store extracted key-value pairs
            Dictionary<string, string> extractedValues = new Dictionary<string, string>();

            // Extract attributes from properties based on the list of key names
            foreach (var key in relevant_attributes)
            {
                if (properties.TryGetValue(key, out var value))
                {
                    extractedValues[key] = value.ToString(); // Store as string
                                                             //             Console.WriteLine($"Extracted {key} = {value}");
                }
                else
                {
                     extractedValues[key] = string.Empty; // Key not found, set as empty
                                                          //           Console.WriteLine($"Key '{key}' not found in properties");
                }
            }



            // Generate all possible subsets (power set) from the extracted values' keys
            List<string> subsets = GeneratePermutationsofallSubsets(extractedValues.Keys.ToList());

            // Construct possible expected filenames based on subsets
            List<string> expectedFilenames = new List<string>();

            // Print the generated subsets to the console
            //Console.WriteLine("Generated subsets:");
            //foreach (var subset in subsets)
            //{
            //    Console.WriteLine(subset);
            //}

            foreach (var subset in subsets)
            {
                // Create filename by concatenating the key-value pairs in the subset
                string filename = markerType; // Start with the marker type
                foreach (var key in subset.Split(','))
                {
                    // Skip if the value is empty or the key is not in extractedValues
                    if (string.IsNullOrEmpty(key) || !extractedValues.ContainsKey(key))
                        continue;

                    filename += $"_{key}{extractedValues[key]}"; // Concatenate key and value
                }

                filename += ".png"; // Add the file extension
                expectedFilenames.Add(filename);
            }

            // Sort expectedFilenames by length in descending order
            expectedFilenames = expectedFilenames.OrderByDescending(f => f.Length).ToList();


            //Console.WriteLine("Possible expected filenames:");
            //foreach (var filename in expectedFilenames)
            //{
            //    Console.WriteLine(filename);
            //}

            // Find the best match in available icons
            string matchingIcon = expectedFilenames.FirstOrDefault(icon => availableIcons.Contains(icon));

            if (!string.IsNullOrEmpty(matchingIcon))
            {
        //        Console.WriteLine($"Matching icon found: {matchingIcon}");
                return matchingIcon;
            }

          //  Console.WriteLine("No matching icon found; returning default: default.png");
            return "default.png"; // Default fallback icon
        }

        //    // Function to generate all subsets (power set) from a list of keys
        private static List<string> GeneratePermutationsofallSubsets(List<string> keys)
        {
            List<string> allCombinations = new List<string>();

            // Generate subsets (power set)
            int totalSubsets = 1 << keys.Count; // 2^n

            // For each subset
            for (int i = 1; i < totalSubsets; i++)
            {
                List<string> subset = new List<string>();
                // Determine which keys are in the subset
                for (int j = 0; j < keys.Count; j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        subset.Add(keys[j]);
                    }
                }

                // Generate permutations for the current subset
                GeneratePermutations(subset, allCombinations);
            }

            return allCombinations;
        }

        // Helper function to generate permutations for a subset
        private static void GeneratePermutations(List<string> subset, List<string> allCombinations)
        {
            void Permute(List<string> list, int start)
            {
                if (start == list.Count - 1)
                {
                    allCombinations.Add(string.Join(",", list));
                }
                else
                {
                    for (int i = start; i < list.Count; i++)
                    {
                        // Swap elements to create permutations
                        (list[start], list[i]) = (list[i], list[start]);
                        Permute(list, start + 1); // Recursively permute the rest
                        (list[start], list[i]) = (list[i], list[start]); // Swap back to original order
                    }
                }
            }

            Permute(new List<string>(subset), 0);
        }

    

        public GMapOverlay LndareOverlay { get; private set; }
        public GMapOverlay DepareOverlay { get; private set; }
        public GMapOverlay DRGAREOverlay { get; private set; }
        public GMapOverlay NOTMRKoverlay { get; private set; } // New overlay for notice marks 
        public GMapOverlay BOYLAToverlay { get; private set; } // New overlay for laterall buoys
        public GMapOverlay BOYSAWoverlay { get; private set; } // New overlay for safe water buoys
        public GMapOverlay BOYSPPoverlay { get; private set; } // New overlay for safe water buoys
        public GMapOverlay SOUNDGoverlay { get; private set; } // New overlay for soundings
        public GMapOverlay LIGHTSoverlay { get; private set; } // New overlay for lights
        public GMapOverlay Generic_markers_overlay { get; private set; }

        public GMapOverlay UNHANDLEDoverlay { get; private set; }

        private DepthSettings depthSettings;



    }
    public class ENCs_for_Mission_Planner : Plugin
    {


        // internal static List<IconDetails> BOYLATiconLIST; // Now accessible throughout the class
        internal PointLatLng MouseDownStart;
        internal static myOverlay mo;
        internal static myOverlay sectr_overlay;
        internal static myOverlay sectr_overlay2;
        internal static myOverlay sectr_overlay3;

        internal int mouseX;
        internal int mouseY;
        private GeoJsonHandler my_beautiful_overlays; // Class level variable
        private List<GMapOverlay> overlaysToSearch;
        private List<GMapOverlay> PolygonoverlaysToSearch;
        public override string Name
        {
            get { return "ENCs_for_Mission_Planner"; }
        }
        public override string Version
        {
            get { return "0.1"; }
        }
        public override string Author
        {
            get { return "Louis."; }
        }
      
        public override bool Init()
        {
            // Add a new menu item to the Mission Planner map context menu
            ToolStripMenuItem readS57Item = new ToolStripMenuItem("Read S57 Layers");
            readS57Item.Click += ReadS57ItemClick;

            // Ensure that the menu item is added to the correct context menu
            if (Host.FDMenuMap != null)
            {
                Host.FDMenuMap.Items.Add(readS57Item);
            }

            loopratehz = 1;

            var zoom = Host.FDGMapControl.Zoom;

            PointLatLng center = Host.FDGMapControl.Position;
            // Get the number of meters per pixel at the current zoom level
            double metersperPixel = Host.FDGMapControl.MapProvider.Projection.GetGroundResolution((int)zoom, center.Lat);

            //   mo = new myOverlay("polygonOverlay");
            //    mo.AddPolygon(zoom, metersperPixel, center.Lat);
            //     Host.FDGMapControl.Overlays.Add(mo);            
            Console.WriteLine("opened ENC_for_Mission_Planner plugin");


             // Debugging: Check how many overlays and polygons are present
             Console.WriteLine("Overlays count: " + Host.FDGMapControl.Overlays.Count);
    //        Console.WriteLine("Polygons in myOverlay: " + mo.Polygons.Count);


            my_beautiful_overlays = new GeoJsonHandler(); //this creates the overlays that we will put stuff in before placing them on the map

            Console.WriteLine("about to call LoadAndParseGeoJson");
            my_beautiful_overlays.LoadAndParseGeoJson();  // This will read the GeoJSONs, parse them and add them to the overlays, except for the case of lights
            //for lights, this function creates a bunch of dictionaries containing light features with various properties.
            Console.WriteLine("about to add geoJsonHandler.MapOverlay");
                      

            overlaysToSearch = new List<GMapOverlay>
        {
            my_beautiful_overlays.BOYLAToverlay,
            my_beautiful_overlays.NOTMRKoverlay,
            my_beautiful_overlays.BOYSPPoverlay,
            my_beautiful_overlays.LIGHTSoverlay,
            my_beautiful_overlays.BOYSAWoverlay,
            my_beautiful_overlays.Generic_markers_overlay,
            my_beautiful_overlays.UNHANDLEDoverlay
        };


            PolygonoverlaysToSearch = new List<GMapOverlay>
        {
            my_beautiful_overlays.LndareOverlay,
            my_beautiful_overlays.DepareOverlay,
            my_beautiful_overlays.DRGAREOverlay,
            my_beautiful_overlays.UNHANDLEDoverlay

        };

            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.LndareOverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.DepareOverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.DRGAREOverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.NOTMRKoverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.Generic_markers_overlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.BOYLAToverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.BOYSAWoverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.BOYSPPoverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.SOUNDGoverlay);
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.UNHANDLEDoverlay);


            //     Host.FPGMapControl.Overlays.Add(geoJsonHandler.MapOverlay); //  REMEMBER TO ADD IT TO THIS, WHICH IS THE PLANNING MAP
            // Setup event handler to adjust overlay properties based on zoom level



            Host.FDGMapControl.OnMapZoomChanged += MapZoomChanged;
            Host.FDGMapControl.MouseClick += MYgMapControl1_MouseDown;
 //           Console.WriteLine("Inside Init function! Current Zoom: " + Host.FDGMapControl.Zoom);
            Console.WriteLine("Overlay Count: " + Host.FDGMapControl.Overlays.Count);
  

            double init_metersperPixel = 5;
            LightProcessing.ClassifyLights();//you need to call this before creating the overlays for sector lights


            sectr_overlay = new myOverlay("sectr_overlay");
            sectr_overlay.AddSECTR_lines(13, init_metersperPixel, center.Lat, "lights_sectr_alone");
            Host.FDGMapControl.Overlays.Add(sectr_overlay);

            sectr_overlay2 = new myOverlay("sectr_overlay2");
            sectr_overlay2.AddSECTR_lines(13, init_metersperPixel, center.Lat, "lights_sectr_large");
            Host.FDGMapControl.Overlays.Add(sectr_overlay2);

            sectr_overlay3 = new myOverlay("sectr_overlay3");
            sectr_overlay3.AddSECTR_lines(13, init_metersperPixel, center.Lat, "lights_sectr_small");
            Host.FDGMapControl.Overlays.Add(sectr_overlay3);
            //    sectr_overlay.AddSECTR_lines(13, init_metersperPixel, center.Lat);//, "LIGHTS_SECTR_ALONE");
            //sectr_overlay2.AddSECTR_lines(13, init_metersperPixel, center.Lat, "LIGHTS_SECTR_LARGE");
            //sectr_overlay3.AddSECTR_lines(13, init_metersperPixel, center.Lat, "LIGHTS_SECTR_SMALL");

            //  Host.FDGMapControl.Overlays.Add(sectr_overlay);
            //Host.FDGMapControl.Overlays.Add(sectr_overlay2);
            //Host.FDGMapControl.Overlays.Add(sectr_overlay3);


            //   Host.FDGMapControl.MouseClick += FDGMapControl_MouseClick;
            // Host.FDGMapControl.OnMarkerClick += gmap_OnMarkerClick;
            // gmap_OnMarkerClick();
            //     Host.FDGMapControl.MouseClick += FDGMapControl_MouseClick;
            //  Host.FPGMapControl.OnMapZoomChanged += MapZoomChanged;
            Host.FDGMapControl.Overlays.Add(my_beautiful_overlays.LIGHTSoverlay);
            return true;
        }

        //private void ReadS57ItemClick(object sender, EventArgs e)
        //{
        //    using (var fbd = new FolderBrowserDialog())
        //    {
        //        if (fbd.ShowDialog() == DialogResult.OK)
        //        {
        //            // Ensure GDAL is configured before attempting to read files
        //            GdalConfiguration.ConfigureOgr();

        //            // List each .000 file in the selected folder
        //            foreach (string file in Directory.GetFiles(fbd.SelectedPath, "*.000"))
        //            {
        //                Console.WriteLine("Processing file: " + file);
        //                List<string> layers = ListLayerInfo(file);
        //                Console.WriteLine("Layers: " + String.Join(", ", layers));
        //            }
        //        }
        //    }
        //}
        private void ReadS57ItemClick(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    GdalConfiguration.ConfigureOgr();

                    foreach (string file in Directory.GetFiles(fbd.SelectedPath, "*.000"))
                    {
                        Console.WriteLine("Processing file: " + file);
                        List<string> layers = ListLayerInfo(file);
                        foreach (string layerName in layers)
                        {
                            if (GenerateGeoJSONFeatureCollection(layerName, file))
                            {
                                Console.WriteLine($"Generated GeoJSON for layer: {layerName}");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to generate GeoJSON for layer: {layerName}");
                            }
                        }
                    }
                }
            }
        }



private bool GenerateGeoJSONFeatureCollection(string featureType, string fileName)
    {
        try
        {
            Console.WriteLine("We are opening a file");
            string encFileName = fileName;
            string endBit = "_" + featureType.ToUpper() + ".js";  // Added an underscore before the feature type
            string filenameWithJsOnEnd = fileName.Replace(".000", endBit);
            string outName = filenameWithJsOnEnd;
            Console.WriteLine("Making GeoJSON: " + outName);

            // Open the original data source
            DataSource ds = Ogr.Open(encFileName, 0);
            Layer layer = ds.GetLayerByName(featureType);
            if (layer == null)
            {
                Console.WriteLine("Layer not found: " + featureType);
                return false;
            }

            // Create the GeoJSON output
            Driver drv = Ogr.GetDriverByName("GeoJSON");
            if (drv == null)
            {
                Console.WriteLine("GeoJSON driver not available.");
                return false;
            }

            DataSource outDs = drv.CreateDataSource(outName, null);
            if (outDs == null)
            {
                Console.WriteLine("Could not create GeoJSON output.");
                return false;
            }

            Layer outLayer = outDs.CopyLayer(layer, featureType, null);
            if (outLayer == null)
            {
                Console.WriteLine("Could not copy layer to GeoJSON.");
                return false;
            }

            outDs.FlushCache();
            Console.WriteLine("Success");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return false;
        }
    }


    private List<string> ListLayerInfo(string filePath)
        {
            List<string> layerNames = new List<string>();

            // Open the data source
            DataSource dataSource = Ogr.Open(filePath, 0);
            if (dataSource == null)
            {
                Console.WriteLine("Unable to open file: " + filePath);
                return layerNames;
            }

            // Iterate through each layer and add layer names to the list
            for (int i = 0; i < dataSource.GetLayerCount(); i++)
            {
                Layer layer = dataSource.GetLayerByIndex(i);
                layerNames.Add(layer.GetName());
            }

            // Clean up
            dataSource.Dispose();

            return layerNames;
        }



        //private void ReadS57ItemClick(object sender, EventArgs e)
        //{
        //    using (var fbd = new FolderBrowserDialog())
        //    {
        //        if (fbd.ShowDialog() == DialogResult.OK)
        //        {
        //            foreach (string file in Directory.GetFiles(fbd.SelectedPath, "*.000"))
        //            {
        //                Console.WriteLine(file);
        //            }
        //        }
        //    }
        //    GdalConfiguration.ConfigureOgr();
        //}

        // Example assuming Mission Planner has an appropriate method/class to handle GDAL operations



        private void DisplayLightSectrPropertiesNearClick(PointLatLng clickLocation, int pixelThreshold, GMapControl mapControl)
        {
            var clickPoint = mapControl.FromLatLngToLocal(clickLocation);  // Convert to screen coordinates

            foreach (var entry in GlobalResources.LIGHTS_SECTR_PROPERTIES)  // Loop through each light sector entry
            {
                var lightInfo = entry.Value;
                var lightPoint = mapControl.FromLatLngToLocal(lightInfo.Position);  // Convert the light's position to screen coordinates

                double dx = Math.Abs(clickPoint.X - lightPoint.X);
                double dy = Math.Abs(clickPoint.Y - lightPoint.Y);

                double distance = Math.Sqrt(dx * dx + dy * dy);  // Calculate the Euclidean distance in pixels

                if (distance <= pixelThreshold)  // Check if within the pixel threshold
                {
                    // Prepare and display the message box with the light's properties
                    string messageText = "Light Sector Properties:\n";
                    foreach (var kvp in lightInfo.properties_dictionary)
                    {
                        messageText += $"{kvp.Key}: {kvp.Value}\n";
                    }

                    MessageBox.Show(messageText, "Light Sector Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }


        private List<GMap.NET.WindowsForms.GMapMarker> GetMarkersNearClick(PointLatLng clickLocation, int pixelThreshold, GMapControl mapControl, List<GMapOverlay> overlays)
        {
            var nearbyMarkers = new List<GMap.NET.WindowsForms.GMapMarker>();
            var clickPoint = mapControl.FromLatLngToLocal(clickLocation); // Convert to screen coordinates

            foreach (var overlay in overlays) // Loop through each overlay
            {
                foreach (var marker in overlay.Markers)
                {
                    var markerPoint = mapControl.FromLatLngToLocal(marker.Position);

                    double dx = Math.Abs(clickPoint.X - markerPoint.X);
                    double dy = Math.Abs(clickPoint.Y - markerPoint.Y);

                    double distance = Math.Sqrt(dx * dx + dy * dy); // Calculate the Euclidean distance in pixels

                    if (distance <= pixelThreshold)
                    {
                        nearbyMarkers.Add(marker); // Add marker if within the pixel threshold
                    }
                }
            }

            return nearbyMarkers; // Return the list of nearby markers from all overlays
        }

        private void MYgMapControl1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                var clickLocation = Host.FDGMapControl.FromLocalToLatLng(e.X, e.Y);

                // Pass the list of overlays to GetMarkersNearClick
                var nearbyMarkers = GetMarkersNearClick(clickLocation, 10, Host.FDGMapControl, overlaysToSearch);

                foreach (var marker in nearbyMarkers)
                {
                    var tagDictionary = marker.Tag as Dictionary<string, object>;

                    if (tagDictionary != null)
                    {
                        // Log or display marker properties
                        string messageText = "Marker properties:\n";
                        foreach (var kvp in tagDictionary)
                        {
                            messageText += $"{kvp.Key}: {kvp.Value}\n";
                        }

                        // Display the message box with the marker's information
                        MessageBox.Show(messageText, "Marker Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }



                // Check if the click is within any polygon
                foreach (var overlay in PolygonoverlaysToSearch)
                {
                    foreach (var poly in overlay.Polygons)
                    {
                        if (IsPointInPolygon(clickLocation, poly.Points))
                        {   // Handle the polygon click
                            var propertiesDictionary = poly.Tag as Dictionary<string, object>;
                            if (propertiesDictionary != null)
                            {
                                // Prepare the message displaying polygon properties
                                string messageText = "Polygon properties:\n";
                                foreach (var kvp in propertiesDictionary)
                                {
                                    messageText += $"{kvp.Key}: {kvp.Value}\n";
                                }

                                // Display the message box with the polygon's information
                                MessageBox.Show(messageText, "Polygon Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                // Handle the case where no additional data is attached to the polygon
                                MessageBox.Show("Polygon clicked but no additional data available.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            //break; // Exit the loop after handling the first found polygon
                        }
                    }
                }
                DisplayLightSectrPropertiesNearClick(clickLocation, 10, Host.FDGMapControl);

            }
        }

        private bool IsPointInPolygon(PointLatLng point, List<PointLatLng> polygon)
        {
            bool result = false;
            int j = polygon.Count() - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].Lat < point.Lat && polygon[j].Lat >= point.Lat || polygon[j].Lat < point.Lat && polygon[i].Lat >= point.Lat)
                {
                    if (polygon[i].Lng + (point.Lat - polygon[i].Lat) / (polygon[j].Lat - polygon[i].Lat) * (polygon[j].Lng - polygon[i].Lng) < point.Lng)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }
        private void MapZoomChanged()
        {
            var zoom = Host.FDGMapControl.Zoom;
     //       Host.FDGMapControl.Overlays.Remove(mo);
            Host.FDGMapControl.Overlays.Remove(sectr_overlay);
            Host.FDGMapControl.Overlays.Remove(sectr_overlay2);
            Host.FDGMapControl.Overlays.Remove(sectr_overlay3);

            PointLatLng center = Host.FDGMapControl.Position;
            double metersperPixel = Host.FDGMapControl.MapProvider.Projection.GetGroundResolution((int)zoom, center.Lat);
        //    Console.WriteLine("metersperPixel");
         //   Console.WriteLine(metersperPixel);
      //      mo = new myOverlay("polygonOverlay");
       //     mo.AddPolygon(zoom, metersperPixel, center.Lat);
        //    Host.FDGMapControl.Overlays.Add(mo);

            if (zoom > 10)
            {
                sectr_overlay = new myOverlay("sectr_overlay");
                sectr_overlay.AddSECTR_lines(zoom, metersperPixel, center.Lat, "lights_sectr_alone");
                Host.FDGMapControl.Overlays.Add(sectr_overlay);

                sectr_overlay2 = new myOverlay("sectr_overlay2");
                sectr_overlay2.AddSECTR_lines(zoom, metersperPixel, center.Lat, "lights_sectr_large");
                Host.FDGMapControl.Overlays.Add(sectr_overlay2);

                sectr_overlay3 = new myOverlay("sectr_overlay3");
                sectr_overlay3.AddSECTR_lines(zoom, metersperPixel, center.Lat, "lights_sectr_small");
                Host.FDGMapControl.Overlays.Add(sectr_overlay3);
                // Adjust properties for sector lights polygons

            }



            // Adjust properties for LndareOverlay polygons
            foreach (var polygon in my_beautiful_overlays.LndareOverlay.Polygons)
            {
                polygon.IsVisible = zoom > 2;
                polygon.Stroke.Width = zoom > 10 ? 2 : 1; // Thicker line at higher zoom levels, thinner otherwise
            }

            // Adjust properties for DepareOverlay polygons
            foreach (var polygon in my_beautiful_overlays.DepareOverlay.Polygons)
            {
                polygon.IsVisible = zoom > 5;
                polygon.Stroke.Width = zoom > 10 ? 2 : 1; // Thicker line at higher zoom levels, thinner otherwise
            }
            // Adjust properties for DRGAREOverlay polygons
            foreach (var polygon in my_beautiful_overlays.DRGAREOverlay.Polygons)
            {
                polygon.IsVisible = zoom > 5;
                polygon.Stroke.Width = zoom > 10 ? 2 : 1; // Thicker line at higher zoom levels, thinner otherwise
            }

            // Adjust visibility for BOYLAToverlay markers
            foreach (var marker in my_beautiful_overlays.BOYLAToverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }
                        // Adjust visibility for LIGHTSoverlay markers
            foreach (var marker in my_beautiful_overlays.LIGHTSoverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }
            // Adjust visibility for BOYLAToverlay markers
            foreach (var marker in my_beautiful_overlays.Generic_markers_overlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }


            // Adjust visibility for NOTMRKoverlay markers
            foreach (var marker in my_beautiful_overlays.NOTMRKoverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }
            // Adjust visibility for BOYSPP markers
            foreach (var marker in my_beautiful_overlays.BOYSPPoverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }

            // Adjust visibility for safe water buoy markers
            foreach (var marker in my_beautiful_overlays.BOYSAWoverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }
            foreach (var marker in my_beautiful_overlays.LIGHTSoverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // 10 Only visible at higher zoom levels
            }
            foreach (var marker in my_beautiful_overlays.UNHANDLEDoverlay.Markers)
            {
                marker.IsVisible = zoom > 10; // Only visible at higher zoom levels
            }

            //foreach (var route in my_beautiful_overlays.LIGHTSoverlay.Markers)
            //{
            //    route.IsVisible = zoom > 7; // 10 Only visible at higher zoom levels
            //}
            //foreach (var polygon in my_beautiful_overlays.LIGHTSoverlay.Markers)
            //{
            //    polygon.IsVisible = zoom > 7; // 10 Only visible at higher zoom levels
            //}
            foreach (var marker in my_beautiful_overlays.SOUNDGoverlay.Markers)
            {
                marker.IsVisible = zoom > 14; // Only visible at higher zoom levels
            }
        }


        public override bool Loaded()
        //Loaded called after the plugin dll successfully loaded
        {
            return true;     //If it is false plugin will not start (loop will not called)
        }
        public override bool Loop()
        //Loop is called in regular intervalls (set by loopratehz)
        {
            int n = 1;
            if (n == 3){
                
            }
            n += 1;
           
    //        Console.WriteLine("Looping the loop");


            return true;	//Return value is not used
        }

        public override bool Exit()
        //Exit called when plugin is terminated (usually when Mission Planner is exiting)
        {
            return true;	//Return value is not used
        }
    }
}