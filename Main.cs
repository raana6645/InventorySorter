using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace InventorySorter
{
    public class Main : RocketPlugin
    {
        public static Main Instance;

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("InventorySorter loaded! /sortinv | /sortstorage");
        }

        protected override void Unload()
        {
            Instance = null;
            Rocket.Core.Logging.Logger.Log("InventorySorter unloaded!");
        }

        // 命令调用入口
        public void ExecuteCommand(UnturnedPlayer player, string command)
        {
            if (player == null) return;
            Player p = player.Player;
            if (p == null) return;

            InteractableStorage storage = GetOpenStorage(p);

            if (command == "sortinv")
            {
                if (storage != null)
                {
                    SortStorage(storage);
                    ChatManager.serverSendMessage("Storage sorted!", Color.green, null, player.SteamPlayer(), EChatMode.SAY, null, true);
                }
                else
                {
                    SortBackpack(p);
                    ChatManager.serverSendMessage("Backpack sorted!", Color.green, null, player.SteamPlayer(), EChatMode.SAY, null, true);
                }
            }
            else if (command == "sortstorage")
            {
                if (storage != null)
                {
                    SortStorage(storage);
                    ChatManager.serverSendMessage("Storage sorted!", Color.green, null, player.SteamPlayer(), EChatMode.SAY, null, true);
                }
                else
                {
                    ChatManager.serverSendMessage("No storage open!", Color.red, null, player.SteamPlayer(), EChatMode.SAY, null, true);
                }
            }
        }

        // ==================== 公有 API ====================

        private InteractableStorage GetOpenStorage(Player player)
        {
            return null;
        }

        public void SortStorage(InteractableStorage storage)
        {
            if (storage == null) return;

            Items storageItems = storage.items;
            if (storageItems == null) return;

            Player player = storage.opener;
            List<ItemSnapshot> snapshots = new List<ItemSnapshot>();

            // 收集物品快照
            var jarList = new List<ItemJar>(storageItems.items);
            foreach (var jar in jarList)
            {
                snapshots.Add(new ItemSnapshot(jar));
                int index = storageItems.items.IndexOf(jar);
                if (index >= 0) storageItems.removeItem((byte)index);
            }

            snapshots = SortSnapshots(snapshots);
            List<ItemSnapshot> remaining = PlaceInStorage(storageItems, snapshots);

            // 放不下的回到玩家背包
            if (remaining.Count > 0 && player != null)
            {
                List<ItemSnapshot> leftover = PlaceInBackpack(player, remaining);
                foreach (var snap in leftover)
                {
                    DropItem(player, snap.Item);
                }
            }
        }

        public void SortBackpack(Player player)
        {
            if (player == null) return;
            List<ItemSnapshot> snapshots = new List<ItemSnapshot>();

            // 从第 1 页开始遍历（跳过装备页）
            for (byte page = 1; page < player.inventory.items.Length; page++)
            {
                Items pageItems = player.inventory.items[page];
                if (pageItems == null) continue;

                var jarList = new List<ItemJar>(pageItems.items);
                foreach (var jar in jarList)
                {
                    snapshots.Add(new ItemSnapshot(jar, page));
                    int index = pageItems.items.IndexOf(jar);
                    if (index >= 0) pageItems.removeItem((byte)index);
                }
            }

            snapshots = SortSnapshots(snapshots);
            List<ItemSnapshot> remaining = PlaceInBackpack(player, snapshots);

            foreach (var snap in remaining)
            {
                DropItem(player, snap.Item);
            }
        }

        // ==================== 排序逻辑 ====================

        private List<ItemSnapshot> SortSnapshots(List<ItemSnapshot> snapshots)
        {
            return snapshots
                .OrderBy(s => s.Item != null ? s.Item.id : (ushort)0)
                .ThenBy(s => s.Item != null ? s.Item.amount : (byte)0)
                .ToList();
        }

        // ==================== 放回背包 ====================

        private List<ItemSnapshot> PlaceInBackpack(Player player, List<ItemSnapshot> snapshots)
        {
            List<ItemSnapshot> remaining = new List<ItemSnapshot>();

            foreach (var snap in snapshots)
            {
                bool placed = false;

                for (byte page = 1; page < player.inventory.items.Length && !placed; page++)
                {
                    Items items = player.inventory.items[page];
                    if (items == null) continue;

                    for (byte y = 0; y < items.height && !placed; y++)
                    {
                        for (byte x = 0; x < items.width && !placed; x++)
                        {
                            if (IsSlotFree(items, x, y, snap.SizeX, snap.SizeY, snap.Rot))
                            {
                                items.addItem(x, y, snap.Rot, snap.Item);
                                placed = true;
                            }
                        }
                    }
                }

                if (!placed) remaining.Add(snap);
            }

            return remaining;
        }

        // ==================== 放回存储容器 ====================

        private List<ItemSnapshot> PlaceInStorage(Items container, List<ItemSnapshot> snapshots)
        {
            List<ItemSnapshot> remaining = new List<ItemSnapshot>();

            foreach (var snap in snapshots)
            {
                bool placed = false;

                for (byte y = 0; y < container.height && !placed; y++)
                {
                    for (byte x = 0; x < container.width && !placed; x++)
                    {
                        if (IsSlotFree(container, x, y, snap.SizeX, snap.SizeY, snap.Rot))
                        {
                            container.addItem(x, y, snap.Rot, snap.Item);
                            placed = true;
                        }
                    }
                }

                if (!placed) remaining.Add(snap);
            }

            return remaining;
        }

        // ==================== 辅助方法 ====================

        private bool IsSlotFree(Items items, byte px, byte py, byte sx, byte sy, byte rot)
        {
            byte w = (rot == 1) ? sy : sx;
            byte h = (rot == 1) ? sx : sy;

            if (px + w > items.width || py + h > items.height) return false;

            for (byte dy = 0; dy < h; dy++)
            {
                for (byte dx = 0; dx < w; dx++)
                {
                    foreach (var jar in items.items)
                    {
                        byte jw = (jar.rot == 1) ? jar.size_y : jar.size_x;
                        byte jh = (jar.rot == 1) ? jar.size_x : jar.size_y;
                        byte cx = (byte)(px + dx);
                        byte cy = (byte)(py + dy);
                        if (cx >= jar.x && cx < jar.x + jw &&
                            cy >= jar.y && cy < jar.y + jh)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private void DropItem(Player player, Item item)
        {
            if (item == null || player == null) return;
            Vector3 pos = player.transform.position;
            pos.y += 1f;
            ItemManager.dropItem(item, pos, true, true, true);
        }
    }

    /// <summary>
    /// 物品快照
    /// </summary>
    public class ItemSnapshot
    {
        public Item Item;
        public byte OriginalPage;
        public byte SizeX;
        public byte SizeY;
        public byte Rot;

        public ItemSnapshot(ItemJar jar, byte page = 0)
        {
            Item = jar.item;
            OriginalPage = page;
            SizeX = jar.size_x;
            SizeY = jar.size_y;
            Rot = jar.rot;
        }
    }

    // ==================== 命令类 ====================

    public class SortInvCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Player; } }
        public string Name { get { return "sortinv"; } }
        public string Help { get { return "Sort your backpack inventory"; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string> { "sort" }; } }
        public List<string> Permissions { get { return new List<string>(); } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player != null && Main.Instance != null)
            {
                Main.Instance.ExecuteCommand(player, "sortinv");
            }
        }
    }

    public class SortStorageCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Player; } }
        public string Name { get { return "sortstorage"; } }
        public string Help { get { return "Sort the open storage container"; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string> { "stsort" }; } }
        public List<string> Permissions { get { return new List<string>(); } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player != null && Main.Instance != null)
            {
                Main.Instance.ExecuteCommand(player, "sortstorage");
            }
        }
    }
}
