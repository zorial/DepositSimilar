using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config; // Tutaj znajduje się Lang
using Vintagestory.API.Server;

namespace DepositSimilar
{
    public class DepositSimilarSystem : ModSystem
    {
        IClientNetworkChannel clientChannel = null!;
        IServerNetworkChannel serverChannel = null!;
        ICoreClientAPI clientApi = null!;
        ICoreServerAPI serverApi = null!;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("depositsimilar")
                .RegisterMessageType(typeof(DepositPacket));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientApi = api;
            clientChannel = api.Network.GetChannel("depositsimilar");

            // ZMIANA: Używamy Lang.Get dla nazwy skrótu w menu ustawień
            // Format klucza: "modid:klucz"
            string hotkeyName = Lang.Get("depositsimilar:depositsimilar-hotkey");
            api.Input.RegisterHotKey(
                "depositsimilar",
                hotkeyName,
                GlKeys.X,
                HotkeyType.GUIOrOtherControls,
                false, // Alt (wyłączony)
                true,  // Ctrl (WŁĄCZONY)
                false  // Shift (wyłączony)
            );
            api.Input.SetHotKeyHandler("depositsimilar", OnHotkeyPressed);
        }

        private bool OnHotkeyPressed(KeyCombination comb)
        {
            clientChannel.SendPacket(new DepositPacket());
            return true;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;
            serverChannel = api.Network.GetChannel("depositsimilar");
            serverChannel.SetMessageHandler<DepositPacket>(OnDepositPacketReceived);
        }

        private void OnDepositPacketReceived(IServerPlayer player, DepositPacket packet)
        {
            IInventory openedInv = null;

            HashSet<string> ignoredInventories = new HashSet<string>
            {
                "hotbar", "backpack", "player", "mouse", "creative",
                "character", "ground", "rot", "craftinggrid"
            };

            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                string invName = inv.ClassName.ToLower();
                if (ignoredInventories.Contains(invName)) continue;

                openedInv = inv;
                break;
            }

            // Grupa czatu, na której wyświetlimy wiadomość (EnumChatType.Notification - mały szary tekst)
            // W Singleplayerze Lang.Get użyje języka gry gracza.

            if (openedInv == null)
            {
                // ZMIANA: Wyślij wiadomość do gracza używając tłumaczenia
                string msg = Lang.GetL(player.LanguageCode, "depositsimilar:depositsimilar-msg-nochest");
                player.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
                return;
            }

            var playerBackpacks = player.InventoryManager.GetOwnInventory("backpack");
            var playerHotbar = player.InventoryManager.GetOwnInventory("hotbar");

            List<IInventory> sourceInventories = new List<IInventory> { playerBackpacks, playerHotbar };
            bool movedAny = false;
            int itemsMovedCount = 0;

            foreach (var sourceInv in sourceInventories)
            {
                if (sourceInv == null) continue;

                foreach (var playerSlot in sourceInv)
                {
                    if (playerSlot.Empty) continue;

                    if (InventoryContainsItemType(openedInv, playerSlot.Itemstack))
                    {
                        foreach (var targetSlot in openedInv)
                        {
                            int moved = playerSlot.TryPutInto(player.Entity.World, targetSlot, playerSlot.StackSize);
                            if (moved > 0)
                            {
                                movedAny = true;
                                itemsMovedCount += moved;
                                playerSlot.MarkDirty();
                                targetSlot.MarkDirty();
                                if (playerSlot.StackSize <= 0) break;
                            }
                        }
                    }
                }
            }

            if (movedAny)
            {
                player.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish1"), player.Entity);

                // ZMIANA: Wiadomość o sukcesie z liczbą (placeholder {0})
                string msg = Lang.GetL(player.LanguageCode, "depositsimilar:depositsimilar-msg-success", itemsMovedCount);
                player.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
            }
            else
            {
                // ZMIANA: Wiadomość o porażce
                string msg = Lang.GetL(player.LanguageCode, "depositsimilar:depositsimilar-msg-fail");
                player.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
            }
        }

        private bool InventoryContainsItemType(IInventory targetInv, ItemStack sourceStack)
        {
            foreach (var slot in targetInv)
            {
                if (slot.Empty) continue;
                if (slot.Itemstack.Satisfies(sourceStack))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [ProtoBuf.ProtoContract]
    public class DepositPacket
    {
    }
}