using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
namespace Vintagestory.GameContent
{
    public class ALCMYCollectibleBehaviorGroundStoredProcessable : CollectibleBehavior, IContainedInteractable
    {

        public ALCMYCollectibleBehaviorGroundStoredProcessable(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            this.interactionHelpCode = properties["interactionHelpCode"].AsString("blockhelp-processable-process");
            this.HandbookProcessIntoTitle = properties["handbookProcessIntoTitle"].AsString("groundstoredprocessesdesc-title");
            this.HandbookCreatedByTitle = properties["handbookCreatedByTitle"].AsString("handbook-createdby-groundstoredprocessing");
            this.ProcessTime = properties["processTime"].AsFloat(0f);
            this.ProcessedStacks = properties["processedStacks"].AsObject<BlockDropItemStack[]>(null);
            this.ProcessingAnimationCode = properties["processingAnimationCode"].AsString(null);

            string code = properties["processingSound"].AsString(null);
            if (code != null)
            {
                this.ProcessingSound = AssetLocation.Create(code, this.collObj.Code.Domain);
            }
            code = properties["completionSound"].AsString(null);
            if (code != null)
            {
                this.CompletionSound = AssetLocation.Create(code, this.collObj.Code.Domain);
            }
            this.ProcessingItems = properties["processingItems"].AsObject<JsonItemStack[]>(null);
            this.OffHandProcessingItems = properties["offhandProcessingItems"].AsObject<JsonItemStack[]>(null);
            this.RemainingItem = properties["remainingItem"].AsObject<JsonItemStack>(null);
            this.ConsumedGroundStorageStackQty = properties["consumedGroundStorageStackQty"].AsInt(0);
            this.OffHandMustBeEmpty = properties["offHandMustBeEmpty"].AsBool(false);
            this.MainHandMustBeEmpty = properties["mainHandMustBeEmpty"].AsBool(false);
            this.Tool = properties["tool"].AsObject<EnumTool?>(null);
            this.toolDamage = properties["toolDurabilityCost"].AsInt(1);
            this.ToolOffhand = properties["toolOffHand"].AsObject<EnumTool?>(null);
            this.toolOffhandDamage = properties["toolOffhandDurabilityCost"].AsInt(1);
            this.transferFreshness = properties["transferFreshness"].AsBool(false);
            this.RequiredALCMYCraftingSurfaceAttributes = properties["requiredALCMyCraftingSurfaceAttributes"].AsObject<String[]>(null);
            this.craftingSurfaceErrorLangCode = properties["craftingSurfaceErrorLangCode"].AsString("groundprocessable-wrongcraftingsurface");
            this.RequiredSurfaceMaterials = properties["requiredSurfaceMaterials"].AsObject<EnumBlockMaterial[]>(null);
            this.surfaceErrorLangCode = properties["surfaceErrorLangCode"].AsString("itemore-needssolid-error");
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (this.ProcessedStacks == null && this.OutputItem == null)
            {
                ILogger logger = api.Logger;
                api.Logger.Warning($"{collObj.Code} has no processedStacks or remainingItem specified for GroundStoredProcessable behavior");
            }
            if (this.Tool != null && this.ProcessingItems?.Length > 0)
            {
                api.Logger.Warning($"{collObj.Code} has both Tool & Processing Item Specified, Will Not Function Properly");
            }
            BlockDropItemStack[] processedStacks = this.ProcessedStacks;
            if (processedStacks != null)
            {
                processedStacks.Foreach(delegate (BlockDropItemStack processedStack)
                {
                    if (processedStack != null)
                    {
                        processedStack.Resolve(api.World, "processedStack of item ", this.collObj.Code);
                    }
                });
            }
            JsonItemStack outputItem = this.OutputItem;
            if (outputItem == null)
            {
                return;
            }
            OutputItem.Resolve(api.World, "outputItem of item ", this.collObj.Code, true);
        }

