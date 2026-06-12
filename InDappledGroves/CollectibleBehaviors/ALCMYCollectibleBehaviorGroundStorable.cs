using InDappledGroves.Util.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using static System.Collections.Specialized.BitVector32;
using static Vintagestory.GameContent.ALCMYCollectibleBehaviorGroundStoredProcessable;
namespace Vintagestory.GameContent
{
    public class ALCMYCollectibleBehaviorGroundStoredProcessable : CollectibleBehavior, IContainedInteractable
    {
        private readonly Dictionary<string, ActiveGroundProcess> activeProcessByEntityId = new();
        private List<ProcessableProperties> validProcesses;

        public ALCMYCollectibleBehaviorGroundStoredProcessable(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (properties["validProcesses"] == null)
            {
                return;
            }
            CollectibleObject fuckthis = collObj;
            System.Diagnostics.Debug.WriteLine(collObj.Code);
            List<ProcessableProperties> vProcess = new();
            foreach (JsonObject process in properties["validProcesses"].AsArray())
            {
                ProcessableProperties nextProcess = new ProcessableProperties(process);
                if(nextProcess.ProcessedStacks?.Length == 0 && nextProcess.RemainingItem == null)
                {
                    continue;
                }
                vProcess.Add(nextProcess);
            }
            if (vProcess.Count != 0) validProcesses = vProcess;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
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
                    if(p.ProcessedStacks?.Length == 0 && p.RemainingItem == null)
                    {
                        api.Logger.Debug(Lang.Get("indappledgroves:ALCMyGroundStoredProcessable-MissingOutput", p.Name, p.FromModID, this.collObj.Code));
                        validProcesses.Remove(p);
                        continue;
                    }
                    if (api != null)
                    {
                        api.Logger.Debug(Lang.Get("indappledgroves:ALCMyGroundStoredProcessable-Added", p.Name, p.FromModID, this.collObj.Code));
                    }
                    BlockDropItemStack[] processedStacks = p.ProcessedStacks;
                    JsonItemStack remainingItem = p.RemainingItem;
                    if(processedStacks?.Length == 0 && p.RemainingItem == null)
                    if (processedStacks != null)
                    {
                            for (int j = processedStacks.Length - 1; j >= 0; j--)
                            {
                            if (processedStacks[j] != null)
                            {
                                if (processedStacks[j].ResolvedItemstack == null)
                                {
                                    api.Logger.Debug(Lang.Get("indappledgroves:ALCMyGroundStoredProcessable-ItemStackUnresolvable", p.Name, p.FromModID, this.collObj.Code));
                                    validProcesses.Remove(p);
                                    continue;
                                    
                                }
                                processedStacks[j].Resolve(api.World, "processedStack groundstoredcollectableprocess " + p.Name + " on ", this.collObj.Code);                               
                            }
                        };
                    }
                    if (remainingItem != null)
                    {
                        remainingItem.Resolve(api.World, "remainingStack of groundstoredcollectableprocess " + p.Name + " on ", this.collObj.Code);
                        if(remainingItem.ResolvedItemstack == null)
                        {
                            api.Logger.Debug(Lang.Get("indappledgroves:ALCMyGroundStoredProcessable-MissingData", p.Name, p.FromModID, this.collObj.Code));
                            validProcesses.Remove(p);
                            continue;
                        }
                    }
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

            if (!TrySelectBestProcess(be, slot, byPlayer, blockSel, out ProcessableProperties curProcess, out int mainItemConsumed, out int offItemConsumed, out bool requirementsMet
               ))
            {
                return false;
            }

            if (!requirementsMet)
            {
                checkProcessingRequirements(be, slot, byPlayer, blockSel, curProcess);
                return false;
            }


            if (byPlayer.Entity.Api is ICoreClientAPI capi)
            {
                capi.TriggerIngameError(this, "craftingmessage", Lang.Get(curProcess.FromModID + ":" + curProcess.Name + "recipe"));
            }

            if (curProcess.ProcessedStacks == null && curProcess.RemainingItem == null)
            {
                return false;
            }

            activeProcessByEntityId[byPlayer.PlayerUID] = new ActiveGroundProcess(curProcess, mainItemConsumed, offItemConsumed);

            return true;
        }

