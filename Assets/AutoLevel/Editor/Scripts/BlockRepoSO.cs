using UnityEditor;
using UnityEngine;
using AlaslTools;
using System.Collections.Generic;
using System.Linq;

namespace AutoLevel
{
    public class BlocksRepoSO : BaseSO<BlocksRepo>
    {
        public bool useFilling;

        public List<string>         groups;
        public List<string>         weightGroups;

        public List<BlocksRepo.ActionsGroup>    actionsGroups;

        public List<Connection>     bannedConnections;
        public List<Connection>     exclusiveConnections;

        public BlocksRepoSO(Object target) : base(target) { IntegrityCheck(this); }
        public BlocksRepoSO(SerializedObject serializedObject) : base(serializedObject) { IntegrityCheck(this); }

        public static void IntegrityCheckAndAllChildren(BlocksRepo repo)
        {
            var so = new BlocksRepoSO(repo);
            ChildrenIntegrityCheck(repo);
            so.Dispose();
        }

        public static void ChildrenIntegrityCheck(BlocksRepo repo)
        {
            GetRepoEntities(repo, out var all, out var active);
            IntegrityCheck(all);
        }

        private static void IntegrityCheck(IEnumerable<MonoBehaviour> allRepoEntities)
        {
            foreach (var entity in allRepoEntities)
            {
                if (entity is BlockAsset)
                    BlockAssetSO.IntegrityCheck((BlockAsset)entity);
                else if (entity is BigBlockAsset)
                    BigBlockAssetSO.IntegrityCheck((BigBlockAsset)entity);
            }
        }

        public static void GetRepoEntities(
            BlocksRepo repo,
            out List<MonoBehaviour> allRepoEntities,
            out List<MonoBehaviour> activeRepoEntities)
        {
            var allTransforms = repo.GetComponentsInChildren<Transform>(true);
            var allBlockAssets = allTransforms.Select((t) => t.GetComponent<BlockAsset>()).Where((asset) => asset != null);
            var allBigBlockAssets = allTransforms.Select((t) => t.GetComponent<BigBlockAsset>()).Where((asset) => asset != null);

            allRepoEntities = new List<MonoBehaviour>(allBlockAssets.Cast<MonoBehaviour>().Concat(allBigBlockAssets));
            activeRepoEntities = new List<MonoBehaviour>(allRepoEntities.Where((e) => e.gameObject.activeInHierarchy));
        }

        public static void IntegrityCheck(BlocksRepo repo) 
        { var so = new BlocksRepoSO(repo); so.Dispose(); }

        private static void IntegrityCheck(BlocksRepoSO so)
        {
            HashSet<string> set = new HashSet<string>();
            bool apply = false;

            for (int i = so.bannedConnections.Count - 1; i > -1; i--)
                if (!so.bannedConnections[i].Valid)
                {
                    so.bannedConnections.RemoveAt(i);
                    apply = true;
                }

            for (int i = so.exclusiveConnections.Count - 1; i > -1; i--)
                if (!so.exclusiveConnections[i].Valid)
                {
                    so.exclusiveConnections.RemoveAt(i);
                    apply = true;
                }

            set.Clear();
            for (int i = so.groups.Count - 1; i > -1; i--)
            {
                if (set.Contains(so.groups[i]))
                {
                    so.groups.RemoveAt(i);
                    apply = true;
                }
                else
                    set.Add(so.groups[i]);
            }

            set.Clear();
            for (int i = so.weightGroups.Count - 1; i > -1; i--)
            {
                if (set.Contains(so.weightGroups[i]))
                {
                    so.weightGroups.RemoveAt(i);
                    apply = true;
                }
                else
                    set.Add(so.weightGroups[i]);
            }

            set.Clear();
            for (int i = so.actionsGroups.Count - 1; i > -1; i--)
            {
                if (set.Contains(so.actionsGroups[i].name))
                {
                    apply = true;
                    so.actionsGroups.RemoveAt(i);
                }
                else
                    set.Add(so.actionsGroups[i].name);
            }

            if (apply) so.Apply();
        }
    }
}