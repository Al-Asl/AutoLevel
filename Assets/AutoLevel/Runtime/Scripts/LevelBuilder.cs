using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GroupSettings
{
    public int hash;
    public bool overridWeight;
    public float Weight;
}

[System.Serializable]
public class GroupsSettings
{
    public bool toggle;
    public List<GroupSettings> groups;
}

[AddComponentMenu("AutoLevel/Level Builder")]
public class LevelBuilder : MonoBehaviour
{
    const int k_start_size = 5;

    [SerializeField]
    private BlocksRepo blockRepo;

    [SerializeField]
    private GroupsSettings groupsSettings;

    [SerializeField]
    private BoundsInt selection = new BoundsInt(Vector3Int.zero, Vector3Int.one);
    [SerializeField]
    private LevelData levelData = new LevelData(new BoundsInt(Vector3Int.zero, Vector3Int.one * k_start_size));
    [SerializeField]
    private Array3D<InputWaveBlock> inputWave = new Array3D<InputWaveBlock>(Vector3Int.one * k_start_size);
}