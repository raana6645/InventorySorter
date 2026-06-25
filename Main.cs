using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events;
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
            UnturnedPlayerEvents.OnPlayerInventoryDrag += OnPlayerInventoryDrag;
            Logger.Log("InventorySorter loaded!");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerInventoryDrag -= OnPlayerInventoryDrag;
            Logger.Log("InventorySorter unloaded!");
        }

        private void OnPlayerInventoryDrag(UnturnedPlayer player, ItemJar from, ItemJar to)
        {
            if (from == null || to == null) return;
            if (from.x != 0 || from.y != 0) return;
            if (to.x != 0 || to.y != 0) return;

            Player p = player.Player;
            if (p == null) return;

            InteractableStorage storage = GetOpenStorage(p);
            if (storage != null)
            {
                SortStorage(storage);
            }
            else
            {
                SortBackpack(p);
            }
        }

        private InteractableStorage GetOpenStorage(Player player)
        {
            // 检测玩家当前打开的存储容器
            if (player.interactableStorage != null &&
                player.interactableStorage.isOpen &&
                player.interactableStorage.opener == player)
            {
                return player.interactableStorage;
            }
            return null;
        }

        // ==================== 背包排序 ====================

        public void SortBackpack(Player player)
        {
            List<ItemJarSnapshot> items = new List<ItemJarSnapshot>();

            // 遍历所有页面（跳过快键槽页面 SLOTS）
            for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.STORAGE; page++)
            {
                Items pageItems = player.inventory.items[page];
                if (pageItems == null) continue;

                // 收集快照并移除
                foreach (var jar in pageItems.items.ToList())
                {
                    items.Add(new ItemJarSnapshot(jar, page));
                    pageItems.removeItem(pageItems.items.IndexOf(jar));
                }
            }

            items = SortItems(items);
            List<ItemJarSnapshot> remaining = PlaceItemsBack(player, items);

            foreach (var snap in remaining)
            {
                DropItem(player, snap.Item);
            }
        }

        // ==================== 存储容器排序 ====================

        public void SortStorage(InteractableStorage storage)
        {
            Items storageItems = storage.items;
            Player player = storage.opener;
            List<ItemJarSnapshot> items = new List<ItemJarSnapshot>();

            // 收集快照并移除
            foreach (var jar in storageItems.items.ToList())
            {
                items.Add(new ItemJarSnapshot(jar));
                storageItems.removeItem(storageItems.items.IndexOf(jar));
            }

            items = SortItems(items);
            List<ItemJarSnapshot> remainingItems = PlaceItemsInStorage(storageItems, items);

            // 剩余的放回背包
            if (remainingItems.Count > 0 && player != null)
            {
                List<ItemJarSnapshot> backpackRemaining = PlaceItemsBack(player, remainingItems);

                foreach (var snap in backpackRemaining)
                {
                    DropItem(player, snap.Item);
                }
            }
        }

        // ==================== 排序逻辑 ====================

        private List<ItemJarSnapshot> SortItems(List<ItemJarSnapshot> items)
        {
            return items.OrderBy(snap =>
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

        private List<ItemJarSnapshot> PlaceItemsBack(Player player, List<ItemJarSnapshot> items)
        {
            List<ItemJarSnapshot> remaining = new List<ItemJarSnapshot>();

            foreach (var snap in items)
            {
                bool placed = false;

                for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.STORAGE && !placed; page++)
                {
                    Items pageItems = player.inventory.items[page];
                    if (pageItems == null) continue;

                    // 尝试在 (0,0) 位置添加——Items.addItem 会自动处理旋转
                    // 遍历找到第一个空位
                    for (byte y = 0; y < pageItems.height && !placed; y++)
                    {
                        for (byte x = 0; x < pageItems.width && !placed; x++)
                        {
                            if (IsPositionFree(pageItems, x, y, snap.SizeX, snap.SizeY, snap.Rot))
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

        private List<ItemJarSnapshot> PlaceItemsInStorage(Items storageItems, List<ItemJarSnapshot> items)
        {
            List<ItemJarSnapshot> remaining = new List<ItemJarSnapshot>();

            foreach (var snap in items)
            {
                bool placed = false;

                for (byte y = 0; y < storageItems.height && !placed; y++)
                {
                    for (byte x = 0; x < storageItems.width && !placed; x++)
                    {
                        if (IsPositionFree(storageItems, x, y, snap.SizeX, snap.SizeY, snap.Rot))
                        {
                            storageItems.addItem(x, y, snap.Rot, snap.Item);
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

        private bool IsPositionFree(Items items, byte x, byte y, byte sizeX, byte sizeY, byte rot)
        {
            // 旋转：rot=1 时交换宽高
            byte w = rot == 1 ? sizeY : sizeX;
            byte h = rot == 1 ? sizeX : sizeY;

            if (x + w > items.width || y + h > items.height) return false;

            for (byte dy = 0; dy < h; dy++)
            {
                for (byte dx = 0; dx < w; dx++)
                {
                    // 检查该位置是否被占用
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

            Vector3 position = player.transform.position;
            position.y += 1f;

            ItemManager.dropItem(item, position, true, false, true);
        }
    }

    /// <summary>
    /// 物品快照——保存排序前的物品和位置信息
    /// </summary>
    public class ItemJarSnapshot
    {
        public Item Item;
        public byte OriginalPage;
        public byte SizeX;
        public byte SizeY;
        public byte Rot;

        public ItemJarSnapshot(ItemJar jar, byte page = 0)
        {
            Item = jar.item;
            OriginalPage = page;
            SizeX = jar.size_x;
            SizeY = jar.size_y;
            Rot = jar.rot;
        }
    }
}
