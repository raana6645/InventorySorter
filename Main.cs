using Rocket.API;
using Rocket.Core.Commands;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
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
            RocketCommandManager.RegisterCommand(this, new SortInvCommand());
            RocketCommandManager.RegisterCommand(this, new SortStorageCommand());
            Logger.Log("InventorySorter loaded! /sortinv | /sortstorage");
        }

        protected override void Unload()
        {
            Instance = null;
            Logger.Log("InventorySorter unloaded!");
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
        }

        // ==================== 公有 API ====================

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
                if (index >= 0)
                {
                    storageItems.removeItem((byte)index);
                }
            }

            // 排序
            snapshots = SortSnapshots(snapshots);

            // 放回存储容器
            List<ItemSnapshot> remaining = PlaceInItems(storageItems, snapshots);

            // 剩余放入背包
            if (remaining.Count > 0 && player != null)
            {
                List<ItemSnapshot> backpackRemaining = PlaceInBackpack(player, remaining);

                foreach (var snap in backpackRemaining)
                {
                    DropItem(player, snap.Item);
                }
            }
        }

        public void SortBackpack(Player player)
        {
            if (player == null) return;

            List<ItemSnapshot> snapshots = new List<ItemSnapshot>();

            // 收集所有背包页面的物品
            for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.STORAGE; page++)
            {
                Items pageItems = player.inventory.items[page];
                if (pageItems == null) continue;

                var jarList = new List<ItemJar>(pageItems.items);
                foreach (var jar in jarList)
                {
                    snapshots.Add(new ItemSnapshot(jar, page));
                    int index = pageItems.items.IndexOf(jar);
                    if (index >= 0)
                    {
                        pageItems.removeItem((byte)index);
                    }
                }
            }

            // 排序并放回
            snapshots = SortSnapshots(snapshots);
            List<ItemSnapshot> remaining = PlaceInBackpack(player, snapshots);

            // 放不下的丢弃
            foreach (var snap in remaining)
            {
                DropItem(player, snap.Item);
            }
        }

        // ==================== 排序逻辑 ====================

        private List<ItemSnapshot> SortSnapshots(List<ItemSnapshot> snaps)
        {
            return snaps.OrderBy(s =>
            {
                if (s.Item == null) return 0;
                return s.Item.id;
            }).ThenBy(s =>
            {
                return s.Item != null ? s.Item.amount : (byte)0;
            }).ToList();
        }

        // ==================== 放置逻辑 ====================

        private List<ItemSnapshot> PlaceInBackpack(Player player, List<ItemSnapshot> snaps)
        {
            List<ItemSnapshot> remaining = new List<ItemSnapshot>(snaps);

            for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.STORAGE; page++)
            {
                Items pageItems = player.inventory.items[page];
                if (pageItems == null) continue;

                List<ItemSnapshot> stillRemaining = new List<ItemSnapshot>();
                foreach (var snap in remaining)
                {
                    if (!TryAddToItems(pageItems, snap))
                    {
                        stillRemaining.Add(snap);
                    }
                }
                remaining = stillRemaining;
                if (remaining.Count == 0) break;
            }

            return remaining;
        }

        private List<ItemSnapshot> PlaceInItems(Items items, List<ItemSnapshot> snaps)
        {
            List<ItemSnapshot> remaining = new List<ItemSnapshot>();
            foreach (var snap in snaps)
            {
                if (!TryAddToItems(items, snap))
                {
                    remaining.Add(snap);
                }
            }
            return remaining;
        }

        private bool TryAddToItems(Items items, ItemSnapshot snap)
        {
            for (byte y = 0; y < items.height; y++)
            {
                for (byte x = 0; x < items.width; x++)
                {
                    if (IsPositionFree(items, (byte)x, (byte)y, snap.SizeX, snap.SizeY, snap.Rot))
                    {
                        items.addItem((byte)x, (byte)y, snap.Rot, snap.Item);
                        return true;
                    }
                }
            }
            return false;
        }

        // ==================== 辅助方法 ====================

        private InteractableStorage GetOpenStorage(Player player)
        {
            if (player == null) return null;

            // 尝试通过 Player 的交互对象检测
            try
            {
                Interactable interactable = player.interactable;
                if (interactable != null && interactable is InteractableStorage)
                {
                    InteractableStorage storage = interactable as InteractableStorage;
                    if (storage != null && storage.isOpen && storage.opener == player)
                    {
                        return storage;
                    }
                }
            }
            catch { }

            return null;
        }

        private bool IsPositionFree(Items items, byte x, byte y, byte sizeX, byte sizeY, byte rot)
        {
            byte w = rot == 1 ? sizeY : sizeX;
            byte h = rot == 1 ? sizeX : sizeY;

            if ((byte)(x + w) > items.width || (byte)(y + h) > items.height) return false;

            foreach (var jar in items.items)
            {
                byte jarW = jar.rot == 1 ? jar.size_y : jar.size_x;
                byte jarH = jar.rot == 1 ? jar.size_x : jar.size_y;

                for (byte dy = 0; dy < h; dy++)
                {
                    for (byte dx = 0; dx < w; dx++)
                    {
                        byte checkX = (byte)(x + dx);
                        byte checkY = (byte)(y + dy);
                        if (checkX >= jar.x && checkX < (byte)(jar.x + jarW) &&
                            checkY >= jar.y && checkY < (byte)(jar.y + jarH))
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
            ItemManager.dropItem(item, pos, true, false, true);
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
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "sortinv";
        public string Help => "Sort your backpack inventory";
        public string Syntax => "";
        public List<string> Aliases => new List<string> { "sort" };
        public List<string> Permissions => new List<string>();

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
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "sortstorage";
        public string Help => "Sort the open storage container";
        public string Syntax => "";
        public List<string> Aliases => new List<string> { "stsort" };
        public List<string> Permissions => new List<string>();

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
