import subprocess
from osgeo import ogr, gdal
import os
import glob
import geojson
import fileinput
import time
import os
import glob


def line_pre_adder(filename, line_to_prepend):
    with open(filename, encoding='latin1') as f:
        lines = f.readlines()
        for line in lines:
            line.encode('utf-8')
            # print(line)
        with open(filename, 'w', encoding='utf-8') as f:
            f.write(line_to_prepend)
            for line in lines:
                f.write(line)

def read_s57(filename):
    # Set S57 options
    gdal.SetConfigOption('S57_PROFILE', 'iw')

    # Open the dataset
    ds = gdal.OpenEx(filename, gdal.OF_VECTOR)
    if ds is None:
        print("Failed to open file.")
        return

    # Print out all layer names and their feature counts
    for i in range(ds.GetLayerCount()):
        layer = ds.GetLayerByIndex(i)
        print(f"{i + 1}: {layer.GetName()} ({layer.GetGeomType()}) - {layer.GetFeatureCount()} features")

# Example usage read_s57("1R5NK000.000")

enc_list_geojson = []
list_of_name_var = []
list_filename_with_js_on_end = []




output_file = "layer_names.txt"
enc_directory = r"./put_ENC_in_Here"
output_path = os.path.join(enc_directory, output_file)
def extract_layer_names(file_path):
    layer_names = []
    with open(file_path, 'r') as file:
        for line in file:
            if line.startswith("Layer name:"):
                # Assuming the format is "Layer name: LAYER_NAME"
                parts = line.split(":")
                if len(parts) > 1:
                    layer_names.append(parts[1].strip())
    return layer_names


def run_command_with_name(out_name, enc_file_name, feature_type):
    # Configure the S57_PROFILE to "iw" and include all primitives
    result = subprocess.run(
        ["ogr2ogr", "-f", "GeoJSON", out_name, enc_file_name, feature_type,
         ],
        capture_output=True, text=True
    )#"--config", "S57_PROFILE", "iw"
    print("result")
    print(result)
    if 'ERROR' in result.stderr:
        print(result.stderr)
        print("FALSE")
        return False
    else:
        print("TRUE")
        return True


def generate_a_geojson_feature_collection(feature_type,fileName):
    # Your code here
    print("we are opening a file")
    enc_file_name = fileName
    end_bit = "_" + str(feature_type).upper() + ".js"  # Added an underscore before the feature type
    filename_with_js_on_end = fileName.replace(".000", end_bit)
    print(filename_with_js_on_end)
    out_name =  filename_with_js_on_end
    print("making geojson:", out_name)
    if run_command_with_name(out_name, enc_file_name, feature_type):
        print("success")
        return True
    return False
    # No need for else statement, as we don't want to create empty files if ogr2ogr fails
    time.sleep(0.1)


print("we are at the start of the code")
# Loop through all .000 files in the specified directory
for file_path in glob.glob("./put_ENC_in_Here/*.000", recursive=True):
    # Ensure that "layer_names.txt" is emptied of its contents.
    print("emptying the layer names text file")
    if os.path.exists(output_path):
        os.remove(output_path)
    with open(output_path, 'w') as file:
        pass  # Create an empty file
    print(f"Processing file: {file_path}")
     # Set the directory and file name
    # # Construct the full path to the ENC file
    full_enc_path =file_path
    # # Prepare the ogrinfo command
    command = ["ogrinfo", "-ro", "-al", "-so", full_enc_path]
     # # Run the command and capture the output
    result = subprocess.run(command, text=True, capture_output=True)
    # # Write the output to a text file
    with open(output_path, 'w') as file:
         file.write(result.stdout)
    if result.stderr:
        print("Errors:\n", result.stderr)
    #generate a listof all the layers in this file
    layer_names = extract_layer_names(output_path)
    print("all_layers are :")
    print(layer_names)
    for layer_name in layer_names:
        if generate_a_geojson_feature_collection(layer_name,file_path):
            print(f"Generated GeoJSON for layer: {layer_name}")
        else:
            print(f"Failed to generate GeoJSON for layer: {layer_name}")
