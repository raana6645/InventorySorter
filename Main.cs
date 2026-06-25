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

        // ==================== 命令入口 ====================

        public void ExecuteCommand(UnturnedPlayer player, string command)
        {
            if (player == null) return;
            Player p = player.Player;
            if (p == null)
            {
                Rocket.Core.Logging.Logger.Log("[DEBUG] ExecuteCommand: player.Player is null");
                return;
            }

            ChatManager.serverSendMessage(
                "[DEBUG] 收到命令: " + command, Color.yellow, null, player.SteamPlayer(),
                EChatMode.SAY, null, true);

            InteractableStorage storage = GetOpenStorage(p);

            if (command == "sortinv")
            {
                if (storage != null)
                {
                    ChatManager.serverSendMessage(
                        "[DEBUG] 检测到打开存储容器，改为整理容器", Color.yellow, null, player.SteamPlayer(),
                        EChatMode.SAY, null, true);
                    SortStorage(storage);
                }
                else
                {
                    SortBackpack(p);
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
            else if (command == "debuginv")
            {
                DebugInventory(player, p);
            }
        }

        private InteractableStorage GetOpenStorage(Player player)
        {
            if (player == null) return null;
            try
            {
                InteractableStorage[] storages = Object.FindObjectsOfType<InteractableStorage>();
                foreach (InteractableStorage storage in storages)
                {
                    if (storage != null && storage.isOpen && storage.opener == player)
                    {
                        return storage;
                    }
                }
            }
            catch { }
            return null;
        }

        // ==================== 背包排序 ====================

        public void SortBackpack(Player player)
        {
            List<ItemSnapshot> snapshots = new List<ItemSnapshot>();

            // 从第 1 页开始遍历（跳过装备页）
            for (byte page = 1; page < player.inventory.items.Length; page++)
            {
                Items pageItems = player.inventory.items[page];
                if (pageItems == null) continue;

                foreach (var jar in pageItems.items.ToList())
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

        // ==================== 存储容器排序 ====================

        public void SortStorage(InteractableStorage storage)
        {
            Items container = storage.items;
            Player player = storage.opener;
            List<ItemSnapshot> snapshots = new List<ItemSnapshot>();

            foreach (var jar in container.items.ToList())
            {
                snapshots.Add(new ItemSnapshot(jar));
                int index = container.items.IndexOf(jar);
                if (index >= 0) container.removeItem((byte)index);
            }

            snapshots = SortSnapshots(snapshots);
            List<ItemSnapshot> remaining = PlaceInStorage(container, snapshots);

            if (remaining.Count > 0 && player != null)
            {
                List<ItemSnapshot> backpackRemaining = PlaceInBackpack(player, remaining);
                foreach (var snap in backpackRemaining)
                {
                    DropItem(player, snap.Item);
                }
            }
        }

        // ==================== 排序逻辑 ====================

        private List<ItemSnapshot> SortSnapshots(List<ItemSnapshot> snapshots)
        {
            return snapshots.OrderBy(snap =>
            {
                if (snap.Item == null) return 0;
                ItemAsset asset = (ItemAsset)Assets.find(EAssetType.ITEM, snap.Item.id);
                return asset != null ? asset.id : 0;
            }).ThenBy(snap =>
            {
                return snap.Item != null ? snap.Item.amount : (byte)0;
            }).ToList();
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
                    Items pageItems = player.inventory.items[page];
                    if (pageItems == null) continue;

                    for (byte y = 0; y < pageItems.height && !placed; y++)
                    {
                        for (byte x = 0; x < pageItems.width && !placed; x++)
                        {
                            if (IsSlotFree(pageItems, x, y, snap.SizeX, snap.SizeY, snap.Rot))
                            {
                                pageItems.addItem(x, y, snap.Rot, snap.Item);
                                placed = true;
                            }
                        }
                    }
                }

                if (!placed)
                {
                    remaining.Add(snap);
                }
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

                if (!placed)
                {
                    remaining.Add(snap);
                }
            }

            return remaining;
        }

        // ==================== 辅助方法 ====================

        private bool IsSlotFree(Items items, byte x, byte y, byte sizeX, byte sizeY, byte rot)
        {
            byte w = rot == 1 ? sizeY : sizeX;
            byte h = rot == 1 ? sizeX : sizeY;

            if (x + w > items.width || y + h > items.height) return false;

            for (byte dy = 0; dy < h; dy++)
            {
                for (byte dx = 0; dx < w; dx++)
                {
                    foreach (var jar in items.items)
                    {
                        byte jarW = jar.rot == 1 ? jar.size_y : jar.size_x;
                        byte jarH = jar.rot == 1 ? jar.size_x : jar.size_y;
                        if ((byte)(x + dx) >= jar.x && (byte)(x + dx) < jar.x + jarW &&
                            (byte)(y + dy) >= jar.y && (byte)(y + dy) < jar.y + jarH)
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
            if (item == null) return;
            Vector3 pos = player.transform.position;
            pos.y += 1f;
            ItemManager.dropItem(item, pos, true, false, true);
        }

        // ==================== 调试方法 ====================

        private void DebugInventory(UnturnedPlayer unturnedPlayer, Player player)
        {
            ChatManager.serverSendMessage(
                "[DEBUG] === 背包状态 ===", Color.cyan, null, unturnedPlayer.SteamPlayer(),
                EChatMode.SAY, null, true);

            ChatManager.serverSendMessage(
                "[DEBUG] 总页数: " + player.inventory.items.Length, Color.cyan, null, unturnedPlayer.SteamPlayer(),
                EChatMode.SAY, null, true);

            int totalItems = 0;
            for (byte page = 0; page < player.inventory.items.Length; page++)
            {
                Items pageItems = player.inventory.items[page];
                if (pageItems == null)
                {
                    ChatManager.serverSendMessage(
                        "[DEBUG] 页" + page + ": NULL", Color.cyan, null, unturnedPlayer.SteamPlayer(),
                        EChatMode.SAY, null, true);
                    continue;
                }
                int count = pageItems.items.Count;
                totalItems += count;
                ChatManager.serverSendMessage(
                    "[DEBUG] 页" + page + " (" + pageItems.width + "x" + pageItems.height + "): " + count + " 物品", Color.cyan, null, unturnedPlayer.SteamPlayer(),
                    EChatMode.SAY, null, true);
            }

            ChatManager.serverSendMessage(
                "[DEBUG] 总物品数: " + totalItems, Color.cyan, null, unturnedPlayer.SteamPlayer(),
                EChatMode.SAY, null, true);

            // 检查存储容器
            InteractableStorage found = Main.Instance.GetOpenStorage(player);
            ChatManager.serverSendMessage(
                "[DEBUG] 附近打开存储: " + (found == null ? "无" : "有! isOpen=" + found.isOpen), Color.cyan, null, unturnedPlayer.SteamPlayer(),
                EChatMode.SAY, null, true);
        }
    }

    // ==================== 物品快照 ====================

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

    public class DebugInvCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Player; } }
        public string Name { get { return "debuginv"; } }
        public string Help { get { return "Show inventory debug info"; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string> { "di" }; } }
        public List<string> Permissions { get { return new List<string>(); } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player != null && Main.Instance != null)
            {
                Main.Instance.ExecuteCommand(player, "debuginv");
            }
        }
    }
}
