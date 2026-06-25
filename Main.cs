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

            if (p.interactableStorage != null)
            {
                SortStorage(p);
            }
            else
            {
                SortBackpack(p);
            }
        }

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

                if (!placed)
                {
                    remaining.Add(jar);
                }
            }

            return remaining;
        }

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

                if (!placed)
                {
                    remaining.Add(jar);
                }
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
}
