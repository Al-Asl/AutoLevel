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
        public bool useFilling = true;
        [Space]
        [SerializeField]
        public List<string> groups          = new List<string>();
        [SerializeField]
        public List<string> weightGroups    = new List<string>();
        [Space]
        [SerializeField]
        public List<ActionsGroup>   actionsGroups = new List<ActionsGroup>()
        {
            new ActionsGroup()
            {
                name = "Full Rotate On Y",
                groupActions = new List<ActionsGroup.GroupActions>()
                {
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.RotateY } },
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.RotateY, BlockAction.RotateY } },
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.RotateY, BlockAction.RotateY ,BlockAction.RotateY } },
                }
            },
            new ActionsGroup()
            {
                name = "Rotate On Y",
                groupActions = new List<ActionsGroup.GroupActions>()
                {
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.RotateY } },
                }
            },
            new ActionsGroup()
            {
                name = "Full Mirror",
                groupActions = new List<ActionsGroup.GroupActions>()
                {
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.MirrorX } },
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.MirrorY } },
                    new ActionsGroup.GroupActions() { actions = new List<BlockAction>(){ BlockAction.MirrorZ } },
                }
            },
        };

        [HideInInspector] [SerializeField]
        public List<Connection>     bannedConnections       = new List<Connection>();
        [HideInInspector] [SerializeField]
        public List<Connection>     exclusiveConnections    = new List<Connection>();

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