        // Token: 0x060013CE RID: 5070 RVA: 0x000A8430 File Offset: 0x000A6630
        public virtual bool canProcessOnSurfaceMaterial(BlockEntityContainer be, IPlayer byPlayer)
        {
            if (this.RequiredSurfaceMaterials == null)
            {
                return true;
            }
            EnumBlockMaterial belowMaterial = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).BlockMaterial;
            return this.RequiredSurfaceMaterials.Contains(belowMaterial);
        }

        public virtual bool canProcessOnCraftingSurface(BlockEntityContainer be, IPlayer byPlayer)
        {
            if (this.RequiredALCMYCraftingSurfaceAttributes == null)
            {
                return true;
            }
            bool isSurfaceValid = false;
            Block block = byPlayer.Entity.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1));
            String[] testString = block.Attributes["ALCMyCrafting"]["surfaces"].AsArray<string>(null);
            byPlayer.Entity.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).Attributes["ALCMyCrafting"]["surfaces"].AsArray<string>(null)?.Foreach((string surface) =>
            {
                if (this.RequiredALCMYCraftingSurfaceAttributes.Contains(surface))
                {
                    isSurfaceValid = true;
                }
            });
            return isSurfaceValid;

        }

        // Token: 0x060013CF RID: 5071 RVA: 0x000A847C File Offset: 0x000A667C
        public virtual bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                return false;
            }
            if (!be.Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            
            bool testFlag = checkProcessingRequirements(byPlayer, be, blockSel);
            if (!testFlag) return testFlag;
            
            

            if (this.ProcessedStacks != null || this.OutputItem != null)
            {
                be.Api.World.PlaySoundAt(this.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
                return true;
            }
            return false;
        }

        // Token: 0x060013D0 RID: 5072 RVA: 0x000A8584 File Offset: 0x000A6784
        public virtual bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool testFlag = checkProcessingRequirements(byPlayer, be, blockSel);
            if (!testFlag) return testFlag;
            
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                return false;
            }
            if (blockSel == null)
            {
                return false;
            }
            if (!this.canProcessOnSurfaceMaterial(be, byPlayer))
            {
                return false;
            }
            if (this.ProcessingAnimationCode != null && byPlayer.Entity.World is IClientWorldAccessor)
            {
                byPlayer.Entity.StartAnimation(this.ProcessingAnimationCode);
            }
            if (be.Api.World.Rand.NextDouble() < 0.05)
            {
                be.Api.World.PlaySoundAt(this.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
            }
            if (be.Api.World.Side == EnumAppSide.Client && be.Api.World.Rand.NextDouble() < 0.25)
            {
                BlockDropItemStack[] processedStacks = this.ProcessedStacks;
                ItemStack itemStack;
                if (processedStacks == null)
                {
                    itemStack = null;
                }
                else
                {
                    BlockDropItemStack blockDropItemStack = processedStacks[0];
                    itemStack = ((blockDropItemStack != null) ? blockDropItemStack.ResolvedItemstack : null);
                }
                ItemStack itemStack2;
                if ((itemStack2 = itemStack) == null)
                {
                    JsonItemStack remainingItem = this.RemainingItem;
                    itemStack2 = ((remainingItem != null) ? remainingItem.ResolvedItemstack : null);
                }
                if (itemStack2 != null)
                {
                    IWorldAccessor world = be.Api.World;
                    Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                    BlockDropItemStack[] processedStacks2 = this.ProcessedStacks;
                    ItemStack itemStack3;
                    if (processedStacks2 == null)
                    {
                        itemStack3 = null;
                    }
                    else
                    {
                        BlockDropItemStack blockDropItemStack2 = processedStacks2[0];
                        itemStack3 = ((blockDropItemStack2 != null) ? blockDropItemStack2.ResolvedItemstack : null);
                    }
                    world.SpawnCubeParticles(pos, itemStack3 ?? this.RemainingItem.ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0f, 1f, 0f));
                }
            }
            return secondsUsed < ProcessTime;
        }

        // Token: 0x060013D1 RID: 5073 RVA: 0x000A8768 File Offset: 0x000A6968
        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            byPlayer.Entity.StopAnimation(this.ProcessingAnimationCode);
            if (!checkProcessingRequirements(byPlayer, be, blockSel)) return;
            if (secondsUsed > this.ProcessTime - 0.05f && (this.ProcessedStacks != null || this.RemainingItem != null) && be.Api.World.Side == EnumAppSide.Server)
            {
                
                HandleProcessedStacks(byPlayer, slot, blockSel, be);
                HandleRemainingItem(byPlayer, slot, blockSel, be);
                if (be.Inventory.Empty)
                {
                    be.Api.World.BlockAccessor.SetBlock(0, blockSel.Position);
                }
                if (this.Tool != null)
                {
                    ItemSlot toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    ItemStack itemstack = toolSlot.Itemstack;
                    if (itemstack != null)
                    {
                        itemstack.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot, this.toolDamage, true);
                    }
                }
                if (this.ToolOffhand != null)
                {
                    ItemSlot toolSlot = byPlayer.InventoryManager.OffhandHotbarSlot;
                    ItemStack itemstack = toolSlot.Itemstack;
                    if (itemstack != null)
                    {
                        itemstack.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot, this.toolOffhandDamage, true);
                    }
                }
                if (this.ProcessingItems?.Length > 0 && ConsumedMainHandProcessItem > 0)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(ConsumedMainHandProcessItem);
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                }
                if (this.OffHandProcessingItems?.Length > 0 && ConsumedOffHandProcessItem > 0)
                {
                    byPlayer.InventoryManager.OffhandHotbarSlot.TakeOut(ConsumedOffHandProcessItem);
                    byPlayer.InventoryManager.OffhandHotbarSlot.MarkDirty();
                }
                
                be.Api.World.PlaySoundAt(this.CompletionSound ?? this.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
            }
        }
        

        public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            byPlayer.Entity.StopAnimation(this.ProcessingAnimationCode);
            return false;
        }

        

        public virtual bool checkProcessingRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel)
        {
            var inv = byPlayer.InventoryManager;

            var mainSlot = inv.ActiveHotbarSlot;
            var offSlot = inv.OffhandHotbarSlot;

            ICoreClientAPI coreClientAPI = be.Api as ICoreClientAPI;

            if (!checkProcessingSurface(byPlayer, be, blockSel, coreClientAPI, inv)) return false;
            if (!checkToolRequirements(byPlayer,be, blockSel, coreClientAPI, inv)) return false;
            if (!checkheldItemRequirements(byPlayer, be, blockSel, coreClientAPI, inv)) return false;
            if (!checkEmptyHandRequirements(byPlayer, be, blockSel, coreClientAPI, inv)) return false;

            if (ConsumedGroundStorageStackQty > 0)
            {
                {
                    if (be.Inventory[blockSel.SelectionBoxIndex].Empty || be.Inventory[blockSel.SelectionBoxIndex].StackSize < ConsumedGroundStorageStackQty)
                    {
                        if (coreClientAPI != null)
                        {
                            coreClientAPI.TriggerIngameError(this, "notenoughstackitems", Lang.Get("indappledgroves:groundprocessable-notenoughitemsinstack", be.Inventory[blockSel.SelectionBoxIndex].Itemstack.GetName().ToLower()));
                        }
                        return false;
                    }
                }
            }          

            return true;
        }

        private bool checkheldItemRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv)
        {
            bool mainItemRequired = this.ProcessingItems?.Length > 0;
            bool offhandItemRequired = this.OffHandProcessingItems?.Length > 0;

            // Always reset these before checking.
            this.ConsumedMainHandProcessItem = 0;
            this.ConsumedOffHandProcessItem = 0;
            ItemStack playerMainStack = inv.ActiveHotbarSlot.Itemstack;
            ItemStack playerOffHandStack = inv.OffhandHotbarSlot.Itemstack;

            if (mainItemRequired)
            {
                bool mainhandItemFound = false;
                foreach (JsonItemStack jsonStack in this.ProcessingItems)
                {
                    if (jsonStack.Code.FirstCodePart() != playerMainStack?.Collectible?.Code.FirstCodePart())
                    {
                        continue;
                    }

                    if (playerMainStack.StackSize < jsonStack.StackSize)
                    {
                        capi?.TriggerIngameError(
                            this,
                            "notenoughprocessitems",
                            Lang.Get("indappledgroves:groundprocessable-notenoughprocessitems", playerMainStack.GetName().ToLower(), jsonStack.StackSize)
                        );
                        return false;
                    }

                    mainhandItemFound = true;
                    this.ConsumedMainHandProcessItem = jsonStack.StackSize;
                    break;
                }

                if (!mainhandItemFound)
                {
                    return false;
                }
            }

            if (offhandItemRequired)
            {
                bool offhandItemFound = false;

                foreach (JsonItemStack jsonStack in this.OffHandProcessingItems)
                {
                    if (jsonStack.Code.FirstCodePart() != playerOffHandStack?.Collectible?.Code.FirstCodePart())
                    {
                        continue;
                    }

                    if (playerOffHandStack.StackSize < jsonStack.StackSize)
                    {
                        capi?.TriggerIngameError(
                            this,
                            "notenoughoffhandprocessitems",
                            Lang.Get("indappledgroves:groundprocessable-notenoughoffhandprocessitems", playerOffHandStack.GetName().ToLower(), jsonStack.StackSize)
                        );
                        return false;
                    }

                    offhandItemFound = true;
                    this.ConsumedOffHandProcessItem = jsonStack.StackSize;
                    break;
                }

                if (!offhandItemFound)
                {
                    return false;
                }
            }

            return true;
        }

        private bool checkProcessingSurface(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv)
        {
            if (!this.canProcessOnSurfaceMaterial(be, byPlayer))
            {

                if (capi != null)
                {
                    capi.TriggerIngameError(this, "needssolidsurface", Lang.Get(this.surfaceErrorLangCode, Array.Empty<object>()));
                }
                return false;
            }
            if (!this.canProcessOnCraftingSurface(be, byPlayer))
            {
                if (capi != null)
                {
                    capi.TriggerIngameError(this, "indappledgroves:needscraftingsurface", Lang.Get(this.craftingSurfaceErrorLangCode, Array.Empty<object>()));
                }
                return false;
            }
            return true;
        }

        private bool checkToolRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv)
        {
            bool mainItemRequired = this.ProcessingItems?.Length > 0;
            bool offhandItemRequired = this.OffHandProcessingItems?.Length > 0;

            // Same hand cannot require both a tool and a processing item.
            if ((this.Tool != null && mainItemRequired) || (this.ToolOffhand != null && offhandItemRequired))
            {
                capi?.TriggerIngameError(this, "toolanditem", Lang.Get("indappledgroves:groundprocessable-toolitem-error"));
                return false;
            }

            // Main hand tool requirement
            if (this.Tool != null && inv.ActiveTool != this.Tool)
            {
                return false;
            }

            // Offhand tool requirement
            if (this.ToolOffhand != null && inv.OffhandTool != this.ToolOffhand)
            {
                return false;
            }
            return true;
        }

        private bool checkEmptyHandRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv)
        {
            var mainSlot = inv.ActiveHotbarSlot;
            var offSlot = inv.OffhandHotbarSlot;

            // If main hand must be empty
            if (this.MainHandMustBeEmpty && !mainSlot.Empty)
            {
                capi?.TriggerIngameError(this, "mainhandmustbeempty", Lang.Get("indappledgroves:groundprocessable-mainhandmustbeempty"));
                return false;
            }

            // If off hand must be empty
            if (this.OffHandMustBeEmpty && !offSlot.Empty)
            {
                capi?.TriggerIngameError(this, "offhandmustbeempty", Lang.Get("indappledgroves:groundprocessable-offhandmustbeempty"));
                return false;
            }
            return true;
        }

        

       

        public virtual void HandleProcessedStacks(IPlayer byPlayer, ItemSlot slot, BlockSelection blockSel, BlockEntity be)
        {
            BlockDropItemStack[] processedStacks = this.ProcessedStacks;
            if (processedStacks != null)
            {
                processedStacks.Foreach(delegate (BlockDropItemStack processedStack)
                {
                    ItemStack stack = processedStack.GetNextItemStack(1f);
                    if (stack == null)
                    {
                        return;
                    }
                    ItemStack origStack2 = stack.Clone();
                    int quantity2 = stack.StackSize;
                    if (this.transferFreshness)
                    {
                        TransitionableProperties[] transitionableProperties2 = stack.Collectible.GetTransitionableProperties(be.Api.World, stack, null);
                        TransitionableProperties transitionableProperties3;
                        if (transitionableProperties2 == null)
                        {
                            transitionableProperties3 = null;
                        }
                        else
                        {
                            transitionableProperties3 = transitionableProperties2.FirstOrDefault((TransitionableProperties p) => p.Type == EnumTransitionType.Perish);
                        }
                        TransitionableProperties perishProps2 = transitionableProperties3;
                        if (perishProps2 != null)
                        {
                            CollectibleObject.CarryOverFreshness(be.Api, slot, stack, perishProps2);
                        }
                    }
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, false))
                    {
                        be.Api.World.SpawnItemEntity(stack, blockSel.Position, null);
                    }
                    be.Api.World.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.", new object[]
                    {
                            byPlayer.PlayerName,
                            quantity2,
                            stack.Collectible.Code,
                            this.collObj.Code,
                            blockSel.Position
                    });
                    TreeAttribute tree2 = new TreeAttribute();
                    tree2["itemstack"] = new ItemstackAttribute(origStack2.Clone());
                    tree2["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    be.Api.World.Api.Event.PushEvent("onitemcollected", tree2);
                });
            }
        }

        public virtual void HandleRemainingItem(IPlayer byPlayer, ItemSlot slot, BlockSelection blockSel, BlockEntityContainer be)
        {
            JsonItemStack remainingItem = this.RemainingItem;
            ItemStack itemStack;
            if (remainingItem == null)
            {
                itemStack = null;
            }
            else
            {
                ItemStack resolvedItemstack = remainingItem.ResolvedItemstack;
                itemStack = ((resolvedItemstack != null) ? resolvedItemstack.Clone() : null);
            }
            ItemStack remainingStack = itemStack;
            if (this.transferFreshness)
            {
                TransitionableProperties[] array = (remainingStack != null) ? remainingStack.Collectible.GetTransitionableProperties(be.Api.World, remainingStack, null) : null;
                TransitionableProperties transitionableProperties;
                if (array == null)
                {
                    transitionableProperties = null;
                }
                else
                {
                    transitionableProperties = array.FirstOrDefault((TransitionableProperties p) => p.Type == EnumTransitionType.Perish);
                }
                TransitionableProperties perishProps = transitionableProperties;
                if (perishProps != null)
                {
                    CollectibleObject.CarryOverFreshness(be.Api, slot, remainingStack, perishProps);
                }
            }
            if (slot.Itemstack.StackSize >= this.ConsumedGroundStorageStackQty)
            {
                if (remainingStack != null)
                {
                    ItemStack origStack = remainingStack.Clone();
                    int quantity = remainingStack.StackSize;
                    if (!byPlayer.InventoryManager.TryGiveItemstack(remainingStack, false))
                    {
                        be.Api.World.SpawnItemEntity(remainingStack, blockSel.Position, null);
                    }
                    be.Api.World.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.", new object[]
                    {
                            byPlayer.PlayerName,
                            quantity,
                            remainingStack.Collectible.Code,
                            this.collObj.Code,
                            blockSel.Position
                    });
                    TreeAttribute tree = new TreeAttribute();
                    tree["itemstack"] = new ItemstackAttribute(origStack.Clone());
                    tree["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    be.Api.World.Api.Event.PushEvent("onitemcollected", tree);
                }
                slot.TakeOut(ConsumedGroundStorageStackQty);
            }
            else
            {
                slot.Itemstack = remainingStack;
            }
            be.MarkDirty(true, null);
        }

        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.ProcessedStacks != null || this.RemainingItem != null)
            {
                if (!this.canProcessOnSurfaceMaterial(be, byPlayer))
                {
                    return Array.Empty<WorldInteraction>();
                }
                bool notProtected = true;
                if (be.Api.World.Claims != null)
                {
                    IClientWorldAccessor clientWorld = be.Api.World as IClientWorldAccessor;
                    if (clientWorld != null)
                    {
                        IClientPlayer player = clientWorld.Player;
                        if (player != null && player.WorldData.CurrentGameMode == EnumGameMode.Survival && clientWorld.Claims.TestAccess(clientWorld.Player, blockSel.Position, EnumBlockAccessFlags.Use) != EnumWorldAccessResponse.Granted)
                        {
                            notProtected = false;
                        }
                    }
                }
                if (notProtected)
                {

                    List<ItemStack> processStackList = new();
                    if (ProcessingItems?.Length > 0)
                    {
                        foreach (JsonItemStack jstack in this.ProcessingItems)
                        {
                            jstack.Resolve(byPlayer.Entity.World, "barkWorldInteractionList");
                            ItemStack stack = jstack.ResolvedItemstack;
                            if (!(stack == null))
                            {
                                processStackList.Add(new ItemStack(stack.Item, stack.StackSize));
                            }
                        }
                    }
                    return new WorldInteraction[]
                    {

                        new WorldInteraction
                        {

                            ActionLangCode = interactionHelpCode,
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = "shift",
                            Itemstacks = processStackList?.Count > 0 ? processStackList?.ToArray() : ((this.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, this.Tool.Value))
                            

                        }
                    };
                }
            }
            return Array.Empty<WorldInteraction>();
        }

        [DocumentAsJson("Recommended", "0", false)]
        public float ProcessTime;


        [DocumentAsJson("Required", "", false)]
        public BlockDropItemStack[] ProcessedStacks = Array.Empty<BlockDropItemStack>();
     
        [DocumentAsJson("Optional", "None", false)]
        public AssetLocation ProcessingSound;
        
        [DocumentAsJson("Optional", "None", false)]
        public string ProcessingAnimationCode;
        private int ConsumedGroundStorageStackQty;

        public bool OffHandMustBeEmpty { get; private set; }
        public bool MainHandMustBeEmpty { get; private set; }

        [DocumentAsJson("Optional", "None", false)]
        public JsonItemStack RemainingItem;

        public JsonItemStack OutputItem { get; private set; }
        public JsonItemStack[] ProcessingItems { get; private set; }
        public JsonItemStack[] OffHandProcessingItems { get; private set; }
        public int ConsumedOffHandProcessItem { get; private set; }

        [DocumentAsJson("Optional", "blockhelp-processable-process", false)]
        private string interactionHelpCode;
        
        [DocumentAsJson("Optional", "groundstoredprocessesdesc-title", false)]
        public string HandbookProcessIntoTitle;
        
        [DocumentAsJson("Optional", "handbook-createdby-groundstoredprocessing", false)]
        public string HandbookCreatedByTitle;

        [DocumentAsJson("Optional", "None", false)]
        public EnumTool? Tool;

        [DocumentAsJson("Optional", "1", false)]
        private int toolDamage = 1;

        public EnumTool? ToolOffhand { get; private set; }
        public int ConsumedMainHandProcessItem { get; private set; }

        private int toolOffhandDamage;
        [DocumentAsJson("Optional", "false", false)]
        private bool transferFreshness;

        public String[] RequiredALCMYCraftingSurfaceAttributes { get; private set; }
        public string craftingSurfaceErrorLangCode { get; private set; }

        [DocumentAsJson("Optional", "None", false)]
        public EnumBlockMaterial[] RequiredSurfaceMaterials;

        [DocumentAsJson("Optional", "itemore-needssolid-error", false)]
        private string surfaceErrorLangCode;

        [DocumentAsJson("Optional", "None", false)]
        public AssetLocation CompletionSound;
    }
}
