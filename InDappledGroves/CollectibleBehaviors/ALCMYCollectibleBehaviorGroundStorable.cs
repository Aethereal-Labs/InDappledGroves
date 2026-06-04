using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
            if(properties["validProcesses"] == null)
            {
                return;
            }          
            CollectibleObject fuckthis = collObj;
            System.Diagnostics.Debug.WriteLine(collObj.Code);
            List<ProcessableProperties> vProcess = new();
            foreach(JsonObject process in properties["validProcesses"].AsArray())
            {
                ProcessableProperties nextProcess = new ProcessableProperties(process);    
                vProcess.Add(nextProcess);
            }
            if(vProcess.Count != 0) validProcesses = vProcess;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (validProcesses?.Count == 0)
            {
                ILogger logger = api.Logger;
                api.Logger.Warning($"{collObj.Code} has no processedStacks or remainingItem specified for GroundStoredProcessable behavior");
            }
            if (validProcesses != null && validProcesses.Count != 0)
            {
                for (int i = validProcesses.Count - 1; i >= 0; i--)
                {
                    ProcessableProperties p = validProcesses[i];
                    if (p.Name == "unnamed" || p.FromModID == "modidnotprovided")
                    {
                        api.Logger.Debug(Lang.Get("indappledgroves:ALCMyGroundStoredProcessable-MissingData", p.Name, p.FromModID, this.collObj.Code));
                        validProcesses.Remove(p);
                        continue;
                    }
                    //if (sapi != null)
                    //{
                    //    sapi.Logger.Debug(Lang.Get("indappledgroves:ALCMyGroundStoredProcessable-Added", p.Name, p.FromModID, this.collObj.Code));
                    //}
                    BlockDropItemStack[] processedStacks = p.ProcessedStacks;
                    if (processedStacks != null)
                    {
                        processedStacks.Foreach(delegate (BlockDropItemStack processedStack)
                        {
                            if (processedStack != null)
                            {
                                processedStack.Resolve(api.World, "processedStack groundstoredcollectableprocess " + p.Name + " on ", this.collObj.Code);
                            }
                        });
                    }
                    if (p.RemainingItem != null)
                    {
                        p.RemainingItem.Resolve(api.World,"remainingStack of groundstoredcollectableprocess " + p.Name + " on ", this.collObj.Code);                    }
                }
            }
        }



        // Token: 0x060013CF RID: 5071 RVA: 0x000A847C File Offset: 0x000A667C
        public virtual bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {


            if (!be.Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            if (!TrySelectBestProcess(be, slot, byPlayer, blockSel))
            {
                return false;
            }

            foreach (EnumEntityAction action in curProcess.RequiredActions) { 
                if (!byPlayer.Entity.Controls.Flags[(int)action])
                {
                    return false;
                }
            }

            if (byPlayer.Entity.Api is ICoreClientAPI capi)
            {
                capi.TriggerIngameError(this, "craftingmessage", Lang.Get(curProcess.FromModID + ":" + curProcess.Name + "recipe"));
            }


            if (curProcess.ProcessedStacks != null || curProcess.RemainingItem != null)
            {
                return true;
            }

            return false;
        }

        // Token: 0x060013D0 RID: 5072 RVA: 0x000A8584 File Offset: 0x000A6784
        public virtual bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            
            bool testFlag = checkProcessingRequirements(byPlayer, be, blockSel);
            if (!testFlag) return testFlag;

            foreach (EnumEntityAction action in curProcess.RequiredActions)
            {
                if (!byPlayer.Entity.Controls.Flags[(int)action])
                {
                    return false;
                }
            }

            if (blockSel == null)
            {
                return false;
            }
            IWorldAccessor world = be.Api.World;
            Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);

            if (!string.IsNullOrEmpty(curProcess.ProcessingAnimationCode) && !byPlayer.Entity.AnimManager.IsAnimationActive(curProcess.ProcessingAnimationCode))
            {
                if (be.Pos.Y > byPlayer.Entity.Pos.Y)
                {
                    byPlayer.Entity.StopAnimation("sneakidle");
                }
                else if (be.Pos.Y <= byPlayer.Entity.Pos.Y)
                {
                    byPlayer.Entity.StartAnimation("sneakidle");
                }
                byPlayer.Entity.StartAnimation(curProcess.ProcessingAnimationCode);
            }
            float curFrame = byPlayer.Entity.AnimManager.GetAnimationState(curProcess.ProcessingAnimationCode).CurrentFrame;
            float procAnimationFrameQty = byPlayer.Entity.AnimManager.GetAnimationState(curProcess.ProcessingAnimationCode).Animation.QuantityFrames;
            float targetSoundFrame;
            if (curProcess.ProcessingAnimationTargetFrame == 0f)
            {
                targetSoundFrame = soundFrames.TryGetValue(curProcess.ProcessingAnimationCode) != 0f ? soundFrames.TryGetValue(curProcess.ProcessingAnimationCode) : procAnimationFrameQty / 2;
            } else
            {
                targetSoundFrame = curProcess.ProcessingAnimationTargetFrame > procAnimationFrameQty ? procAnimationFrameQty / 2 : curProcess.ProcessingAnimationTargetFrame;
            }


            if (curFrame > targetSoundFrame && curFrame< targetSoundFrame+2)
            {
                world.PlaySoundAt(curProcess.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);

                    BlockDropItemStack[] processedStacks = curProcess.ProcessedStacks;
                    if (processedStacks != null && processedStacks.Length > 0)
                    {
                        foreach (BlockDropItemStack stack in curProcess.ProcessedStacks)
                        {
                            world.SpawnCubeParticles(pos, stack.ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0f, 3f, 0f));
                        }
                    }

                    ItemStack itemStack2 = curProcess.RemainingItem?.ResolvedItemstack;
                    if (itemStack2 != null)
                    {


                        BlockDropItemStack[] processedStacks2 = curProcess.ProcessedStacks;
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
                        world.SpawnCubeParticles(pos, itemStack3 ?? curProcess.RemainingItem.ResolvedItemstack, 0.25f, 4, 0.5f, byPlayer, new Vec3f(0f, 3f, 0f));
                    }           
            }
            return secondsUsed < curProcess.ProcessTime;
        }

        // Token: 0x060013D1 RID: 5073 RVA: 0x000A8768 File Offset: 0x000A6968
        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            //byPlayer.Entity.AnimManager.ActiveAnimationsByAnimCode;
            if (!byPlayer.Entity.Controls.Sneak) byPlayer.Entity.StopAnimation("sneakidle");
            if (!string.IsNullOrEmpty(curProcess?.ProcessingAnimationCode))
            {
                byPlayer.Entity.StopAnimation(curProcess.ProcessingAnimationCode);
            }
            if (!checkProcessingRequirements(byPlayer, be, blockSel))
            {
                curProcess = null;
                return;
            }
            if (secondsUsed > curProcess.ProcessTime - 0.05f
                && (curProcess.ProcessedStacks != null || curProcess.RemainingItem != null)
                && be.Api.World.Side == EnumAppSide.Server)
            {
                
                HandleProcessedStacks(byPlayer, slot, blockSel, be);
                HandleRemainingItem(byPlayer, slot, blockSel, be);
                if (be.Inventory.Empty)
                {
                    be.Api.World.BlockAccessor.SetBlock(0, blockSel.Position);
                }
                if (curProcess.Tool != null)
                {
                    ItemSlot toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    ItemStack itemstack = toolSlot.Itemstack;
                    if (itemstack != null)
                    {
                        itemstack.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot, curProcess.toolDamage, true);
                    }
                }
                if (curProcess.ToolOffhand != null)
                {
                    ItemSlot toolSlot = byPlayer.InventoryManager.OffhandHotbarSlot;
                    ItemStack itemstack = toolSlot.Itemstack;
                    if (itemstack != null)
                    {
                        itemstack.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot, curProcess.toolOffhandDamage, true);
                    }
                }
                if (curProcess.MainHandProcessingItemsByCode?.Length > 0 && curProcess.ConsumedMainHandProcessItem > 0)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(curProcess.ConsumedMainHandProcessItem);
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                }
                if (curProcess.OffHandProcessingItemsByCode?.Length > 0 && curProcess.ConsumedOffHandProcessItem > 0)
                {
                    byPlayer.InventoryManager.OffhandHotbarSlot.TakeOut(curProcess.ConsumedOffHandProcessItem);
                    byPlayer.InventoryManager.OffhandHotbarSlot.MarkDirty();
                }

                be.Api.World.PlaySoundAt(curProcess.CompletionSound ?? curProcess.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 2f);
                be.MarkDirty();
                curProcess = null;
            }
        }
        

        public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            if (!byPlayer.Entity.Controls.Sneak) byPlayer.Entity.StopAnimation("sneakidle");
            if (!string.IsNullOrEmpty(curProcess.ProcessingAnimationCode))
            {
                byPlayer.Entity.StopAnimation(curProcess.ProcessingAnimationCode);
            }
            curProcess = null;
            return false;
        }

        

        public virtual bool checkProcessingRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel)
        {
            var inv = byPlayer.InventoryManager;

            var mainSlot = inv.ActiveHotbarSlot;
            var offSlot = inv.OffhandHotbarSlot;

            ICoreClientAPI coreClientAPI = be.Api as ICoreClientAPI;
            if (!IsCorrectStoredStackForThisBehavior(be.Inventory[blockSel.SelectionBoxIndex]))
            {
                return false;
            }
            if (!checkProcessingSurface(byPlayer, be, blockSel, coreClientAPI, inv)) return false;
            if (!checkToolRequirements(byPlayer,be, blockSel, coreClientAPI, inv)) return false;
            if (!checkheldItemRequirements(byPlayer, be, blockSel, coreClientAPI, inv)) return false;
            if (!checkEmptyHandRequirements(byPlayer, be, blockSel, coreClientAPI, inv)) return false;

            if (curProcess.ConsumedGroundStorageStackQty > 0)
            {
                if (be.Inventory[blockSel.SelectionBoxIndex].Empty || be.Inventory[blockSel.SelectionBoxIndex].StackSize < curProcess.ConsumedGroundStorageStackQty)
                {
                    //if (coreClientAPI != null)
                    //{
                    //    coreClientAPI.TriggerIngameError(this, "notenoughstackitems", Lang.Get("indappledgroves:groundprocessable-notenoughitemsinstack", be.Inventory[blockSel.SelectionBoxIndex].Itemstack.GetName().ToLower()));
                    //}
                    return false;
                }
            }          

            return true;
        }

        private bool checkheldItemRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv)
        {
            bool mainItemRequired = curProcess.MainHandProcessingItemsByCode?.Length > 0;
            bool offhandItemRequired = curProcess.OffHandProcessingItemsByCode?.Length > 0;

            // Always reset these before checking.
            curProcess.ConsumedMainHandProcessItem = 0;
            curProcess.ConsumedOffHandProcessItem = 0;
            ItemStack playerMainStack = inv.ActiveHotbarSlot.Itemstack;
            ItemStack playerOffHandStack = inv.OffhandHotbarSlot.Itemstack;

            if (mainItemRequired)
            {
                bool mainhandItemFound = false;
                foreach (JsonItemStack jsonStack in curProcess.MainHandProcessingItemsByCode)
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
                    curProcess.ConsumedMainHandProcessItem = jsonStack.StackSize;
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

                foreach (JsonItemStack jsonStack in curProcess.OffHandProcessingItemsByCode)
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
                    curProcess.ConsumedOffHandProcessItem = jsonStack.StackSize;
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
                    capi.TriggerIngameError(this, "needssolidsurface", Lang.Get(curProcess.surfaceErrorLangCode, Array.Empty<object>()));
                }
                return false;
            }
            if (!this.canProcessOnCraftingSurface(be, byPlayer))
            {
                if (capi != null)
                {
                    capi.TriggerIngameError(this, "indappledgroves:needscraftingsurface", Lang.Get(curProcess.craftingSurfaceErrorLangCode, Array.Empty<object>()));
                }
                return false;
            }
            return true;
        }

        private bool checkToolRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv)
        {
            bool mainItemRequired = curProcess.MainHandProcessingItemsByCode?.Length > 0;
            bool offhandItemRequired = curProcess.OffHandProcessingItemsByCode?.Length > 0;

            // Same hand cannot require both a tool and a processing item.
            if ((curProcess.Tool != null && mainItemRequired) || (curProcess.ToolOffhand != null && offhandItemRequired))
            {
                capi?.TriggerIngameError(this, "toolanditem", Lang.Get("indappledgroves:groundprocessable-toolitem-error"));
                return false;
            }

            // Main hand tool requirement
            if (curProcess.Tool != null && inv.ActiveTool != curProcess.Tool)
            {
                return false;
            }

            // Offhand tool requirement
            if (curProcess.ToolOffhand != null && inv.OffhandTool != curProcess.ToolOffhand)
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
            if (curProcess.MainHandMustBeEmpty && !mainSlot.Empty)
            {
                capi?.TriggerIngameError(this, "mainhandmustbeempty", Lang.Get("indappledgroves:groundprocessable-mainhandmustbeempty"));
                return false;
            }

            // If off hand must be empty
            if (curProcess.OffHandMustBeEmpty && !offSlot.Empty)
            {
                capi?.TriggerIngameError(this, "offhandmustbeempty", Lang.Get("indappledgroves:groundprocessable-offhandmustbeempty"));
                return false;
            }
            return true;
        }

        

       

        public virtual void HandleProcessedStacks(IPlayer byPlayer, ItemSlot slot, BlockSelection blockSel, BlockEntity be)
        {
            BlockDropItemStack[] processedStacks = curProcess.ProcessedStacks;
            if (processedStacks != null)
            {
                processedStacks.Foreach(delegate (BlockDropItemStack processedStack)
                {
                    processedStack.Resolve(be.Api.World, "processedStack of item ", this.collObj.Code);
                    ItemStack stack = processedStack.ResolvedItemstack;
                    if (stack == null)
                    {
                        return;
                    }
                    ItemStack origStack2 = stack.Clone();
                    int quantity2 = stack.StackSize;
                    if (curProcess.transferFreshness)
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
                        BlockFacing facing = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Opposite;
                        be.Api.World.SpawnItemEntity(stack, blockSel.Position, new Vec3d(facing.Normalf.X * 0.075, 0.03f, facing.Normalf.Z * 0.075f));
                    }
                    slot.TakeOut(curProcess.ConsumedGroundStorageStackQty);
                    slot.MarkDirty();
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
            
            JsonItemStack remainingItem = curProcess.RemainingItem;
            
            ItemStack itemStack = null;
            if (remainingItem == null)
            {
                itemStack = null;
                return;
            }
            curProcess.RemainingItem.Resolve(byPlayer.Entity.World, "resolvingcraftingstack");
            itemStack = remainingItem.ResolvedItemstack;
            if (curProcess.PlaceRemainingItemAsBlock)
            {
                remainingItem.Resolve(be.Api.World, "remainingItem of item ", this.collObj.Code);
                Block block = remainingItem.ResolvedItemstack?.Block;
                if (block != null)
                {
                    block.DoPlaceBlock(byPlayer.Entity.Api.World, byPlayer, blockSel, itemStack);
                }
                be.MarkDirty(true, null);
                return;
            }
            else
            {
                ItemStack resolvedItemstack = remainingItem.ResolvedItemstack;
                itemStack = ((resolvedItemstack != null) ? resolvedItemstack.Clone() : null);
            }
            ItemStack remainingStack = itemStack;
            if (curProcess.transferFreshness)
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
            if (slot.Itemstack.StackSize > curProcess.ConsumedGroundStorageStackQty)
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
                slot.TakeOut(curProcess.ConsumedGroundStorageStackQty);
            }
            else
            {
                slot.Itemstack = remainingStack;
            }
            be.MarkDirty(true, null);
        }

        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
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

                    List<WorldInteraction> interactionList = new();
                    if(validProcesses != null) { 
                        foreach (ProcessableProperties p in validProcesses)
                        {
                            List<ItemStack> processItemList = new();
                            if (p.MainHandProcessingItemsByCode?.Length > 0)
                            {
                                foreach (JsonItemStack jstack in p.MainHandProcessingItemsByCode)
                                {
                                if(
                                    jstack.Resolve(byPlayer.Entity.World, "barkWorldInteractionList"));
                                    ItemStack stack = jstack.ResolvedItemstack;
                                    if (!(stack == null))
                                    {
                                        processItemList.Add(new ItemStack(stack.Item, stack.StackSize));
                                    }
                                }
                            }
                            if(p.RequiredActions.Length > 1)
                            {

                                List<string> actioncodes = new();
                                foreach(EnumEntityAction action in p.RequiredActions)
                                {
                                    actioncodes.Add(action.ToString());
                                }
                                string[] actioncodestrings = actioncodes.ToArray<string>();
                                interactionList.Add(new WorldInteraction
                                    {
                                        ActionLangCode = p.interactionHelpCode,
                                        MouseButton = EnumMouseButton.Right,
                                        HotKeyCodes = actioncodestrings,
                                        Itemstacks = processItemList?.Count > 0 ? processItemList?.ToArray() : ((p.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, p.Tool.Value))
                                    });
                            } else
                            {

                            interactionList.Add(new WorldInteraction
                            {
                                    ActionLangCode = p.interactionHelpCode,
                                    MouseButton = EnumMouseButton.Right,
                                    HotKeyCode = p.RequiredActions[0].ToString(),
                                    Itemstacks = processItemList?.Count > 0 ? processItemList?.ToArray() : ((p.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, p.Tool.Value))
                            });
                            }
                                
                        };
                    return interactionList.ToArray();
                }

            }
            return Array.Empty<WorldInteraction>();
        }

        private bool TrySelectBestProcess(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            curProcess = null;
            if (validProcesses == null || validProcesses.Count == 0)
            {
                return false;
            }

            List<ProcessMatch> bestMatches = new();
            int bestScore = int.MinValue;

            foreach (ProcessableProperties process in validProcesses)
            {
                //TODO: Implement Multiple Recipe Types By Using Enum.GroundProcessType,
                //Valid types should be:
                //MultislotA (Using more than one ground storage slot for a single process);
                //MultislotB (Using more than one ground storage slot for a single process, with neighboring blocks being considered);
                foreach(EnumEntityAction action in process.RequiredActions){
                    if (!byPlayer.Entity.Controls.Flags[(int)action])
                    {
                        return false;
                    }
                }
                

                if (!IsCorrectStoredStackForThisBehavior(slot))
                {
                    return false;
                }

                if (process.RequiredClassTraits != null && byPlayer.Entity.World.Config.GetBool("classExclusiveRecipes", true))
                {
                    bool hasTrait = false;
                    foreach (string trait in process.RequiredClassTraits) {
                        if (be.Api.ModLoader.GetModSystem<CharacterSystem>().HasTrait(byPlayer, trait))
                        {
                            hasTrait = true;
                            break;
                        }
                    }
                    if (!hasTrait) return false;
                }

                if(process.RestrictedImmersedInMaterials.ToArray<EnumBlockMaterial>().Contains(be.Api.World.BlockAccessor.GetBlock(be.Pos, 1).BlockMaterial))
                {
                    return false;
                }

                if (!TryScoreProcess(process, be, slot, byPlayer, blockSel, out int score, out int mainConsumed, out int offhandConsumed))
                {
                    continue;
                }

                ProcessMatch match = new ProcessMatch()
                {
                    Process = process,
                    Score = score,
                    MainConsumed = mainConsumed,
                    OffhandConsumed = offhandConsumed
                };

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatches.Clear();
                    bestMatches.Add(match);
                    continue;
                }

                if (score == bestScore)
                {
                    bestMatches.Add(match);
                }
            }

            if (bestMatches.Count == 0)
            {
                return false;
            }

            if (bestMatches.Count > 1)
            {
                LogConflictingProcesses(be, bestMatches, bestScore);
                return false;
            }

            ProcessMatch bestMatch = bestMatches[0];

            curProcess = bestMatch.Process;
            curProcess.ConsumedMainHandProcessItem = bestMatch.MainConsumed;
            curProcess.ConsumedOffHandProcessItem = bestMatch.OffhandConsumed;

            return true;
        }

        private bool TryScoreProcess(ProcessableProperties process, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, out int score, out int mainConsumed, out int offhandConsumed)
        {
            score = 0;
            mainConsumed = 0;
            offhandConsumed = 0;

            if (process == null || blockSel == null)
            {
                return false;
            }

            IPlayerInventoryManager inv = byPlayer.InventoryManager;

            ItemSlot mainSlot = inv.ActiveHotbarSlot;
            ItemSlot offhandSlot = inv.OffhandHotbarSlot;

            ItemStack mainStack = mainSlot.Itemstack;
            ItemStack offhandStack = offhandSlot.Itemstack;

            bool mainItemRequired = process.MainHandProcessingItemsByCode?.Length > 0;
            bool offhandItemRequired = process.OffHandProcessingItemsByCode?.Length > 0;

            // Invalid/conflicting process definitions.
            if (process.Tool != null && mainItemRequired) return false;
            if (process.ToolOffhand != null && offhandItemRequired) return false;
            if (process.Tool != null && process.MainHandMustBeEmpty) return false;
            if (process.ToolOffhand != null && process.OffHandMustBeEmpty) return false;
            if (mainItemRequired && process.MainHandMustBeEmpty) return false;
            if (offhandItemRequired && process.OffHandMustBeEmpty) return false;

            // Ground storage stack requirement.
            if (process.ConsumedGroundStorageStackQty > 0)
            {
                if (slot.Empty)
                {
                    return false;
                }

                int stackSize = slot.Itemstack.StackSize;
                int requiredQty = process.ConsumedGroundStorageStackQty;

                if (process.ExactGroundStorageStackQtyRequired)
                {
                    if (stackSize != requiredQty)
                    {
                        return false;
                    }

                    score += 100 + requiredQty;
                }
                else
                {
                    if (stackSize < requiredQty)
                    {
                        return false;
                    }

                    score += 50 + requiredQty;
                }
            }

            // Required block material under the ground storage block.
            if (process.RequiredSurfaceMaterials != null)
            {
                EnumBlockMaterial belowMaterial = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).BlockMaterial;
                if (!process.RequiredSurfaceMaterials.Contains(belowMaterial))
                {
                    return false;
                }
                score += 30 + process.RequiredSurfaceMaterials.Length;
            }

            // Required CraftingTagssurfaceattribute.
            if (process.RequiredCraftingSurfaceByTag != null)
            {
                if (!DoesBlockBelowHaveAnyCraftingSurface(be, process.RequiredCraftingSurfaceByTag))
                {
                    return false;
                }
                score += 30 + process.RequiredCraftingSurfaceByTag.Length;
            }

            // Main hand tool.
            if (process.Tool != null)
            {
                if (inv.ActiveTool != process.Tool)
                {
                    return false;
                }

                score += 100;
            }

            // Offhand tool.
            if (process.ToolOffhand != null)
            {
                if (inv.OffhandTool != process.ToolOffhand)
                {
                    return false;
                }
                score += 100;
            }

            // Main hand processing item.
            if (mainItemRequired)
            {
                if (!TryMatchProcessingItem(process.MainHandProcessingItemsByCode, process.MainHandProcessingItemsByKey, mainStack, out mainConsumed))
                {
                    return false;
                }
                score += 100 + mainConsumed;
            }

            // Offhand processing item.
            if (offhandItemRequired)
            {
                if (!TryMatchProcessingItem(process.OffHandProcessingItemsByCode, process.OffHandProcessingItemsByKey, offhandStack, out offhandConsumed))
                {
                    return false;
                }
                score += 100 + offhandConsumed;
            }

            // Empty main hand.
            if (process.MainHandMustBeEmpty)
            {
                if (!mainSlot.Empty)
                {
                    return false;
                }
                score += 60;
            }

            // Empty offhand.
            if (process.OffHandMustBeEmpty)
            {
                if (!offhandSlot.Empty)
                {
                    return false;
                }
                score += 60;
            }

            return true;
        }

        private class ProcessMatch
        {
            public ProcessableProperties Process;
            public int Score;
            public int MainConsumed;
            public int OffhandConsumed;
        }

        private bool TryMatchProcessingItem(JsonItemStack[] requiredItems, JsonObject[] processingItemByKey, ItemStack heldStack, out int consumedQty)
        {
            consumedQty = 0;

            if (requiredItems == null || requiredItems.Length == 0)
            {
                return false;
            }

            if (heldStack == null)
            {
                return false;
            }

            foreach (JsonItemStack jsonStack in requiredItems)
            {
                if (jsonStack?.Code == null)
                {
                    continue;
                }

                if (jsonStack.Code.FirstCodePart() != heldStack.Collectible?.Code.FirstCodePart())
                {
                    continue;
                }

                if (heldStack.StackSize < jsonStack.StackSize)
                {
                    continue;
                }

                consumedQty = jsonStack.StackSize;
                return true;
            }


            if(processingItemByKey == null || processingItemByKey.Length == 0)
            {
                return false;
            }

            foreach(JsonObject jsonObj in processingItemByKey) {
                if (heldStack.Collectible.Attributes["CraftingTags"].Exists && heldStack.Collectible.Attributes["CraftingTags"].AsArray<String>(null).Contains<string>(jsonObj["tag"].ToString()))
                {
                    continue;
                }

                if (heldStack.StackSize < jsonObj["quantity"].AsInt())
                {
                    continue;
                }

                consumedQty = jsonObj["quantity"].AsInt();
                return true;
            }

            return false;
        }

        private bool DoesBlockBelowHaveAnyCraftingSurface(BlockEntityContainer be, string[] requiredSurfaces)
        {
            if (requiredSurfaces == null || requiredSurfaces.Length == 0)
            {
                return true;
            }

            Block belowBlock = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1));

            string[] surfaces = belowBlock.Attributes?["CraftingTags"]?["surfaces"].AsArray<string>(null);

            if (surfaces == null || surfaces.Length == 0)
            {
                return false;
            }

            foreach (string surface in surfaces)
            {
                if (requiredSurfaces.Contains(surface))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogConflictingProcesses(BlockEntityContainer be, List<ProcessMatch> matches, int score)
        {
            // Avoid duplicate warnings from both sides when possible.
            if (be.Api.Side != EnumAppSide.Server)
            {
                return;
            }

            string processList = string.Join(", ", matches.Select(match => FormatProcessIdentity(match.Process)));

            be.Api.Logger.Warning(
                "Conflicting ground stored processes on collectible {0}. " + 
                "Multiple validProcesses matched with the same best score of {1}. " +
                "The process selection is ambiguous and has been cancelled. " +
                "Conflicting processes: {2}. " +
                "Notify the mod creator responsible for these process definitions.",
                collObj.Code,
                score,
                processList
            );
        }

        private string FormatProcessIdentity(ProcessableProperties process)
        {
            string name = string.IsNullOrEmpty(process.Name) ? "unnamed" : process.Name;
            string fromModID = string.IsNullOrEmpty(process.FromModID) ? "" : process.FromModID;

            return $"name='{name}', fromModID='{fromModID}'";
        }
        private bool IsCorrectStoredStackForThisBehavior(ItemSlot slot)
        {
            if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null)
            {
                return false;
            }

            return slot.Itemstack.Collectible.Code.Equals(collObj.Code);
        }

        // Token: 0x060013CE RID: 5070 RVA: 0x000A8430 File Offset: 0x000A6630
        public virtual bool canProcessOnSurfaceMaterial(BlockEntityContainer be, IPlayer byPlayer)
        {
            if (curProcess.RequiredSurfaceMaterials == null)
            {
                return true;
            }
            EnumBlockMaterial belowMaterial = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).BlockMaterial;
            return curProcess.RequiredSurfaceMaterials.Contains(belowMaterial);
        }

        public virtual bool canProcessOnCraftingSurface(BlockEntityContainer be, IPlayer byPlayer)
        {
            if (curProcess.RequiredCraftingSurfaceByTag == null)
            {
                return true;
            }
            bool isSurfaceValid = false;
            Block block = byPlayer.Entity.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1));
            String[] testString = block.Attributes["CraftingTags"]["surfaces"].AsArray<string>(null);
            byPlayer.Entity.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).Attributes["CraftingTags"]["surfaces"].AsArray<string>(null)?.Foreach((string surface) =>
            {
                if (curProcess.RequiredCraftingSurfaceByTag.Contains(surface))
                {
                    isSurfaceValid = true;
                }
            });
            return isSurfaceValid;
            
        }

        public Dictionary<string, float> soundFrames = new Dictionary<string, float>
        {
            {"smithingwide", 15f },

        };

        public class ProcessableProperties
        {
            public string Name = "unnamed";
            public string FromModID = "modidnotprovided";
            public float ProcessTime = 1;
            public BlockDropItemStack[] ProcessedStacks = null;
            public AssetLocation ProcessingSound = "";
            public string ProcessingAnimationCode = "";
            public float ProcessingAnimationTargetFrame = 0f;
            public JsonItemStack RemainingItem = null;
            public int ConsumedGroundStorageStackQty = 1;
            public bool ExactGroundStorageStackQtyRequired = false;
            public bool PlaceRemainingItemAsBlock = false; //Replaces ground storage with output block.
            //public bool ReplaceSurfaceBlock = false; //Replaces block under ground storage with output block, PlaceRemainingItemAsBlock must be true,
            //and RequiredSurfaceMaterials or RequiredCraftingSurfaceByTag must be set.
            public bool OffHandMustBeEmpty = false;
            public bool MainHandMustBeEmpty = false;
            public string[] RequiredClassTraits = null;
            public EnumTool? Tool { get; set; } = null;
            public int toolDamage = 1;
            public EnumTool? ToolOffhand { get; set; } = null;
            public int toolOffhandDamage = 1;
            public bool transferFreshness = false;
            public String[] RequiredCraftingSurfaceByTag = null;
            public string craftingSurfaceErrorLangCode = "";
            public EnumBlockMaterial[] RequiredSurfaceMaterials = null;
            public EnumBlockMaterial[] RequiredImmersedInMaterials = null;
            public EnumBlockMaterial[] RestrictedImmersedInMaterials = new EnumBlockMaterial[] { EnumBlockMaterial.Water, EnumBlockMaterial.Lava };
            public string surfaceErrorLangCode = "";
            public AssetLocation CompletionSound = "";
            public JsonItemStack[] MainHandProcessingItemsByCode = null;
            public JsonObject[] MainHandProcessingItemsByKey = null;
            public JsonItemStack[] OffHandProcessingItemsByCode = null;
            public JsonObject[] OffHandProcessingItemsByKey = null;
            //public JsonObject[] GroundStorageProcessItems = null;
            public int ConsumedOffHandProcessItem = 0;
            public int ConsumedMainHandProcessItem = 0;
            public string interactionHelpCode = "";
            public string handbookProcessIntoTitle = "";
            public string handbookCreatedByTitle = "";
            public EnumEntityAction[] RequiredActions = new EnumEntityAction[] { EnumEntityAction.Sneak };
            
            public ProcessableProperties(JsonObject newProcess) {
                if (newProcess == null)
                {
                    
                    return;
                }
                try
                {
                    //The Nmae of the Recipe - Required for clarity in logging, processes without Name and ModID set will autoreject.
                    //Both must be set manually to ensure compliance.
                    Name = newProcess["name"]?.ToString() ?? Name;
                    FromModID = newProcess["fromModID"]?.ToString() ?? FromModID;

                    //Time in seconds for process to complete
                    ProcessTime = newProcess["processTime"].Exists ? newProcess["processTime"].AsFloat(1) : ProcessTime;
                    //BlockDropItemStacks that will be produced every time the process is completed.
                    ProcessedStacks = newProcess["processedStacks"].Exists ? newProcess["processedStacks"].AsObject<BlockDropItemStack[]>() : ProcessedStacks;
                    //The sound that will play during processing
                    ProcessingSound = newProcess["processingSound"].Exists ? new AssetLocation(newProcess["processingSound"].ToString()) : ProcessingSound;
                    //The animation that will play during processing
                    ProcessingAnimationCode = newProcess["processingAnimationCode"]?.ToString() ?? ProcessingAnimationCode;
                    //The target frame in the animation when the sound will play and particles will be produced.
                    ProcessingAnimationTargetFrame = newProcess["processingAnimationTargetFrame"]?.AsFloat() ?? ProcessingAnimationTargetFrame;
                    //This item will attempt to be placed in the GroundStorage or (if a block) will replace the ground storage based on PlaceRemainingItemAsBlock.
                    //Will TryGiveItemStack otherwise.
                    RemainingItem = newProcess["remainingItem"].Exists ? newProcess["remainingItem"].AsObject<JsonItemStack>() : null;
                    //The number of items that will be consumed from the groundStorage if GroundStorageProcessItems[] is null.
                    ConsumedGroundStorageStackQty = newProcess["consumedGroundStorageStackQty"].Exists ? newProcess["consumedGroundStorageStackQty"].AsInt(1) : ConsumedGroundStorageStackQty;
                    //If the stacksize of the GroundStorage slot (or slots if multislot) must match GroundStorageProcessItems or consumedGroundStorageStackQty exactly.
                    //Primarily used for PlaceItemAsBlock
                    ExactGroundStorageStackQtyRequired = newProcess["exactGroundStorageStackQtyRequired"].Exists ? newProcess["exactGroundStorageStackQtyRequired"].AsBool(false) : ExactGroundStorageStackQtyRequired;
                    //If true, the RemainingItem will be placed as a block in the world at the position of the ground storage instead of being given to the player. Will require an item that resolves to a block.
                    PlaceRemainingItemAsBlock = newProcess["placeRemainingItemAsBlock"].Exists ? newProcess["placeRemainingItemAsBlock"].AsBool(false) : PlaceRemainingItemAsBlock;
                    
                    //ReplaceSurfaceBlock 

                    //If the players offhand must be empty.
                    OffHandMustBeEmpty = newProcess["offHandMustBeEmpty"].Exists ? newProcess["offHandMustBeEmpty"].AsBool(false) : OffHandMustBeEmpty;
                    //If the players activeHotBarSlot must be empty.
                    MainHandMustBeEmpty = newProcess["mainHandMustBeEmpty"].Exists ? newProcess["mainHandMustBeEmpty"].AsBool(false) : MainHandMustBeEmpty;
                    
                    //Any class trait or traits the player must have to perform this process. Only one trait must be met, but multiple can be supplied. Can easily be altered
                    //To require multiple traits at once if we should wish it.
                    RequiredClassTraits = newProcess["requiredTraits"].Exists ? newProcess["requiredTraits"].AsArray<string>(null) : RequiredClassTraits;

                    //Tool required to be held in the main hand and the damage it will receive upon completion.
                    string toolCode = newProcess["tool"]?.ToString();
                    EnumTool tools = newProcess["tool"].AsObject<EnumTool>();
                    if (!string.IsNullOrWhiteSpace(toolCode))
                    {
                        if (Enum.TryParse(toolCode, true, out EnumTool parsedTool))
                        {
                            Tool = parsedTool;
                        }
                        else
                        {
                            // Optional: log or throw a clearer error
                            throw new Exception($"Invalid tool value '{toolCode}' in process JSON.");
                        }
                    }
                    toolDamage = newProcess["toolDamage"].Exists ? newProcess["toolDamage"].AsInt(1) : toolDamage;


                    //Tool that must be held in the offhand and the damage it will receive upon completion.
                    string toolOffHandCode = newProcess["toolOffhand"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(toolOffHandCode))
                    {
                        if (Enum.TryParse(toolOffHandCode, true, out EnumTool parsedTool))
                        {
                            ToolOffhand = parsedTool;
                        }
                        else
                        {
                            // Optional: log or throw a clearer error
                            throw new Exception($"Invalid tooloffhand value '{toolCode}' in process JSON.");
                        }
                    }
                  
                    //Transfer freshness of input to output.
                    transferFreshness = newProcess["transferFreshness"].Exists ? newProcess["transferFreshness"].AsBool(false) : transferFreshness;

                    //Required tag that must exist on crafting surface for this process to be used.
                    RequiredCraftingSurfaceByTag = newProcess["requiredCraftingSurfaceByTag"].Exists ? newProcess["requiredCraftingSurfaceByTag"].AsArray<string>(null) : RequiredCraftingSurfaceByTag;

                    //The error code that will show up if the crafting surface is wrong. Error codes are not currently working as a non-matching recipe will not show up at all.
                    craftingSurfaceErrorLangCode = newProcess["craftingSurfaceErrorLangCode"]?.ToString() ?? craftingSurfaceErrorLangCode;
                    
                    //Any valid materials for the block the GroundStorage is resting on for the process.
                    RequiredSurfaceMaterials = newProcess["requiredSurfaceMaterials"].Exists ? newProcess["requiredSurfaceMaterials"].AsArray<EnumBlockMaterial>(null) : RequiredSurfaceMaterials;
                    //Any materials the groundstorage must be immersed in for the process to work, defaults to null.
                    RequiredImmersedInMaterials = newProcess["requiredImmersedInMaterials"].Exists ? newProcess["requiredImmersedInMaterials"].AsArray<EnumBlockMaterial>(null) : RequiredImmersedInMaterials;
                    //Any materials the groundstorage must not be immersed in for the process to work, defaults to Water and Lava
                    RestrictedImmersedInMaterials = newProcess["RestrictedImmersedInMaterials"].AsArray<EnumBlockMaterial>(new EnumBlockMaterial[] { EnumBlockMaterial.Water, EnumBlockMaterial.Lava });
                    //The error code that will show up if the surface material lacks the correct tag or material. Error codes are not currently working as a non-matching recipe will not show up at all.
                    surfaceErrorLangCode = newProcess["surfaceErrorLangCode"]?.ToString() ?? surfaceErrorLangCode;
                    //The sound that plays upon completion of the process. No sound by default.
                    CompletionSound = newProcess["completionSound"].Exists ? new AssetLocation(newProcess["completionSound"].ToString()) : CompletionSound;    
                    //A list of itemstacks and stacksizes that can be held in the main hand, if listed, one must be in the activeHotbarSlot in the appropriate quantities to process.
                    MainHandProcessingItemsByCode = newProcess["mainHandProcessingItemsByCode"].Exists ? newProcess["mainHandProcessingItemsByCode"].AsObject<JsonItemStack[]>() : MainHandProcessingItemsByCode;
                    //A list of tags and stacksizes that indicate valid processing items for the main hand and the quantities desired.
                    MainHandProcessingItemsByKey = newProcess["mainHandProcessingItemsByKey"].Exists ? newProcess["mainHandProcessingItemsByKey"].AsObject<JsonObject[]>() : MainHandProcessingItemsByKey;
                    //A list of itemstacks and stacksizes that can be held in the offhand, if listed, one must be in the offhandHotbarSlot in the appropriate quantities to process.
                    OffHandProcessingItemsByCode = newProcess["offHandProcessingItemsByCode"].Exists ? newProcess["offHandProcessingItemsByCode"].AsObject<JsonItemStack[]>() : OffHandProcessingItemsByCode;
                    //A list of tags and stacksizes that indicate valid processing items for the offhand and the quantities desired.
                    OffHandProcessingItemsByKey = newProcess["offHandProcessingItemsByKey"].Exists ? newProcess["offHandProcessingItemsByKey"].AsObject<JsonObject[]>() : OffHandProcessingItemsByKey;
                    //This number is set once the itemstack held in the offhand has been determined. It is not set as part of describing the process.
                    ConsumedOffHandProcessItem = newProcess["consumedOffHandProcessItem"].Exists ? newProcess["consumedOffHandProcessItem"].AsInt(0) : ConsumedOffHandProcessItem;
                    //This number is set once the itemstack held in the main hand has been determined. It is not set as part of describing the process.
                    ConsumedMainHandProcessItem = newProcess["consumedMainHandProcessItem"].Exists ? newProcess["consumedMainHandProcessItem"].AsInt(0) : ConsumedMainHandProcessItem;

                    //(Unimplemented) GroundStorageProcessItems is A list of up to four itemstacks that will be looked for in a target groundstorage.
                    //If all items are present in the indicated quantities, will produce a desired output. This can require an exact quantity per itemstack or simply a minimum quantity,
                    //depending on the value of ExactGroundStorageStackQtyRequired. Should only produce one group of processed stacks. Mass production will require ongoing interaction.
                    //Optionally, at the whim of the gods, it can be set to produce as many groups of processed stacks as the quantity of input stacks allows for.  (Vinter: It's my preference that
                    //Only one group of processed stacks is output at a time.)

                    //The language code for the interaction help text that appears when looking at the ground storage with this process available.                   
                    interactionHelpCode = newProcess["interactionHelpCode"]?.ToString() ?? interactionHelpCode;
                    //If all items are present in the indicated quantities, will produce a desired output. This can require an exact quantity per itemstack or simply a minimum quantity,
                    //depending on the value of ExactGroundStorageStackQtyRequired. Should only produce one group of processed stacks. Mass production will require ongoing interaction.
                    //Optionally, at the whim of the gods, it can be set to produce as many groups of processed stacks as the quantity of input stacks allows for.  (Vinter: It's my preference that
                    //Only one group of processed stacks is output at a time.)
                    handbookProcessIntoTitle = newProcess["handbookProcessIntoTitle"]?.ToString() ?? handbookProcessIntoTitle;  
                    handbookCreatedByTitle = newProcess["handbookCreatedByTitle"]?.ToString() ?? handbookCreatedByTitle;
                    
                    string[] actionCodes = newProcess["requiredActions"]?.AsArray<String>();
                    if (actionCodes != null)
                    {
                        List<EnumEntityAction> actions = new(); 
                        for (int i = 0; i < actionCodes.Length; i++)
                        {
                            if (Enum.TryParse(actionCodes[i], true, out EnumEntityAction parsedAction))
                            {
                                actions.Add(parsedAction);
                            }
                            else
                            {
                                // Optional: log or throw a clearer error
                                throw new Exception($"Invalid action code '{actionCodes[i]}' in process JSON.");
                            }
                            RequiredActions = actions.ToArray();
                        }
                    }
                }
                catch (Exception e)
                {
                    // Catch and log any exceptions during deserialization to avoid crashing the game due to mod errors.
                    // This also helps identify issues with process definitions that may be causing valid processes to be ignored.
                    Console.WriteLine($"Error deserializing ProcessableProperties: {e}");
                }
            }
        }

        private List<ProcessableProperties> validProcesses;
        private ProcessableProperties curProcess;
    }

}
