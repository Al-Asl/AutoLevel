using System.Linq;
using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{ 
    public class InputWaveDrawer : System.IDisposable
    {
        Texture3D texture;
        BlocksRepo.Runtime repo;
        ILevelBuilderData builderData;

        Array3D<InputWaveCell> inputWave => builderData.InputWave;

        public InputWaveDrawer(BlocksRepo.Runtime repo, ILevelBuilderData builderData)
        {
            this.repo = repo;
            this.builderData = builderData;
            Regenrate();
        }

        public void Dispose()
        {
            if(texture != null)
                Object.DestroyImmediate(texture);
        }

        public void Regenrate()
        {
            if (texture != null && (
               texture.width != inputWave.Size.x ||
               texture.height != inputWave.Size.y ||
               texture.depth != inputWave.Size.z))
                Object.DestroyImmediate(texture, false);
            if (texture == null)
            {
                texture = new Texture3D(inputWave.Size.x, inputWave.Size.y, inputWave.Size.z, TextureFormat.ARGB32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            Color emptyGroupClr = new Color(1, 1, 1, 1);
            Color solidGroupsClr = new Color(0, 1f, 0, 1f);
            Color fullGroupsClr = new Color(0, 0, 0, 0);

            foreach (var i in SpatialUtil.Enumerate(inputWave.Size))
            {
                var iWave = inputWave[i.z, i.y, i.x];

                Color col;

                if (iWave.ContainAll)
                    col = fullGroupsClr;
                else if (iWave.GroupsCount(repo.GroupsCount) == 1)
                {
                    var gi = iWave.GroupsEnum(repo.GroupsCount).First();
                    if (gi == 0)
                        col = emptyGroupClr;
                    else if (gi == 1)
                        col = solidGroupsClr;
                    else
                        col = AlaslTools.ColorUtility.GetColor(repo.GetGroupHash(gi));
                }
                else
                {
                    var h = new XXHash();
                    foreach (var gi in iWave.GroupsEnum(repo.GroupsCount))
                        h = h.Append(repo.GetGroupHash(gi));
                    col = AlaslTools.ColorUtility.GetColor(h);
                }

                texture.SetPixel(i.x, i.y, i.z, col);
            }
            texture.Apply();
        }

        public void Draw()
        {
            int size = Mathf.Max(inputWave.Size.x, inputWave.Size.y, inputWave.Size.z);
            Handles.matrix = Matrix4x4.TRS(builderData.LevelData.bounds.center, Quaternion.identity, size * Vector3.one);
            Handles.DrawTexture3DVolume(texture, 0.5f, 5, FilterMode.Point);
            Handles.matrix = Matrix4x4.identity;
        }
    }
}