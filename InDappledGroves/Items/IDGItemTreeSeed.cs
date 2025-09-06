using InDappledGroves.Util.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace InDappledGroves.Items
{
    public class IDGTreeSeed : ItemTreeSeed
    {
        // Token: 0x06001616 RID: 5654 RVA: 0x000D1A28 File Offset: 0x000CFC28
        public override void OnLoaded(ICoreAPI api)
        {
            this.isMapleSeed = (this.Variant["type"] == "maple" || this.Variant["type"] == "crimsonkingmaple");
            if (api.Side != EnumAppSide.Client)
            {
                return;
            }
            ICoreAPI api2 = api;
            this.interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "treeSeedInteractions", delegate
            {
                List<ItemStack> stacks = new List<ItemStack>();
                foreach (Block block in api.World.Blocks)
                {
                    if (!(block.Code == null) && block.EntityClass != null && block.Fertility > 0)
                    {
                        stacks.Add(new ItemStack(block, 1));
                    }
                }
                return new WorldInteraction[]
                {
                    new WorldInteraction
                    {
                        ActionLangCode = "heldhelp-plant",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        // Token: 0x06001618 RID: 5656 RVA: 0x000D1B7C File Offset: 0x000CFD7C
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || !byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }
            if (IDGTreeConfig.Current.SaplingSpacingEnabled)
            {
                bool foundSapling = false;

                //checkPos is adjusting to position of placement, rather than Pos of targetblock.
                BlockPos checkPos = blockSel.Position.UpCopy();
                byEntity.Api.World.BlockAccessor.WalkBlocks(checkPos.AddCopy(
                    -IDGTreeConfig.Current.MinHorizontalSaplingDistance,
                    -IDGTreeConfig.Current.MinVerticalSaplingDistance,
                    IDGTreeConfig.Current.MinHorizontalSaplingDistance),
                    checkPos.AddCopy(
                    IDGTreeConfig.Current.MinHorizontalSaplingDistance,
                    IDGTreeConfig.Current.MinVerticalSaplingDistance,
                    -IDGTreeConfig.Current.MinHorizontalSaplingDistance),
                    delegate (Block block, int x, int y, int z)
                {
                    if (block.Code.FirstCodePart() == "sapling" || block.FirstCodePart() == "log" && block.FirstCodePart(1) == "grown")
                    {
                        foundSapling = true;
                    }
                });

                if (foundSapling)
                {
                    if (api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError("ItemTreeSapling", "tooCloseToGrownTreeOrSapling", "Cannot Plant So Close To Another Tree or Sapling.");
                    }
                    handHandling = EnumHandHandling.NotHandled;

                    return;
                }
                ;
            }

            string treetype = this.Variant["type"];
            Block saplBlock = byEntity.World.GetBlock(AssetLocation.Create("sapling-" + treetype + "-free", this.Code.Domain));
            if (saplBlock != null)
            {
                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer)
                {
                    byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                }
                blockSel = blockSel.Clone();
                blockSel.Position.Up(1);
                string failureCode = "";
                if (!saplBlock.TryPlaceBlock(this.api.World, byPlayer, itemslot.Itemstack, blockSel, ref failureCode))
                {
                    ICoreClientAPI capi = this.api as ICoreClientAPI;
                    if (capi != null && failureCode != null && failureCode != "__ignore__")
                    {
                        capi.TriggerIngameError(this, failureCode, Lang.Get("placefailure-" + failureCode, Array.Empty<object>()));
                    }
                }
                else
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/dirt1"), (double)((float)blockSel.Position.X + 0.5f), (double)blockSel.Position.Y, (double)((float)blockSel.Position.Z + 0.5f), byPlayer, true, 32f, 1f);
                    EntityPlayer entityPlayer = byEntity as EntityPlayer;
                    IClientPlayer clientPlayer = ((entityPlayer != null) ? entityPlayer.Player : null) as IClientPlayer;
                    if (clientPlayer != null)
                    {
                        clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    }
                    bool flag;
                    if (byPlayer == null)
                    {
                        flag = true;
                    }
                    else
                    {
                        IWorldPlayerData worldData = byPlayer.WorldData;
                        EnumGameMode? enumGameMode = (worldData != null) ? new EnumGameMode?(worldData.CurrentGameMode) : null;
                        EnumGameMode enumGameMode2 = EnumGameMode.Creative;
                        flag = !(enumGameMode.GetValueOrDefault() == enumGameMode2 & enumGameMode != null);
                    }
                    if (flag)
                    {
                        itemslot.TakeOut(1);
                        itemslot.MarkDirty();
                    }
                    handHandling = EnumHandHandling.PreventDefault;
                }
            }
        }

        // Token: 0x06001619 RID: 5657 RVA: 0x000D1D5D File Offset: 0x000CFF5D
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return this.interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

        // Token: 0x04000BAE RID: 2990
        private WorldInteraction[] interactions;

        // Token: 0x04000BAF RID: 2991
        private bool isMapleSeed;
    }
}
