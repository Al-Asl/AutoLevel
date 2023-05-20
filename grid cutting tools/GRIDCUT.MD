# Grid Cutting Tools

grid cutting tools are third-party tools used to cut models into grid in order to be used by the big block asset in AutoLevel.

## Blender

to use the tool in blender select an object and make sure that the object is placed in the positive XYZ region, then run the script `grid_cut_blender.py` the script will cut the model and set the pivot correctly.

to do fbx export from blender with the correct rotation do the following:
* go to `File->Export->FBX`
* from the option make sure that the `Transform->Forward` is set to `Y Forward` and the `Transform->Up` is set to `Z Up`, then set the `Transform->Scale` to `0.01`
* in Unity select the fbx file and uncheck `Convert Units` and check `Bake Axis Conversion` and hit `Apply`