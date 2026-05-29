using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
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
            validProcesses = properties["validProcesses"].AsObject<List<ProcessableProperties>>();
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
                    if (p.Name == "unnamed" || p.FromModID == "modinotprovided")
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
                                processedStack.Resolve(api.World, "processedStack of item ", this.collObj.Code);
                            }
                        });
                    }
                }
            }
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

            if (!TrySelectBestProcess(be, slot, byPlayer, blockSel))
            {
                return false;
            }

            if (byPlayer.Entity.Api is ICoreClientAPI capi)
            {
                capi.TriggerIngameError(this, "craftingmessage", Lang.Get(curProcess.FromModID + ":" + curProcess.Name + "recipe"));
            }


            if (curProcess.ProcessedStacks != null || curProcess.RemainingItem != null)
            {
                be.Api.World.PlaySoundAt(curProcess.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
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
            if (curProcess.ProcessingAnimationCode != null && byPlayer.Entity.World is IClientWorldAccessor)
            {
                byPlayer.Entity.StartAnimation(curProcess.ProcessingAnimationCode);
            }
            if (be.Api.World.Rand.NextDouble() < 0.05)
            {
                be.Api.World.PlaySoundAt(curProcess.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
            }
            if (be.Api.World.Side == EnumAppSide.Client && be.Api.World.Rand.NextDouble() < 0.25)
            {
                BlockDropItemStack[] processedStacks = curProcess.ProcessedStacks;
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
                    JsonItemStack remainingItem = curProcess.RemainingItem;
                    itemStack2 = ((remainingItem != null) ? remainingItem.ResolvedItemstack : null);
                }
                if (itemStack2 != null)
                {
                    IWorldAccessor world = be.Api.World;
                    Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
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
                    world.SpawnCubeParticles(pos, itemStack3 ?? curProcess.RemainingItem.ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0f, 1f, 0f));
                }
            }
            return secondsUsed < curProcess.ProcessTime;
        }

        // Token: 0x060013D1 RID: 5073 RVA: 0x000A8768 File Offset: 0x000A6968
        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            byPlayer.Entity.StopAnimation(curProcess.ProcessingAnimationCode);
            if (!checkProcessingRequirements(byPlayer, be, blockSel))
            {
                curProcess = null;
                return;
            }
            if (secondsUsed > curProcess.ProcessTime - 0.05f && (curProcess.ProcessedStacks != null || curProcess.RemainingItem != null) && be.Api.World.Side == EnumAppSide.Server)
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
                if (curProcess.ProcessingItems?.Length > 0 && curProcess.ConsumedMainHandProcessItem > 0)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(curProcess.ConsumedMainHandProcessItem);
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                }
                if (curProcess.OffHandProcessingItems?.Length > 0 && curProcess.ConsumedOffHandProcessItem > 0)
                {
                    byPlayer.InventoryManager.OffhandHotbarSlot.TakeOut(curProcess.ConsumedOffHandProcessItem);
                    byPlayer.InventoryManager.OffhandHotbarSlot.MarkDirty();
                }
                
                be.Api.World.PlaySoundAt(curProcess.CompletionSound ?? curProcess.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
                curProcess = null;
            }
            
        }
        

        public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            byPlayer.Entity.StopAnimation(curProcess.ProcessingAnimationCode);
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
                {
                    if (be.Inventory[blockSel.SelectionBoxIndex].Empty || be.Inventory[blockSel.SelectionBoxIndex].StackSize < curProcess.ConsumedGroundStorageStackQty)
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
            bool mainItemRequired = curProcess.ProcessingItems?.Length > 0;
            bool offhandItemRequired = curProcess.OffHandProcessingItems?.Length > 0;

            // Always reset these before checking.
            curProcess.ConsumedMainHandProcessItem = 0;
            curProcess.ConsumedOffHandProcessItem = 0;
            ItemStack playerMainStack = inv.ActiveHotbarSlot.Itemstack;
            ItemStack playerOffHandStack = inv.OffhandHotbarSlot.Itemstack;

            if (mainItemRequired)
            {
                bool mainhandItemFound = false;
                foreach (JsonItemStack jsonStack in curProcess.ProcessingItems)
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

                foreach (JsonItemStack jsonStack in curProcess.OffHandProcessingItems)
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
            bool mainItemRequired = curProcess.ProcessingItems?.Length > 0;
            bool offhandItemRequired = curProcess.OffHandProcessingItems?.Length > 0;

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
            JsonItemStack remainingItem = curProcess.RemainingItem;
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
            if (slot.Itemstack.StackSize >= curProcess.ConsumedGroundStorageStackQty)
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
                            if (p.ProcessingItems?.Length > 0)
                            {
                                foreach (JsonItemStack jstack in p.ProcessingItems)
                                {
                                    jstack.Resolve(byPlayer.Entity.World, "barkWorldInteractionList");
                                    ItemStack stack = jstack.ResolvedItemstack;
                                    if (!(stack == null))
                                    {
                                        processItemList.Add(new ItemStack(stack.Item, stack.StackSize));
                                    }
                                }
                            }
                                interactionList.Add(new WorldInteraction
                                {
                                    ActionLangCode = p.interactionHelpCode,
                                    MouseButton = EnumMouseButton.Right,
                                    HotKeyCode = "shift",
                                    Itemstacks = processItemList?.Count > 0 ? processItemList?.ToArray() : ((p.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, p.Tool.Value))
                                });
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
                if (!IsCorrectStoredStackForThisBehavior(slot))
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

        private bool TryScoreProcess(
        ProcessableProperties process,
        BlockEntityContainer be,
        ItemSlot slot,
        IPlayer byPlayer,
        BlockSelection blockSel,
        out int score,
        out int mainConsumed,
        out int offhandConsumed
        )
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

            bool mainItemRequired = process.ProcessingItems?.Length > 0;
            bool offhandItemRequired = process.OffHandProcessingItems?.Length > 0;

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
                if (slot.Empty || slot.Itemstack.StackSize < process.ConsumedGroundStorageStackQty)
                {
                    return false;
                }

                score += 50 + process.ConsumedGroundStorageStackQty;
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

            // Required ALCMY crafting surface attribute.
            if (process.RequiredALCMYCraftingSurfaceAttributes != null)
            {
                if (!DoesBlockBelowHaveAnyCraftingSurface(be, process.RequiredALCMYCraftingSurfaceAttributes))
                {
                    return false;
                }

                score += 30 + process.RequiredALCMYCraftingSurfaceAttributes.Length;
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
                if (!TryMatchProcessingItem(process.ProcessingItems, mainStack, out mainConsumed))
                {
                    return false;
                }

                score += 100 + mainConsumed;
            }

            // Offhand processing item.
            if (offhandItemRequired)
            {
                if (!TryMatchProcessingItem(process.OffHandProcessingItems, offhandStack, out offhandConsumed))
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

        private bool TryMatchProcessingItem(JsonItemStack[] requiredItems, ItemStack heldStack, out int consumedQty)
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

            return false;
        }

        private bool DoesBlockBelowHaveAnyCraftingSurface(BlockEntityContainer be, string[] requiredSurfaces)
        {
            if (requiredSurfaces == null || requiredSurfaces.Length == 0)
            {
                return true;
            }

            Block belowBlock = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1));

            string[] surfaces = belowBlock.Attributes?["ALCMyCrafting"]?["surfaces"].AsArray<string>(null);

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
                "[ALCMy] Conflicting ground stored processes on collectible {0}. " +
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
            string name = string.IsNullOrEmpty(process.Name) ? "<unnamed>" : process.Name;
            string fromModID = string.IsNullOrEmpty(process.FromModID) ? "<unknown mod>" : process.FromModID;

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
            if (curProcess.RequiredALCMYCraftingSurfaceAttributes == null)
            {
                return true;
            }
            bool isSurfaceValid = false;
            Block block = byPlayer.Entity.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1));
            String[] testString = block.Attributes["ALCMyCrafting"]["surfaces"].AsArray<string>(null);
            byPlayer.Entity.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).Attributes["ALCMyCrafting"]["surfaces"].AsArray<string>(null)?.Foreach((string surface) =>
            {
                if (curProcess.RequiredALCMYCraftingSurfaceAttributes.Contains(surface))
                {
                    isSurfaceValid = true;
                }
            });
            return isSurfaceValid;

        }

        public class ProcessableProperties
        {
            public string Name = "unnamed";
            public string FromModID = "modidnotprovided";
            public float ProcessTime = 1;
            public BlockDropItemStack[] ProcessedStacks;
            public AssetLocation ProcessingSound;
            public string ProcessingAnimationCode;
            public JsonItemStack RemainingItem;
            public int ConsumedGroundStorageStackQty;
            public bool OffHandMustBeEmpty;
            public bool MainHandMustBeEmpty;
            public EnumTool? Tool;
            public int toolDamage = 1;
            public EnumTool? ToolOffhand;
            public int toolOffhandDamage = 1;
            public bool transferFreshness;
            public String[] RequiredALCMYCraftingSurfaceAttributes;
            public string craftingSurfaceErrorLangCode;
            public EnumBlockMaterial[] RequiredSurfaceMaterials;
            public string surfaceErrorLangCode;
            public AssetLocation CompletionSound;
            public JsonItemStack[] OffHandProcessingItems;
            public JsonItemStack[] ProcessingItems;
            public int ConsumedOffHandProcessItem;
            public int ConsumedMainHandProcessItem;
            public string interactionHelpCode;
            public string handbookProcessIntoTitle;
            public string handbookCreatedByTitle;
        }

        private List<ProcessableProperties> validProcesses;
        private ProcessableProperties curProcess;
    }

}
