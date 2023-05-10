using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AutoLevel
{
    [AddComponentMenu("AutoLevel/Blocks Repo")]
    public partial class BlocksRepo : MonoBehaviour
    {
        [System.Serializable]
        public class ActionsGroup
        {
            public string name;

            [System.Serializable]
            public class GroupActions { public List<BlockAction> actions = new List<BlockAction>(); }

            public List<GroupActions> groupActions = new List<GroupActions>();
        }

        [SerializeField]
        public List<string>         groups;
        [SerializeField]
        public List<string>         weightGroups;

        [SerializeField]
        public List<ActionsGroup>   actionsGroups;

        [HideInInspector] [SerializeField]
        public List<Connection>     bannedConnections;
        [HideInInspector] [SerializeField]
        public List<Connection>     exclusiveConnections;

        public const string EMPTY_GROUP = "Empty";
        public const string SOLID_GROUP = "Solid";
        public const string BASE_GROUP  = "Base";

        public List<string> GetActionsGroupsNames()
        {
            return new List<string>(actionsGroups.Select((group) => group.name));
        }

        public List<string> GetAllGroupsNames()
        {
            var list = GetBaseGroups();
            list.AddRange(groups);
            return list;
        }

        public List<string> GetAllWeightGroupsNames()
        {
            var list = GetBaseGroups();
            list.AddRange(weightGroups);
            return list;
        }

        public int GetLayersCount() => GetLayersCount(GetComponentsInChildren<BlockAsset>());

        private static int GetLayersCount(IEnumerable<BlockAsset> assets)
        {
            return BlockAsset.GetBlocksEnum(assets).Max((block) => block.layerSettings.layer) + 1;
        }

        private List<string> GetBaseGroups() => new List<string>() { EMPTY_GROUP, SOLID_GROUP, BASE_GROUP };

        public Runtime CreateRuntime() => new Runtime(this, GetAllGroupsNames(), GetAllWeightGroupsNames(), actionsGroups);
    }
}