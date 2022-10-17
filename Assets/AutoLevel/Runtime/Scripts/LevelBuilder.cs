using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{


    [AddComponentMenu("AutoLevel/Level Builder")]
    public class LevelBuilder : MonoBehaviour
    {
        [System.Serializable]
        public class GroupSettings
        {
            public int hash;
            public bool overridWeight;
            public float Weight;
        }

        [System.Serializable]
        public class GroupBoundary
        {
            public List<int> groups = new List<int>();
        }

        [System.Serializable]
        public class BoundarySettings
        {
            public GroupBoundary[] groupsBoundary = new GroupBoundary[6];
            public LevelBuilder[] levelBoundary = new LevelBuilder[6];
        }

        const int k_start_size = 5;

        [SerializeField]
        private BlocksRepo blockRepo;

        [SerializeField]
        private List<GroupSettings> groupsSettings = new List<GroupSettings>();
        [SerializeField]
        private BoundarySettings boundarySettings = new BoundarySettings();

        [SerializeField]
        private BoundsInt selection = new BoundsInt(Vector3Int.zero, Vector3Int.one);
        [SerializeField]
        private LevelData levelData = new LevelData(new BoundsInt(Vector3Int.zero, Vector3Int.one * k_start_size));
        [SerializeField]
        private Array3D<InputWaveCell> inputWave = new Array3D<InputWaveCell>(Vector3Int.one * k_start_size);
    }

}