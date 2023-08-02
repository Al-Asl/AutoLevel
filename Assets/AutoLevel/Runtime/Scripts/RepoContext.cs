using AlaslTools;
using System.Collections.Generic;
using System.Linq;
using static AutoLevel.BlocksRepo;

namespace AutoLevel
{
    internal class StagingContext
    {
        private BiDirectionalList<string>    groups;
        private BiDirectionalList<string>    weightGroups;
        private BiDirectionalList<int>       actionsGroupsHash;

        private int LayersCount;

        private Dictionary<int,IBlock>      blocks;
        private List<ActionsGroup>          actionsGroups;

        public IBlock GetBlock(int blockHash) => blocks[blockHash];

        public StagingContext(
            int                 LayersCount,
            List<string>        GroupsNames,
            List<string>        WeightGroupsNames,
            List<ActionsGroup>  actionsGroups)
        {
            this.groups         = new BiDirectionalList<string>(GroupsNames);
            this.weightGroups   = new BiDirectionalList<string>(WeightGroupsNames);

            this.actionsGroups = actionsGroups;
            actionsGroupsHash   = new BiDirectionalList<int>(actionsGroups.Select((ag) => ag.name.GetHashCode()));

            this.LayersCount = LayersCount;

            blocks = new Dictionary<int, IBlock>();
        }

        public void AddBlock(IBlock block)
        {
            blocks.Add(block.GetHashCode(),block);
            AddAGVariant(block.GetHashCode(), block.GetHashCode(), new List<BlockAction>());
        }

        public void AddBlock(int authoringBlockHash,IBlock block,List<BlockAction> actions)
        {
            blocks.Add(block.GetHashCode(), block);
            AddAGVariant(authoringBlockHash, block.GetHashCode(), actions);
        }

        /// <summary>
        /// adding a block as an authoring block and create it's variants from actionsGroups
        /// </summary>
        public void AddBlockAndVariants(IBlock block)
        {
            AddBlock(block.GetHashCode(), block.CreateCopy(), new List<BlockAction>());

            foreach (var actionGroupKey in block.blockAsset.actionsGroups)
            {
                var ag = GetActionsGroup(actionGroupKey);
                if (ag == null)
                    continue;

                foreach (var group in ag.groupActions)
                {
                    var variant = block.CreateCopy();
                    variant.ApplyActions(group.actions);
                    AddBlock(block.GetHashCode(), variant, group.actions);
                }
            }
        }

        public ActionsGroup GetActionsGroup(int hash)
        {
            if (!actionsGroupsHash.Contains(hash))
                return null;
            else
                return actionsGroups[actionsGroupsHash.GetIndex(hash)]; 
        }

        #region ActionsGroup

        private Dictionary<int, List<(int, List<BlockAction>)>> agVariants = 
            new Dictionary<int, List<(int, List<BlockAction>)>>();

        public void AddAGVariant(int authoringBlockHash, int blockHash, List<BlockAction> actions)
        {
            if (!agVariants.ContainsKey(authoringBlockHash))
                agVariants[authoringBlockHash] = new List<(int, List<BlockAction>)>();

            agVariants[authoringBlockHash].Add((blockHash, actions));
        }

        public List<int> QueryAuthoringBlocks() => new List<int>(agVariants.Select((pair) => pair.Key));

        public IBlock GetFirstVariant(int authoringBlockHash) => blocks[agVariants[authoringBlockHash][0].Item1];

        #endregion

        #region Layers

        private Dictionary<int, List<(int, BlocksResolve)>> baseUpperBlocks = 
            new Dictionary<int, List<(int, BlocksResolve)>>();

        public void AddUpperBlock(int blockKey, int upperBlockKey, BlocksResolve resolve = BlocksResolve.Matching)
        {
            if (!baseUpperBlocks.ContainsKey(blockKey))
                baseUpperBlocks[blockKey] = new List<(int, BlocksResolve)>();

            baseUpperBlocks[blockKey].Add((upperBlockKey, resolve));
        }

        public bool HasUpperBlocks(int block) => baseUpperBlocks.ContainsKey(block);

        public IEnumerable<int> GetUpperBlocksEnum(int block) => baseUpperBlocks[block].Select((item) => item.Item1);

        #endregion

        public ConnectingContext BuildConnectingContext(bool useFilling)
        {
            return new ConnectingContext(LayersCount, useFilling, blocks, groups, weightGroups, baseUpperBlocks, agVariants);
        }
    }