        // Token: 0x060013D0 RID: 5072 RVA: 0x000A8584 File Offset: 0x000A6784
        public virtual bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return false;

            ActiveGroundProcess activeProcess = activeProcessByEntityId.TryGetValue(byPlayer.PlayerUID);
            if (activeProcess == null) return false;

            if (!checkProcessingRequirements(be, slot, byPlayer, blockSel, activeProcess.Process))
            {
                StopProcessingAnimations(byPlayer, activeProcess);
                activeProcessByEntityId.Remove(byPlayer.PlayerUID);
                return false;
            }
            IWorldAccessor world = be.Api.World;
            Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);

            string animCode = activeProcess.AnimationCode;

            if (!string.IsNullOrEmpty(animCode))
            {
                if (!byPlayer.Entity.AnimManager.IsAnimationActive(animCode))
                {
                    if (be.Pos.Y > byPlayer.Entity.Pos.Y)
                    {
                        byPlayer.Entity.StopAnimation("sneakidle");
                        byPlayer.Entity.StopAnimation("sneak");
                    }
                    else
                    {
                        byPlayer.Entity.StartAnimation("sneakidle");
                    }

                    byPlayer.Entity.StartAnimation(animCode);
                }

                RunningAnimation animState = byPlayer.Entity.AnimManager.GetAnimationState(animCode);

                if (animState != null && animState.Animation != null)
                {
                    float curFrame = animState.CurrentFrame;
                    float procAnimationFrameQty = animState.Animation.QuantityFrames;

                    float targetSoundFrame;

                    if (activeProcess.Process.ProcessingAnimationTargetFrame == 0f)
                    {
                        targetSoundFrame = soundFrames.TryGetValue(animCode) != 0f
                            ? soundFrames.TryGetValue(animCode)
                            : procAnimationFrameQty / 2;
                    }
                    else
                    {
                        targetSoundFrame =
                            activeProcess.Process.ProcessingAnimationTargetFrame > procAnimationFrameQty
                                ? procAnimationFrameQty / 2
                                : activeProcess.Process.ProcessingAnimationTargetFrame;
                    }

                    if (curFrame < activeProcess.LastAnimationFrame)
                    {
                        activeProcess.SoundPlayedThisCycle = false;
                    }

                    bool crossedTargetFrame = activeProcess.LastAnimationFrame < targetSoundFrame && curFrame >= targetSoundFrame;


                    if (!activeProcess.SoundPlayedThisCycle && crossedTargetFrame)
                    {
                        world.PlaySoundAt(activeProcess.Process.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
                        activeProcess.SoundPlayedThisCycle = true;
                        activeProcess.LastAnimationFrame = curFrame;
                        BlockDropItemStack[] processedStacks = activeProcess.Process.ProcessedStacks;

                        if (processedStacks != null && processedStacks.Length > 0)
                        {
                            foreach (BlockDropItemStack stack in processedStacks)
                            {
                                stack.Resolve(world, "processrecipe", stack.Code);
                                world.SpawnCubeParticles(pos, stack.ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0f, 3f, 0f));
                            }
                        }

                        ItemStack itemStack2 = activeProcess.Process.RemainingItem?.ResolvedItemstack;

                        if (itemStack2 != null)
                        {
                            ItemStack particleStack = itemStack2;

                            if (processedStacks != null && processedStacks.Length > 0)
                            {
                                particleStack = processedStacks[0]?.ResolvedItemstack ?? itemStack2;
                            }

                            world.SpawnCubeParticles(pos, particleStack, 0.25f, 4, 0.5f, byPlayer, new Vec3f(0f, 3f, 0f));
                        }

                        activeProcess.SoundPlayedThisCycle = true;
                    }
                    activeProcess.LastAnimationFrame = curFrame;
                }
            }

