using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace AutoLevel
{

    [System.Serializable]
    public class BlockResources
    {
        public Mesh mesh;
        public Material material;
    }

    [System.Serializable]
    public class ActionsGroup
    {
        public string name;

        [System.Serializable]
        public class GroupActions { public List<BlockAction> actions = new List<BlockAction>(); }

        public List<GroupActions> groupActions = new List<GroupActions>();
    }

    [AddComponentMenu("AutoLevel/Blocks Repo")]
    public partial class BlocksRepo : MonoBehaviour
    {
        [SerializeField]
        private List<string> userGroups = new List<string>();
        [SerializeField]
        private List<ActionsGroup> actionsGroups = new List<ActionsGroup>();

        public const string EMPTY_GROUP = "Empty";
        public const string SOLID_GROUP = "Solid";
        public const string BASE_GROUP = "Base";

        static int groups_counter_pk = "block_repo_groups_counter".GetHashCode();
        static int generate_blocks_pk = "block_repo_generate_blocks".GetHashCode();

        public List<string> GetActionsGroupsNames()
        {
            return new List<string>(actionsGroups.Select((group) => group.name));
        }

        public List<string> GetAllGroupsNames()
        {
            var list = new List<string>() {
            EMPTY_GROUP,
            SOLID_GROUP,
            BASE_GROUP };
            list.AddRange(userGroups);
            return list;
        }

        public Runtime CreateRuntime() => new Runtime(transform, GetAllGroupsNames(), actionsGroups);
    }
}