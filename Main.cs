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
            Logger.Log("InventorySorter loaded! /sortinv | /sortstorage");
        }

        protected override void Unload()
        {
            Instance = null;
            Logger.Log("InventorySorter unloaded!");
        }

        public void ExecuteCommand(UnturnedPlayer player, string command)
        {
            if (player == null) return;
            Player p = player.Player;
            if (p == null) return;

            if (p.interactableStorage != null)
            {
                SortStorage(p);
            }
            else
            {
                SortBackpack(p);
            }
        }

        // ==================== 背包排序 ====================

        public void SortBackpack(Player player)
        {
            PlayerInventory inventory = player.inventory;
            List<ItemJar> items = new List<ItemJar>();

            for (byte page = 0; page < PlayerInventory.PAGES; page++)
            {
                if (page == PlayerInventory.SPECIAL) continue;

                for (byte y = 0; y < PlayerInventory.HEIGHT; y++)
                {
                    for (byte x = 0; x < PlayerInventory.WIDTH; x++)
                    {
                        ItemJar jar = inventory.getItem(page, x, y);
                        if (jar != null)
                        {
                            items.Add(new ItemJar(jar.item, jar.x, jar.y));
                            inventory.removeItem(page, x, y);
                        }
                    }
                }
            }

            items = SortItems(items);
            List<ItemJar> remaining = PlaceItemsBack(inventory, items);

            foreach (var jar in remaining)
            {
                DropItem(player, jar.item);
            }
        }

        // ==================== 存储容器排序 ====================

        public void SortStorage(Player player)
        {
            InteractableStorage storage = player.interactableStorage;
            PlayerInventory inventory = player.inventory;
            List<ItemJar> items = new List<ItemJar>();

            for (byte y = 0; y < storage.height; y++)
            {
                for (byte x = 0; x < storage.width; x++)
                {
                    ItemJar jar = storage.getItem(x, y);
                    if (jar != null)
                    {
                        items.Add(new ItemJar(jar.item, jar.x, jar.y));
                        storage.removeItem(x, y);
                    }
                }
            }

            items = SortItems(items);
            List<ItemJar> remainingItems = PlaceItemsInStorage(storage, items);

            if (remainingItems.Count > 0)
            {
                List<ItemJar> backpackRemaining = PlaceItemsBack(inventory, remainingItems);

                foreach (var jar in backpackRemaining)
                {
                    DropItem(player, jar.item);
                }
            }
        }

        // ==================== 排序逻辑 ====================

        private List<ItemJar> SortItems(List<ItemJar> items)
        {
            return items.OrderBy(jar =>
            {
                if (jar.item == null || jar.item.asset == null) return 0;
                return jar.item.asset.id;
            }).ThenBy(jar =>
            {
                if (jar.item == null) return 0;
                return jar.item.amount;
            }).ToList();
        }

        // ==================== 放回背包 ====================

        private List<ItemJar> PlaceItemsBack(PlayerInventory inventory, List<ItemJar> items)
        {
            List<ItemJar> remaining = new List<ItemJar>();

            foreach (var jar in items)
            {
                bool placed = false;

                for (byte page = 0; page < PlayerInventory.PAGES && !placed; page++)
                {
                    if (page == PlayerInventory.SPECIAL) continue;

                    for (byte y = 0; y < PlayerInventory.HEIGHT && !placed; y++)
                    {
                        for (byte x = 0; x < PlayerInventory.WIDTH && !placed; x++)
                        {
                            if (inventory.getItem(page, x, y) == null)
                            {
                                inventory.addItem(page, jar.item, true);
                                inventory.sendSlot(page, x, y);
                                placed = true;
                            }
                        }
                    }
                }

                if (!placed) remaining.Add(jar);
            }

            return remaining;
        }

        // ==================== 放回存储容器 ====================

        private List<ItemJar> PlaceItemsInStorage(InteractableStorage storage, List<ItemJar> items)
        {
            List<ItemJar> remaining = new List<ItemJar>();

            foreach (var jar in items)
            {
                bool placed = false;

                for (byte y = 0; y < storage.height && !placed; y++)
                {
                    for (byte x = 0; x < storage.width && !placed; x++)
                    {
                        if (storage.getItem(x, y) == null)
                        {
                            storage.addItem(jar.item, true);
                            placed = true;
                        }
                    }
                }

                if (!placed) remaining.Add(jar);
            }

            return remaining;
        }

        private void DropItem(Player player, Item item)
        {
            if (item == null) return;
            Vector3 position = player.transform.position;
            position.y += 1f;
            ItemManager.dropItem(item, position, true, false, true);
        }
    }

    // ==================== 命令类 ====================

    public class SortInvCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Player; } }
        public string Name { get { return "sortinv"; } }
        public string Help { get { return "Sort your backpack and storage"; } }
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
        public string Help { get { return "Sort storage container"; } }
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