            return secondsUsed < activeProcess.Process.ProcessTime;
        }

        // Token: 0x060013D1 RID: 5073 RVA: 0x000A8768 File Offset: 0x000A6968
        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            ActiveGroundProcess activeProcess = activeProcessByEntityId.TryGetValue(byPlayer.PlayerUID);
            ProcessableProperties curProcess = activeProcess.Process;
            StopProcessingAnimations(byPlayer, activeProcess);
            if (curProcess == null) return;
            if (secondsUsed < curProcess.ProcessTime) return;
            if (be.Api.World.Side != EnumAppSide.Server) return;
            if (curProcess.ProcessedStacks == null && curProcess.RemainingItem == null) return;

            HandleProcessedStacks(byPlayer, slot, blockSel, be, curProcess);
            HandleRemainingItem(byPlayer, slot, blockSel, be, curProcess);
            if (slot.Itemstack?.Collectible.Code == collObj.Code && !curProcess.PlaceRemainingItemAsBlock)
            {
                slot.TakeOut(curProcess.ConsumedGroundStorageStackQty);
            }
            if (be.Inventory.Empty && !curProcess.PlaceRemainingItemAsBlock)
            {
                be.Api.World.BlockAccessor.SetBlock(0, be.Pos);
            }
            byPlayer.Entity.Api.World.BlockAccessor.MarkBlockDirty(blockSel.Position);
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
            if (curProcess.MainHandProcessingItemsByCode?.Length > 0 && activeProcess.MainhandConsumed > 0)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot?.TakeOut(activeProcess.MainhandConsumed);
                byPlayer.InventoryManager.ActiveHotbarSlot?.MarkDirty();
            }
            if (curProcess.OffHandProcessingItemsByCode?.Length > 0 && activeProcess.OffhandConsumed > 0)
            {
                byPlayer.InventoryManager.OffhandHotbarSlot?.TakeOut(activeProcess.OffhandConsumed);
                byPlayer.InventoryManager.OffhandHotbarSlot?.MarkDirty();
            }
            be.Api.World.PlaySoundAt(curProcess.CompletionSound ?? curProcess.ProcessingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 2f);
            activeProcessByEntityId.Remove(byPlayer.PlayerUID);
            be.MarkDirty(true);
        }

        public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            ActiveGroundProcess activeProcess = activeProcessByEntityId.TryGetValue(byPlayer.PlayerUID);

            if (activeProcess != null)
            {
                System.Diagnostics.Debug.WriteLine("OnInteractCancel");
                StopProcessingAnimations(byPlayer, activeProcess);
                activeProcessByEntityId.Remove(byPlayer.PlayerUID);
            }

            return false;

        }

        public virtual bool checkProcessingRequirements(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, ProcessableProperties curProcess)
        {
            var inv = byPlayer.InventoryManager;

            var mainSlot = inv.ActiveHotbarSlot;
            var offSlot = inv.OffhandHotbarSlot;

            ICoreClientAPI coreClientAPI = be.Api as ICoreClientAPI;
            if (!IsCorrectStoredStackForThisBehavior(slot))
            {
                return false;
            }
            if (!checkProcessingSurface(byPlayer, be, blockSel, coreClientAPI, inv, curProcess)) return false;
            if (!checkToolRequirements(byPlayer, be, blockSel, coreClientAPI, inv, curProcess)) return false;
            if (!checkheldItemRequirements(byPlayer, be, blockSel, coreClientAPI, inv, curProcess)) return false;
            if (!checkEmptyHandRequirements(byPlayer, be, blockSel, coreClientAPI, inv, curProcess)) return false;

            if (curProcess.ConsumedGroundStorageStackQty > 0)
            {
                if (slot.Empty || slot.StackSize < curProcess.ConsumedGroundStorageStackQty)
                {
                    if (coreClientAPI != null)
                    {
                        coreClientAPI.TriggerIngameError(this, "notenoughstackitems", Lang.Get("indappledgroves:groundprocessable-notenoughitemsinstack", slot.Itemstack.GetName().ToLower()));
                    }
                    return false;
                }
            }

            if (curProcess.RequiredActions != null)
            {
                foreach (EnumEntityAction action in curProcess.RequiredActions)
                {
                    if (!byPlayer.Entity.Controls.Flags[(int)action])
                    {
                        return false;
                    }
                }
            }
            return true;

        }

        private bool TrySelectBestProcess(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel,
            out ProcessableProperties curProcess, out int mainItemConsumed, out int offItemConsumed, out bool requirementsMet)
        {
            curProcess = null;
            mainItemConsumed = 0;
            offItemConsumed = 0;
            requirementsMet = false;

            if (validProcesses == null || validProcesses.Count == 0)
            {
                return false;
            }

            // This is global to the behavior, not process-specific.
            if (!IsCorrectStoredStackForThisBehavior(slot))
            {
                return false;
            }

            List<ProcessMatch> bestMatches = new();
            int bestScore = int.MinValue;

            foreach (ProcessableProperties process in validProcesses)
            {
                // Process-specific gates should not return false for the whole selection.
                // They should only skip this process.
                bool actionMatched = true;

                if (process.RequiredActions != null)
                {
                    foreach (EnumEntityAction action in process.RequiredActions)
                    {
                        if (!byPlayer.Entity.Controls.Flags[(int)action])
                        {
                            actionMatched = false;
                            break;
                        }
                    }
                }

                if (!actionMatched)
                {
                    continue;
                }

                if (process.RequiredClassTraits != null && byPlayer.Entity.World.Config.GetBool("classExclusiveRecipes", true))
                {
                    bool hasTrait = false;

                    foreach (string trait in process.RequiredClassTraits)
                    {
                        if (be.Api.ModLoader.GetModSystem<CharacterSystem>().HasTrait(byPlayer, trait))
                        {
                            hasTrait = true;
                            break;
                        }
                    }

                    if (!hasTrait)
                    {
                        continue;
                    }
                }

                if (process.RestrictedImmersedInMaterials.ToArray<EnumBlockMaterial>().Contains(
                    be.Api.World.BlockAccessor.GetBlock(be.Pos, 1).BlockMaterial
                ))
                {
                    continue;
                }

                if (!TryScoreProcess(process, be, slot, byPlayer, blockSel, out int score, out int mainConsumed, out int offhandConsumed, out bool processRequirementsMet
                ))
                {
                    continue;
                }

                ProcessMatch match = new ProcessMatch()
                {
                    Process = process,
                    Score = score,
                    MainConsumed = mainConsumed,
                    OffhandConsumed = offhandConsumed,
                    RequirementsMet = processRequirementsMet
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
            mainItemConsumed = bestMatch.MainConsumed;
            offItemConsumed = bestMatch.OffhandConsumed;
            requirementsMet = bestMatch.RequirementsMet;

            return true;
        }

        private bool TryScoreProcess(ProcessableProperties process, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
            BlockSelection blockSel, out int score, out int mainConsumed, out int offhandConsumed, out bool requirementsMet)
        {
            score = 0;
            mainConsumed = 0;
            offhandConsumed = 0;
            requirementsMet = true;

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

            // Invalid/conflicting process definitions should still be rejected outright.
            if (process.Tool != null && mainItemRequired) return false;
            if (process.ToolOffhand != null && offhandItemRequired) return false;
            if (process.Tool != null && process.MainHandMustBeEmpty) return false;
            if (process.ToolOffhand != null && process.OffHandMustBeEmpty) return false;
            if (mainItemRequired && process.MainHandMustBeEmpty) return false;
            if (offhandItemRequired && process.OffHandMustBeEmpty) return false;

            // Ground storage stack requirement.
            if (process.ConsumedGroundStorageStackQty > 0)
            {
                int stackSize = slot.Empty ? 0 : slot.Itemstack.StackSize;
                int requiredQty = process.ConsumedGroundStorageStackQty;

                if (process.ExactGroundStorageStackQtyRequired || process.ExactOrGreaterThanStorageQtyRequired)
                {
                    if (stackSize == requiredQty)
                    {
                        score += 100 + requiredQty;
                    }
                    else
                    {
                        requirementsMet = false;

                        int difference = Math.Abs(stackSize - requiredQty);
                        score += Math.Max(0, 80 - difference * 10);
                    }
                }
                else
                {
                    if (stackSize >= requiredQty)
                    {
                        score += 50 + requiredQty;
                    }
                    else
                    {
                        requirementsMet = false;
                        score += Math.Max(0, stackSize * 5);
                    }
                }
            }

            // Required block material under the ground storage block.
            if (process.RequiredSurfaceMaterials != null)
            {
                EnumBlockMaterial belowMaterial =
                    be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).BlockMaterial;

                if (process.RequiredSurfaceMaterials.Contains(belowMaterial))
                {
                    score += 30 + process.RequiredSurfaceMaterials.Length;
                }
                else
                {
                    requirementsMet = false;
                    score -= 30;
                }
            }

            // Required crafting surface.
            if (process.RequiredCraftingSurfaceByTag != null)
            {
                if (DoesBlockBelowHaveAnyCraftingSurface(be, process.RequiredCraftingSurfaceByTag))
                {
                    score += 30 + process.RequiredCraftingSurfaceByTag.Length;
                }
                else
                {
                    requirementsMet = false;
                    score -= 30;
                }
            }

            // Main hand tool.
            if (process.Tool != null)
            {
                if (inv.ActiveTool == process.Tool)
                {
                    score += 100;
                }
                else
                {
                    requirementsMet = false;
                    score -= 100;
                }
            }

            // Offhand tool.
            if (process.ToolOffhand != null)
            {
                if (inv.OffhandTool == process.ToolOffhand)
                {
                    score += 100;
                }
                else
                {
                    requirementsMet = false;
                    score -= 100;
                }
            }

            // Main hand processing item.
            if (mainItemRequired)
            {
                if (TryMatchProcessingItem(
                    process.MainHandProcessingItemsByCode,
                    process.MainHandProcessingItemsByKey,
                    mainStack,
                    out mainConsumed
                ))
                {
                    score += 100 + mainConsumed;
                }
                else
                {
                    requirementsMet = false;

                    if (HeldStackMatchesAnyRequiredCode(process.MainHandProcessingItemsByCode, mainStack))
                    {
                        // Right item, wrong quantity.
                        score += 40;
                    }
                    else
                    {
                        score -= 100;
                    }
                }
            }

            // Offhand processing item.
            if (offhandItemRequired)
            {
                if (TryMatchProcessingItem(
                    process.OffHandProcessingItemsByCode,
                    process.OffHandProcessingItemsByKey,
                    offhandStack,
                    out offhandConsumed
                ))
                {
                    score += 100 + offhandConsumed;
                }
                else
                {
                    requirementsMet = false;

                    if (HeldStackMatchesAnyRequiredCode(process.OffHandProcessingItemsByCode, offhandStack))
                    {
                        score += 40;
                    }
                    else
                    {
                        score -= 100;
                    }
                }
            }

            // Empty main hand.
            if (process.MainHandMustBeEmpty)
            {
                if (mainSlot.Empty)
                {
                    score += 60;
                }
                else
                {
                    requirementsMet = false;
                    score -= 60;
                }
            }

            // Empty offhand.
            if (process.OffHandMustBeEmpty)
            {
                if (offhandSlot.Empty)
                {
                    score += 60;
                }
                else
                {
                    requirementsMet = false;
                    score -= 60;
                }
            }

            return true;
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


            //if(processingItemByKey == null || processingItemByKey.Length == 0)
            //{
            //    return false;
            //}

            //foreach(JsonObject jsonObj in processingItemByKey) {
            //    if (heldStack.Collectible.Attributes["CraftingTags"].Exists && heldStack.Collectible.Attributes["CraftingTags"].AsArray<String>(null).Contains<string>(jsonObj["tag"].ToString()))
            //    {
            //        continue;
            //    }

            //    if (heldStack.StackSize < jsonObj["quantity"].AsInt())
            //    {
            //        continue;
            //    }

            //    consumedQty = jsonObj["quantity"].AsInt();
            //    return true;
            //}

            return false;
        }

        private bool HeldStackMatchesAnyRequiredCode(JsonItemStack[] requiredItems, ItemStack heldStack)
        {
            if (requiredItems == null || requiredItems.Length == 0 || heldStack == null)
            {
                return false;
            }

            foreach (JsonItemStack jsonStack in requiredItems)
            {
                if (jsonStack?.Code == null)
                {
                    continue;
                }

                if (jsonStack.Code.FirstCodePart() == heldStack.Collectible?.Code.FirstCodePart())
                {
                    return true;
                }
            }

            return false;
        }

        private class ProcessMatch
        {
            public ProcessableProperties Process;
            public int Score;
            public int MainConsumed;
            public int OffhandConsumed;
            public bool RequirementsMet;
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

        private bool checkheldItemRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv, ProcessableProperties curProcess)
        {
            bool mainItemRequired = curProcess.MainHandProcessingItemsByCode?.Length > 0/* || curProcess.MainHandProcessingItemsByKey?.Length > 0*/;
            bool offhandItemRequired = curProcess.OffHandProcessingItemsByCode?.Length > 0/* || curProcess.OffHandProcessingItemsByKey?.Length > 0*/;

            // Always reset these before checking.
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
                    break;
                }

                if (!offhandItemFound)
                {
                    return false;
                }
            }

            return true;
        }

        private bool checkProcessingSurface(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv, ProcessableProperties curProcess)
        {
            if (!this.canProcessOnSurfaceMaterial(be, byPlayer, curProcess))
            {

                if (capi != null)
                {
                    capi.TriggerIngameError(this, "needssolidsurface", Lang.Get(curProcess.surfaceErrorLangCode, Array.Empty<object>()));
                }
                return false;
            }
            if (!this.canProcessOnCraftingSurface(be, byPlayer, curProcess))
            {
                if (capi != null)
                {
                    capi.TriggerIngameError(this, "indappledgroves:needscraftingsurface", Lang.Get(curProcess.craftingSurfaceErrorLangCode, Array.Empty<object>()));
                }
                return false;
            }
            return true;
        }

        private bool checkToolRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv, ProcessableProperties curProcess)
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

        private bool checkEmptyHandRequirements(IPlayer byPlayer, BlockEntityContainer be, BlockSelection blockSel, ICoreClientAPI capi, IPlayerInventoryManager inv, ProcessableProperties curProcess)
        {
            var mainSlot = inv.ActiveHotbarSlot;
            var offSlot = inv.OffhandHotbarSlot;

            // If main hand must be empty
            if (curProcess.MainHandMustBeEmpty && !mainSlot.Empty)
            {
                if (!(be.Inventory[byPlayer.CurrentBlockSelection.SelectionBoxIndex].Itemstack.Collectible.Code == mainSlot.Itemstack.Collectible.Code))
                {
                    capi?.TriggerIngameError(this, "mainhandmustbeempty", Lang.Get("indappledgroves:groundprocessable-mainhandmustbeempty"));
                }
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

        public virtual bool canProcessOnSurfaceMaterial(BlockEntityContainer be, IPlayer byPlayer, ProcessableProperties curProcess)
        {
            if (curProcess.RequiredSurfaceMaterials == null)
            {
                return true;
            }
            EnumBlockMaterial belowMaterial = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy(1)).BlockMaterial;
            return curProcess.RequiredSurfaceMaterials.Contains(belowMaterial);
        }

        public virtual bool canProcessOnCraftingSurface(BlockEntityContainer be, IPlayer byPlayer, ProcessableProperties curProcess)
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

        private bool IsCorrectStoredStackForThisBehavior(ItemSlot slot)
        {
            if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null)
            {
                return false;
            }

            return slot.Itemstack.Collectible.Code.Equals(collObj.Code);
        }

        public virtual void HandleProcessedStacks(IPlayer byPlayer, ItemSlot slot, BlockSelection blockSel, BlockEntity be, ProcessableProperties curProcess)
        {
            BlockDropItemStack[] processedStacks = curProcess.ProcessedStacks;
            if (processedStacks != null)
            {
                processedStacks.Foreach(delegate (BlockDropItemStack processedStack)
                {
                    
                    ItemStack stack = processedStack.GetNextItemStack();
                    if (stack == null)
                    {
                        return;
                    }
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

                    be.Api.World.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.", new object[]
                    {
                            byPlayer.PlayerName,
                            stack.StackSize,
                            stack.Collectible.Code,
                            this.collObj.Code,
                            blockSel.Position
                    });
                    TreeAttribute tree2 = new TreeAttribute();
                    tree2["itemstack"] = new ItemstackAttribute(stack);
                    tree2["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    be.Api.World.Api.Event.PushEvent("onitemcollected", tree2);
                });
            }
        }

        public virtual void HandleRemainingItem(IPlayer byPlayer, ItemSlot slot, BlockSelection blockSel, BlockEntityContainer be, ProcessableProperties curProcess)
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
                slot.TakeOut(curProcess.ConsumedGroundStorageStackQty);
                be.MarkDirty();
                if (be.Inventory.Empty)
                {
                    remainingItem.Resolve(be.Api.World, "remainingItem of item ", this.collObj.Code);
                    Block block = remainingItem.ResolvedItemstack?.Block;

                    if (block != null)
                    {
                        be.Api.World.BlockAccessor.SetBlock(0, blockSel.Position);
                        be.MarkDirty(true);
                        block.DoPlaceBlock(byPlayer.Entity.Api.World, byPlayer, blockSel, itemStack);
                    }
                    return;
                }
                
            }
            //else if (!curProcess.PlaceRemainingItemAsBlock)
            //{
            //    ItemStack resolvedItemstack = remainingItem.ResolvedItemstack;
            //    itemStack = ((resolvedItemstack != null) ? resolvedItemstack.Clone() : null);
            //}
            if (curProcess.transferFreshness)
            {
                TransitionableProperties[] array = (itemStack != null) ? itemStack.Collectible.GetTransitionableProperties(be.Api.World, itemStack, null) : null;
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
                    CollectibleObject.CarryOverFreshness(be.Api, slot, itemStack, perishProps);
                }
            }
            if (slot.Itemstack.StackSize > curProcess.ConsumedGroundStorageStackQty || ((BlockEntityGroundStorage)be).DisplayedItems > 1 || curProcess.PlaceRemainingItemAsBlock)
            {
                if (itemStack != null)
                {
                   
                    int quantity = itemStack.StackSize;
                    if (!byPlayer.InventoryManager.TryGiveItemstack(itemStack, false))
                    {
                        be.Api.World.SpawnItemEntity(itemStack, blockSel.Position, null);
                    }
                    be.Api.World.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.", new object[]
                    {
                            byPlayer.PlayerName,
                            quantity,
                            itemStack.Collectible.Code,
                            this.collObj.Code,
                            blockSel.Position
                    });
                    TreeAttribute tree = new TreeAttribute();
                    tree["itemstack"] = new ItemstackAttribute(itemStack.Clone());
                    tree["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    be.Api.World.Api.Event.PushEvent("onitemcollected", tree);
                }
            }
            else
            {
                slot.Itemstack = itemStack;
                slot.MarkDirty();
            }
            slot.MarkDirty();
            be.MarkDirty(true, null);
        }

        // Token: 0x060013CE RID: 5070 RVA: 0x000A8430 File Offset: 0x000A6630

        private void StopProcessingAnimations(IPlayer byPlayer, ActiveGroundProcess activeProcess)
        {
            if (!byPlayer.Entity.Controls.Sneak)
            {
                byPlayer.Entity.StopAnimation("sneakidle");
            }

            if (!string.IsNullOrEmpty(activeProcess?.AnimationCode))
            {
                byPlayer.Entity.StopAnimation(activeProcess.AnimationCode);
            }
            System.Diagnostics.Debug.WriteLine(byPlayer.Entity.Api.Side.ToString());
        }

        public Dictionary<string, float> soundFrames = new Dictionary<string, float>
        {
            {"smithingwide", 15f },
            {"smithing", 15f},
            {"pickaxe", 15f },
            {"hammerhit", 15f },
            {"breakhand", 10f /*19f*/ },
            {"breaktool", 14f },
            {"AxeChop", 23f },
            {"AxeHit", 34f },
            {"Scythe", 26f },
            {"Shears", 22f },
            {"Hoe", 10f },
            {"Falx", 15f },
            {"Sickle", 20f },
            {"Dig", 15f },
            {"hammerandchisel", 15f },
            {"knifescrape", 10f }
        };

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
                if (validProcesses != null)
                {
                    foreach (ProcessableProperties p in validProcesses)
                    {

                        if (p.ExactGroundStorageStackQtyRequired == true && slot.StackSize != p.ConsumedGroundStorageStackQty) continue;
                        if (p.ExactOrGreaterThanStorageQtyRequired && !(slot.StackSize >= p.ConsumedGroundStorageStackQty)) continue;
                        List<ItemStack> processItemList = new();
                        if (p.MainHandProcessingItemsByCode?.Length > 0)
                        {
                            foreach (JsonItemStack jstack in p.MainHandProcessingItemsByCode)
                            {
                                if (
                                    jstack.Resolve(byPlayer.Entity.World, "barkWorldInteractionList")) ;
                                ItemStack stack = jstack.ResolvedItemstack;
                                if (!(stack == null))
                                {
                                    if (stack.Item != null)
                                    {
                                        processItemList.Add(new ItemStack(stack.Item, stack.StackSize));
                                    }
                                    else
                                        processItemList.Add(new ItemStack(stack.Block, stack.StackSize));
                                }
                            }
                        }
                        if (p.RequiredActions?.Length > 1)
                        {

                            List<string> actioncodes = new();
                            foreach (EnumEntityAction action in p.RequiredActions)
                            {
                                if (action.ToString().ToLower() == "shiftkey")
                                {
                                    actioncodes.Add("shift");
                                }
                                else if (action.ToString().ToLower() == "ctrlkey")
                                {
                                    actioncodes.Add("ctrl");
                                }
                                else
                                {
                                    actioncodes.Add(action.ToString().ToLower());
                                }
                            }
                            string[] actioncodestrings = actioncodes.ToArray<string>();
                            interactionList.Add(new WorldInteraction
                            {
                                ActionLangCode = p.interactionHelpCode,
                                MouseButton = EnumMouseButton.Right,
                                HotKeyCodes = actioncodestrings,
                                Itemstacks = processItemList?.Count > 0 ? processItemList?.ToArray() : ((p.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, p.Tool.Value)),
                                RequireFreeHand = p.MainHandMustBeEmpty
                            });
                        }
                        else if (p.RequiredActions != null)
                        {
                            string HKCode = p.RequiredActions[0].ToString().ToLower();
                            if (HKCode == "shiftkey")
                            {
                                HKCode = "shift";
                            }
                            else if (HKCode == "ctrlkey")
                            {
                                HKCode = "ctrl";
                            }
                            interactionList.Add(new WorldInteraction
                            {
                                ActionLangCode = p.interactionHelpCode,
                                MouseButton = EnumMouseButton.Right,
                                HotKeyCode = HKCode,
                                Itemstacks = processItemList?.Count > 0 ? processItemList?.ToArray() : ((p.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, p.Tool.Value)),
                                RequireFreeHand = p.MainHandMustBeEmpty
                            });
                        }
                        else
                        {
                            interactionList.Add(new WorldInteraction
                            {
                                ActionLangCode = p.interactionHelpCode,
                                MouseButton = EnumMouseButton.Right,
                                Itemstacks = processItemList?.Count > 0 ? processItemList?.ToArray() : ((p.Tool == null) ? null : ObjectCacheUtil.GetToolStacks(be.Api, p.Tool.Value)),
                                RequireFreeHand = p.MainHandMustBeEmpty
                            });
                        }
                    }
                ;
                    return interactionList.ToArray();
                }
            }
            return Array.Empty<WorldInteraction>();
        }
    }

    public class ActiveGroundProcess
    {
        public ProcessableProperties Process;
        public string AnimationCode;
        public int MainhandConsumed;
        public int OffhandConsumed;
        public bool SoundPlayedThisCycle;
        public float LastAnimationFrame;
        public ActiveGroundProcess(ProcessableProperties Process, int QtyConsumedMainItem, int QtyConsumedOffHandItem)
        {

            this.Process = Process;
            this.AnimationCode = Process.ProcessingAnimationCode;
            this.MainhandConsumed = QtyConsumedMainItem;
            this.OffhandConsumed = QtyConsumedOffHandItem;
            this.SoundPlayedThisCycle = false;
            this.LastAnimationFrame = 0f;
        }
    }

}