    internal class ConnectingContext
    {
        private class BlockGroupComparer : IComparer<IBlock>
        {
            private Dictionary<int, int> groupHashToIndex;

            public BlockGroupComparer(Dictionary<int, int> groupHashToIndex)
            {
                this.groupHashToIndex = groupHashToIndex;
            }

            public int Compare(IBlock a, IBlock b)
            {
                return groupHashToIndex[a.group].CompareTo(groupHashToIndex[b.group]);
            }
        }

        public int LayersCount;

        public List<IBlock> blocks;
        public BiDirectionalList<string> groups;
        public BiDirectionalList<string> weightGroups;

        public Runtime.LayerPartioner   layerPartioner;
        public List<List<int>>          groupStartIndex;

        public BiDirectionalList<int>   BlocksHash;
        public List<ConnectionsIds>     BlocksConnections;

        public Dictionary<int, List<(int, BlocksResolve)>> baseUpperBlocks;

        public ConnectingContext(
            int LayersCount,
            bool useFilling,
            Dictionary<int,IBlock> blocksMap,
            BiDirectionalList<string> groups,
            BiDirectionalList<string> weightGroups,
            Dictionary<int, List<(int, BlocksResolve)>> baseUpperBlocks,
            Dictionary<int, List<(int, List<BlockAction>)>> agVariants)
        {
            this.LayersCount = LayersCount;
            this.blocks = new List<IBlock>(blocksMap.Select((pair)=>pair.Value));
            this.groups = groups;
            this.weightGroups = weightGroups;
            this.baseUpperBlocks = baseUpperBlocks;
            this.agVariants = agVariants;

            /// Reorder the blocks ////

            // First by layers //

            var layerStartIndex = new List<int>();
            this.blocks.Sort((a, b) => a.layerSettings.layer.CompareTo(b.layerSettings.layer));

            var currentLayer = 0;
            layerStartIndex.Add(currentLayer);
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block.layerSettings.layer != currentLayer)
                {
                    currentLayer++;
                    layerStartIndex.Add(i);
                    if (block.layerSettings.layer != currentLayer)
                        throw new MissingLayersException(currentLayer);
                }
            }

            layerPartioner = new Runtime.LayerPartioner(layerStartIndex, blocks.Count);

            // Then by groups //

            var groupHashToIndex = HashToIndexLookup(groups);
            var groupComparer = new BlockGroupComparer(groupHashToIndex);
            for (int layer = 0; layer < LayersCount; layer++)
            {
                var range = layerPartioner.GetRange(layer);
                blocks.Sort(range.x, range.y - range.x, groupComparer);
            }

            groupStartIndex = new List<List<int>>();
            groupStartIndex.Fill(LayersCount);
            for (int layer = 0; layer < LayersCount; layer++)
            {
                var list = groupStartIndex[layer];
                var range = layerPartioner.GetRange(layer);
                var lastGroup = -1;
                for (int i = range.x; i < range.y; i++)
                {
                    var block = blocks[i];
                    while (groupHashToIndex[block.group] != lastGroup)
                    {
                        list.Add(i);
                        lastGroup++;
                    }
                }
                while (groups.Count != list.Count)
                    list.Add(range.y);
            }

            /// Generate the hash and base connections ///

            BlocksHash          = new BiDirectionalList<int>();
            BlocksConnections   = new List<ConnectionsIds>();
            foreach (var block in blocks)
            {
                BlocksHash.Add(block.GetHashCode());
                BlocksConnections.Add(useFilling? block.compositeIds : block.baseIds);
            }
        }

        private Dictionary<int, int> HashToIndexLookup<T>(IEnumerable<T> list)
        {
            var result = new Dictionary<int, int>();
            int i = 0;
            foreach (var item in list)
                result[item.GetHashCode()] = i++;
            return result;
        }

        #region ActionsGroup

        private Dictionary<int, List<(int, List<BlockAction>)>> agVariants =
            new Dictionary<int, List<(int, List<BlockAction>)>>();

        public bool HasAgVariants(int blockHash) => agVariants.ContainsKey(blockHash);

        public IEnumerable<(int, List<BlockAction>)> GetAGVariants(int blockHash, bool fullActions = false)
        {
            if (fullActions)
            {
                foreach (var v in agVariants[blockHash])
                    yield return (v.Item1, blocks[BlocksHash.GetIndex(v.Item1)].actions);
            }
            else
            {
                foreach (var v in agVariants[blockHash])
                    yield return v;
            }

        }

        #endregion
    }
}