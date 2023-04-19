using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AlaslTools;

namespace AutoLevel
{
    public partial class LevelBuilderEditor
    {
        public class SO : BaseSO<LevelBuilder> , ILevelBuilderData
        {
            struct LevelLayerSP
            {
                public Vector3Int           size => sizeProp.vector3IntValue;

                public SerializedProperty   arrayProp;
                public SerializedProperty   sizeProp;
                public SerializedProperty   validProp;

                public LevelLayerSP(SerializedProperty layerProp)
                {
                    var blocksProp  = layerProp.FindPropertyRelative("blocks");
                    arrayProp       = blocksProp.FindPropertyRelative("array");
                    sizeProp        = blocksProp.FindPropertyRelative("size");
                    validProp       = layerProp.FindPropertyRelative("valid");
                }
            }

            public LevelBuilder Builder                             => target;
            public BlocksRepo BlockRepo                             => blockRepo;
            public List<LevelBuilder.GroupSettings> GroupsWeights   => groupsWeights;
            public LevelBuilder.BoundarySettings BoundarySettings   => boundarySettings;
            public LevelData LevelData                              => levelData;
            public Array3D<InputWaveCell> InputWave                 => inputWave;


            public BlocksRepo                           blockRepo;

            public List<LevelBuilder.GroupSettings>     groupsWeights;

            public LevelBuilder.BoundarySettings        boundarySettings;

            public bool                                 useMutliThreadedSolver;

            public BoundsInt                            selection;
            [SOIgnore]
            public LevelData                            levelData;
            [SOIgnore]
            public Array3D<InputWaveCell>               inputWave;

            public bool rebuild_bottom_layers_editor_only;


            private SerializedProperty levelDataPositionProp;
            private SerializedProperty levelDataLayersProp;

            private SerializedProperty inputWaveArrayProp;
            private SerializedProperty inputWaveSizeProp;


            public SO(Object target) : base(target) { }

            protected override void OnIntialize()
            {
                var levelDataProp       = serializedObject.FindProperty(nameof(levelData));
                levelDataPositionProp   = levelDataProp.FindPropertyRelative("position");
                levelDataLayersProp     = levelDataProp.FindPropertyRelative("layers");

                var firstLayerProp  = new LevelLayerSP(levelDataLayersProp.GetArrayElementAtIndex(0));
                levelData           = new LevelData(new BoundsInt(levelDataPositionProp.vector3IntValue, firstLayerProp.size));

                var inputWaveProp   = serializedObject.FindProperty(nameof(inputWave));
                inputWaveArrayProp  = inputWaveProp.FindPropertyRelative("array");
                inputWaveSizeProp   = inputWaveProp.FindPropertyRelative("size");

                inputWave           = new Array3D<InputWaveCell>(inputWaveSizeProp.vector3IntValue);
            }

            public void SetSelection(BoundsInt bounds)
            {
                bounds.position     = Vector3Int.Min(levelData.bounds.max - Vector3Int.one, bounds.position);
                bounds.size         = Vector3Int.Max(Vector3Int.one, bounds.size);
                bounds.ClampToBounds(levelData.bounds);

                selection = bounds;
                ApplyField(nameof(selection));
            }

            public void ApplyLevelData()
            {
                ApplyLevelPosition();
                ApplyAllLevelLayers();
            }

            public void ApplyLevelPosition()
            {
                levelDataPositionProp.vector3IntValue = levelData.bounds.position;
                serializedObject.ApplyModifiedProperties();
            }

            public void ApplyAllLevelLayers()
            {
                if(levelDataLayersProp.arraySize != levelData.LayersCount)
                    levelDataLayersProp.arraySize = levelData.LayersCount;

                for (int i = 0; i < levelData.LayersCount; i++)
                    ApplyLevelLayer(new BoundsInt(Vector3Int.zero, levelData.bounds.size), i, false);

                serializedObject.ApplyModifiedProperties();
            }

            public void ApplyLevelLayer(int layer) => ApplyLevelLayer(new BoundsInt(Vector3Int.zero, levelData.size), layer); 

            public void ApplyLevelLayer(BoundsInt bounds, int layer, bool applySO = true)
            {
                var layerSP = new LevelLayerSP(levelDataLayersProp.GetArrayElementAtIndex(layer));
                layerSP.validProp.boolValue = levelData.GetLayer(layer).Valid;

                Apply(layerSP.arrayProp, layerSP.sizeProp, levelData.GetLayer(layer).Blocks,
                    bounds, (prop, value) => prop.intValue = value );

                if(applySO) serializedObject.ApplyModifiedProperties();
            }

            public void ApplyInputWave() => ApplyInputWave(new BoundsInt(Vector3Int.zero, inputWave.Size));

            public void ApplyInputWave(BoundsInt bounds)
            {
                Apply(inputWaveArrayProp, inputWaveSizeProp, inputWave, bounds,
                    (prop, value) => prop.FindPropertyRelative("groups").intValue = value.groups);

                serializedObject.ApplyModifiedProperties();
            }

            protected override void OnUpdate()
            {
                levelData.SetLayerCount(levelDataLayersProp.arraySize);

                for (int i = 0; i < levelData.LayersCount; i++)
                {
                    var layerSP = new LevelLayerSP(levelDataLayersProp.GetArrayElementAtIndex(i));
                    levelData.GetLayer(i).Valid = layerSP.validProp.boolValue;

                    Update(layerSP.arrayProp, layerSP.sizeProp, levelData.GetLayer(i).Blocks, (prop) => prop.intValue);
                }

                Update(inputWaveArrayProp, inputWaveSizeProp, inputWave,
                    (prop) =>
                    {
                        var iw = new InputWaveCell();
                        iw.groups = prop.FindPropertyRelative("groups").intValue;
                        return iw;
                    });
            }

            protected override void OnApply()
            {
                ApplyAllLevelLayers();
                ApplyInputWave();
            }

            private static void Update<T>(SerializedProperty arrayProp, SerializedProperty sizeProp,
                Array3D<T> array, System.Func<SerializedProperty, T> getter)
            {
                if (array.Size != sizeProp.vector3IntValue)
                    array.Resize(sizeProp.vector3IntValue);

                for (int i = 0; i < arrayProp.arraySize; i++)
                    array[SpatialUtil.Index1DTo3D(i, array.Size)] = getter(arrayProp.GetArrayElementAtIndex(i));
            }

            private static void Apply<T>(SerializedProperty arrayProp, SerializedProperty sizeProp,
                Array3D<T> array,BoundsInt bounds , System.Action<SerializedProperty, T> setter)
            {
                if (array.Size != sizeProp.vector3IntValue)
                {
                    sizeProp.vector3IntValue = array.Size;
                    arrayProp.arraySize = array.Size.x * array.Size.y * array.Size.z;
                }

                var sizex = array.Size.x;
                var sizexy = array.Size.x * array.Size.y;
                foreach (var index in SpatialUtil.Enumerate(bounds))
                    setter(arrayProp.GetArrayElementAtIndex(SpatialUtil.Index3DTo1D(index, sizex, sizexy)), array[index]);
            }
        }
    }
}
