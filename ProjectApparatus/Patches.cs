﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using GameNetcodeStuff;
using Dissonance;
using Dissonance.Integrations.Unity_NFGO;
//using Hax;
using HarmonyLib;
using Steamworks;
using TMPro;
using Unity.Netcode;
using Netcode.Transports.Facepunch;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.Rendering.HighDefinition;
using static IngamePlayerSettings;
using static UnityEngine.Rendering.DebugUI;
using Hax;
using System.Collections;

namespace ProjectApparatus
{
    [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
    class WaitForShipPatch
    {
        static IEnumerator Postfix(IEnumerator endOfGame)
        {
            while (endOfGame.MoveNext()) yield return endOfGame.Current;
            yield return new WaitUntil(() => Helper.StartOfRound?.shipIsLeaving is false);
            yield return new WaitForSeconds(5.0f); // Wait a bit to give it a chance to fix itself
            bool isLeverBroken = true;

            while (isLeverBroken)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(2.0f, 5.0f)); // Make lc-hax users not send it all at the same time
                if (Helper.StartOfRound?.travellingToNewLevel is true) continue;

                isLeverBroken =
                    Helper.StartOfRound?.inShipPhase is true &&
                    Helper.FindObject<StartMatchLever>()?.triggerScript.interactable is false;

                if (isLeverBroken)
                {
                    Helper.StartOfRound?.PlayerHasRevivedServerRpc();
                }
            }
        }
    }

    [HarmonyPatch(typeof(ManualCameraRenderer), nameof(ManualCameraRenderer.SwitchRadarTargetClientRpc))]
    class AntiRadar
    {
        static bool Prefix(ManualCameraRenderer __instance, int switchToIndex)
        {
            if (!Settings.Instance.settingsData.b_AntiRadar) return true;
            if (Helper.LocalPlayer?.playerClientId != (ulong)switchToIndex) return true;

            __instance.SwitchRadarTargetForward(true);

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "SendNewPlayerValuesServerRpc")]
    public class AntiKickPatch
    {
        static bool Prefix(PlayerControllerB __instance)
        {
            if (!Settings.AntiKick) return true;

            ulong[] playerSteamIds = new ulong[__instance.playersManager.allPlayerScripts.Length];

            for (int i = 0; i < __instance.playersManager.allPlayerScripts.Length; i++)
            {
                playerSteamIds[i] = __instance.playersManager.allPlayerScripts[i].playerSteamId;
            }

            playerSteamIds[__instance.playerClientId] = SteamClient.SteamId;

            // Using reflection to invoke the method "SendNewPlayerValuesClientRpc"
            typeof(PlayerControllerB)
                .GetMethod("SendNewPlayerValuesClientRpc", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(__instance, new object[] { playerSteamIds });

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Start")]
    public class PlayerControllerB_Start_Patch
    {
        private static void Prefix(PlayerControllerB __instance)
        {
            __instance.gameplayCamera.targetTexture.width = Settings.Instance.settingsData.b_CameraResolution ? Screen.width : 860;
            __instance.gameplayCamera.targetTexture.height = Settings.Instance.settingsData.b_CameraResolution ? Screen.height : 520;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class PlayerControllerB_Update_Patch
    {
        private static float oWeight = 1f;
        private static float oFOV = 66f;
        public static bool Prefix(PlayerControllerB __instance)
        {
            if (__instance == GameObjectManager.Instance.localPlayer)
            {
                oFOV = __instance.gameplayCamera.fieldOfView;

                __instance.disableLookInput = (__instance.quickMenuManager.isMenuOpen || Settings.Instance.b_isMenuOpen) ? true : false;
                Cursor.visible = (__instance.quickMenuManager.isMenuOpen || Settings.Instance.b_isMenuOpen) ? true : false;
                Cursor.lockState = (__instance.quickMenuManager.isMenuOpen || Settings.Instance.b_isMenuOpen) ? CursorLockMode.None : CursorLockMode.Locked;

                oWeight = __instance.carryWeight;
                if (Settings.Instance.settingsData.b_RemoveWeight)
                    __instance.carryWeight = 1f;
            }

            return true;
        }



        public static void Postfix(PlayerControllerB __instance)
        {
            if (__instance == GameObjectManager.Instance.localPlayer)
            {
                __instance.carryWeight = oWeight; // Restore weight after the speed has been calculated @ float num3 = this.movementSpeed / this.carryWeight;

                float flTargetFOV = Settings.Instance.settingsData.i_FieldofView;

                flTargetFOV = __instance.inTerminalMenu ? flTargetFOV - 6f :
                             (__instance.IsInspectingItem ? flTargetFOV - 20f :
                             (__instance.isSprinting ? flTargetFOV + 2f : flTargetFOV));


                __instance.gameplayCamera.fieldOfView = Mathf.Lerp(oFOV, flTargetFOV, 6f * Time.deltaTime);
            }
        }
    }

    //[HarmonyPatch(typeof(StartOfRound), "ManuallyEjectPlayersServerRpc")]
    //public static class ManuallyEjectPlayersServerRpcPatch
    //{
    //    static bool Prefix(ref NetworkManager __instance, ref __RpcExecStage __rpc_exec_stage, ref bool inShipPhase, ref bool firingPlayersCutsceneRunning, ref List<NetworkObject> fullyLoadedPlayers)
    //    {
    //        // Your code to replicate the necessary parts of the original method, excluding the owner check.
    //        // For example:
    //        if ((object)__instance == null || !__instance.IsListening)
    //        {
    //            return false;
    //        }
    //
    //        // Continue with the rest of the method's logic, modifying as necessary.
    //        // ...
    //
    //        // Returning false to skip the original implementation
    //        return false;
    //    }
    //}
    //
    [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
    public class PlayerControllerB_LateUpdate_Patch
    {
        private static float ojumpForce = 0f,
            minIntensity = 100f,
            maxIntensity = 10000f;

        public static void Postfix(PlayerControllerB __instance)
        {
            if (!__instance || !StartOfRound.Instance)
                return;

            if (Settings.Instance.b_DemiGod.ContainsKey(__instance) && Settings.Instance.b_DemiGod[__instance] && __instance.health < 100)
                __instance.DamagePlayerFromOtherClientServerRpc(-(100 - __instance.health), new Vector3(0, 0, 0), 0);

            if (Settings.Instance.b_SpamObjects.ContainsKey(__instance) && Settings.Instance.b_SpamObjects[__instance]
                && GameObjectManager.Instance.shipBuildModeManager)
            {
                foreach (PlaceableShipObject shipObject in GameObjectManager.Instance.shipObjects)
                {
                    NetworkObject networkObject = shipObject.parentObject.GetComponent<NetworkObject>();
                    if (StartOfRound.Instance.unlockablesList.unlockables[shipObject.unlockableID].inStorage)
                        StartOfRound.Instance.ReturnUnlockableFromStorageServerRpc(shipObject.unlockableID);

                    //Vector3 newRotation = new Vector3(45, 30, 60);
                    //__instance.transform.eulerAngles = newRotation;
                    //shipObject.mainMesh.transform.eulerAngles = newRotation;


                    GameObjectManager.Instance.shipBuildModeManager.PlaceShipObject(__instance.transform.position,
                        __instance.transform.eulerAngles,
                        shipObject);
                    GameObjectManager.Instance.shipBuildModeManager.CancelBuildMode(false);
                    GameObjectManager.Instance.shipBuildModeManager.PlaceShipObjectServerRpc(__instance.transform.position,
                        shipObject.mainMesh.transform.eulerAngles,
                        networkObject,
                        Settings.Instance.b_HideObjects ? (int)__instance.playerClientId : -1);
                }
            }

            if (Settings.Instance.b_SpamChat.ContainsKey(__instance) && Settings.Instance.b_SpamChat[__instance])
                PAUtils.SendChatMessage(Settings.Instance.str_ChatAsPlayer, (int)__instance.playerClientId);

            if (Settings.Instance.settingsData.b_AllJetpacksExplode)
            {
                if (__instance.currentlyHeldObjectServer != null && __instance.currentlyHeldObjectServer.GetType() == typeof(JetpackItem))
                {
                    JetpackItem Jetpack = (__instance.currentlyHeldObjectServer as JetpackItem);// fill it in
                    if (Jetpack != null)
                    {
                        PAUtils.SetValue(__instance, "jetpackPower", float.MaxValue, PAUtils.protectedFlags);
                        PAUtils.CallMethod(__instance, "ActivateJetpack", PAUtils.protectedFlags, null);
                        Jetpack.ExplodeJetpackServerRpc();
                        Jetpack.ExplodeJetpackClientRpc();
                    }
                }
            }

            PlayerControllerB Local = GameObjectManager.Instance.localPlayer;
            if (__instance.actualClientId != Local.actualClientId)
                return;

            if (Settings.Instance.settingsData.b_InfiniteStam)
            {
                __instance.sprintMeter = 1f;
                if (__instance.sprintMeterUI != null)
                    __instance.sprintMeterUI.fillAmount = 1f;
            }

            if (Settings.Instance.settingsData.b_InfiniteCharge)
            {
                if (__instance.currentlyHeldObjectServer != null
                    && __instance.currentlyHeldObjectServer.insertedBattery != null)
                {
                    __instance.currentlyHeldObjectServer.insertedBattery.empty = false;
                    __instance.currentlyHeldObjectServer.insertedBattery.charge = 1f;
                }
            }

            if (__instance.currentlyHeldObjectServer != null)
            {
                if (Settings.Instance.settingsData.b_ChargeAnyItem)
                    __instance.currentlyHeldObjectServer.itemProperties.requiresBattery = true;

                if (Settings.Instance.settingsData.b_OneHandAllObjects)
                {
                    __instance.twoHanded = false;
                    __instance.twoHandedAnimation = false;
                    __instance.currentlyHeldObjectServer.itemProperties.twoHanded = false;
                    __instance.currentlyHeldObjectServer.itemProperties.twoHandedAnimation = false;
                }
            }

            if (Settings.Instance.settingsData.b_WalkSpeed && !__instance.isSprinting)
                PAUtils.SetValue(__instance, "sprintMultiplier", Settings.Instance.settingsData.i_WalkSpeed, PAUtils.protectedFlags);

            if (Settings.Instance.settingsData.b_SprintSpeed && __instance.isSprinting)
                PAUtils.SetValue(__instance, "sprintMultiplier", Settings.Instance.settingsData.i_SprintSpeed, PAUtils.protectedFlags);

            __instance.climbSpeed = (Settings.Instance.settingsData.b_FastLadderClimbing) ? 100f : 4f;

            BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PAUtils.SetValue(__instance, "interactableObjectsMask",
                Settings.Instance.settingsData.b_InteractThroughWalls ? LayerMask.GetMask(new string[] { "Props", "InteractableObject" }) : 832,
                bindingAttr);

            __instance.grabDistance = Settings.Instance.settingsData.b_UnlimitedGrabDistance ? 9999f : 5f;

            if (ojumpForce == 0f)
                ojumpForce = __instance.jumpForce;
            else
                __instance.jumpForce = Settings.Instance.settingsData.b_JumpHeight ? Settings.Instance.settingsData.i_JumpHeight : ojumpForce;

            if (__instance.nightVision != null)
            {
                /* I see a lot of cheats set nightVision.enabled to false when the feature is off, this is wrong as the game sets it to true when you're in-doors. 
                   Also there's no reason to reset it as the game automatically sets it back every time Update is called. */

                if (Settings.Instance.settingsData.b_NightVision)
                    __instance.nightVision.enabled = true;

                __instance.nightVision.range = (Settings.Instance.settingsData.b_NightVision) ? 9999f : 12f;
                __instance.nightVision.intensity = (minIntensity + (maxIntensity - minIntensity) * (Settings.Instance.settingsData.i_NightVision / 100f));
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "PlayerHitGroundEffects")]
    public class PlayerControllerB_PlayerHitGroundEffects_Patch
    {
        public static bool Prefix(PlayerControllerB __instance)
        {
            if (__instance.actualClientId == GameObjectManager.Instance.localPlayer.actualClientId
                && Settings.Instance.settingsData.b_DisableFallDamage)
                __instance.takingFallDamage = false;

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "AllowPlayerDeath")]
    public class PlayerControllerB_AllowPlayerDeath_Patch
    {
        public static bool Prefix(PlayerControllerB __instance, ref bool __result)
        {
            if ((Settings.Instance.settingsData.b_GodMode || Features.Possession.possessedEnemy != null) 
                && __instance == GameObjectManager.Instance.localPlayer)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "CheckConditionsForEmote")]
    public class PlayerControllerB_CheckConditionsForEmote_Patch
    {
        public static bool Prefix(PlayerControllerB __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_TauntSlide)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    public class PlayerControllerB_UpdatePlayerPosition_Patch
    {
        private static Vector3 oPosition = new Vector3();
        private static bool oInElevator = false,
            oExhausted = false,
            oIsPlayerGrounded = false;

        [HarmonyPatch(typeof(PlayerControllerB), "UpdatePlayerPositionServerRpc")]
        class Server
        {
            private static void Prefix(PlayerControllerB __instance, ref Vector3 newPos, ref bool inElevator, ref bool exhausted, ref bool isPlayerGrounded)
            {

                bool localPlayerHandle = (__instance == GameObjectManager.Instance.localPlayer && (Settings.Instance.settingsData.b_Invisibility || Features.Possession.possessedEnemy != null));


                if (localPlayerHandle)
                {
                    oPosition = newPos;
                    oInElevator = inElevator;
                    oExhausted = exhausted;
                    oIsPlayerGrounded = isPlayerGrounded;

                    //PAUtils.SendChatMessage(Settings.Instance.settingsData.str_InvisibleDisconnect + "has disconnected.");
                    newPos = new Vector3(0, -100, 0);
                    inElevator = false;
                    exhausted = false;
                    isPlayerGrounded = true;
                }
            }
        }

        class Client
        {
            [HarmonyPatch(typeof(PlayerControllerB), "UpdatePlayerPositionClientRpc")]
            private static void Prefix(PlayerControllerB __instance, ref Vector3 newPos, ref bool inElevator, ref bool exhausted, ref bool isPlayerGrounded)
            {
                bool localPlayerHandle = (__instance == GameObjectManager.Instance.localPlayer && (Settings.Instance.settingsData.b_Invisibility || Features.Possession.possessedEnemy != null));

                if (localPlayerHandle)
                {
                    newPos = oPosition;
                    inElevator = oInElevator;
                    exhausted = oExhausted;
                    isPlayerGrounded = oIsPlayerGrounded;
                }
            }
        }
    }


    [HarmonyPatch(typeof(Landmine), "Update")]
    public class Landmine_Update_Patch
    {
        public static bool Prefix(Landmine __instance)
        {
            if (Settings.Instance.settingsData.b_SensitiveLandmines && !__instance.hasExploded)
            {
                foreach (PlayerControllerB plyr in GameObjectManager.Instance.players)
                {
                    if (plyr.actualClientId == GameObjectManager.Instance.localPlayer.actualClientId) continue;

                    Vector3 plyrPosition = plyr.transform.position,
                        minePosition = __instance.transform.position;

                    float distance = Vector3.Distance(plyrPosition, minePosition);
                    if (distance <= 4f)
                        __instance.ExplodeMineServerRpc();
                }
            }

            if (Settings.Instance.settingsData.b_LandmineEarrape)
                __instance.ExplodeMineServerRpc();

            return true;
        }
    }

    [HarmonyPatch(typeof(ShipBuildModeManager), "Update")]
    public class ShipBuildModeManager_Update_Patch
    {
        public static void Postfix(ShipBuildModeManager __instance)
        {
            if (Settings.Instance.settingsData.b_PlaceAnywhere)
            {
                PlaceableShipObject placingObject = (PlaceableShipObject)PAUtils.GetValue(__instance, "placingObject", PAUtils.protectedFlags);
                if (placingObject)
                {
                    placingObject.AllowPlacementOnCounters = true;
                    placingObject.AllowPlacementOnWalls = true;
                    PAUtils.SetValue(__instance, "CanConfirmPosition", true, PAUtils.protectedFlags);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ShipBuildModeManager), "PlayerMeetsConditionsToBuild")]
    public class ShipBuildModeManager_PlayerMeetsConditionsToBuild_Patch
    {
        public static bool Prefix(ShipBuildModeManager __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_PlaceAnywhere)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GrabbableObject), "RequireCooldown")]
    public class GrabbableObject_RequireCooldown_Patch
    {
        public static bool Prefix(GrabbableObject __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableInteractCooldowns)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GiftBoxItem))]
    class GiftBoxPatch
    {
        static bool LocalPlayerActivated { get; set; } = false;

        [HarmonyPatch(nameof(GiftBoxItem.ItemActivate))]
        static bool Prefix(GiftBoxItem __instance)
        {
            GiftBoxPatch.LocalPlayerActivated = true;
            __instance.OpenGiftBoxServerRpc();
            return false;
        }
        
        [HarmonyPatch(nameof(GiftBoxItem.OpenGiftBoxClientRpc))]
        static bool Prefix()
        {
            bool skipFlag = !GiftBoxPatch.LocalPlayerActivated;
            GiftBoxPatch.LocalPlayerActivated = false;
            return skipFlag;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GiftBoxItem.OpenGiftBoxNoPresentClientRpc))]
        static bool NoPresentPrefix() => false;
    }

    //[HarmonyPatch(typeof(GiftBoxItem), nameof(GiftBoxItem.ItemActivate))]
    //class GiftBoxPatch
    //{
    //    static void Prefix(ref bool used, ref bool ___hasUsedGift)
    //    {
    //        used = false;
    //        ___hasUsedGift = false;
    //    }
    //}

    [HarmonyPatch(typeof(InteractTrigger), "Interact")]
    public class InteractTrigger_Interact_Patch
    {
        public static bool Prefix(InteractTrigger __instance)
        {
            __instance.interactCooldown = !Settings.Instance.settingsData.b_DisableInteractCooldowns;
            return true;
        }
    }

    [HarmonyPatch(typeof(PatcherTool), "LateUpdate")]
    public class PatcherTool_LateUpdate_Patch
    {
        public static void Postfix(PatcherTool __instance)
        {
            if (Settings.Instance.settingsData.b_InfiniteZapGun)
            {
                __instance.gunOverheat = 0f;
                __instance.bendMultiplier = 9999f;
                __instance.pullStrength = 9999f;
                PAUtils.SetValue(__instance, "timeSpentShocking", 0.01f, PAUtils.protectedFlags);
            }
        }
    }

    [HarmonyPatch(typeof(StunGrenadeItem), "ItemActivate")]
    public class StunGrenadeItem_ItemActivate_Patch
    {
        public static bool Prefix(StunGrenadeItem __instance)
        {
            if (Settings.Instance.settingsData.b_DisableInteractCooldowns)
                __instance.inPullingPinAnimation = false;

            if (Settings.Instance.settingsData.b_InfiniteItems)
            {
                __instance.itemUsedUp = false;

                __instance.pinPulled = false;
                __instance.hasExploded = false;
                __instance.DestroyGrenade = false;
                PAUtils.SetValue(__instance, "pullPinCoroutine", null, PAUtils.protectedFlags);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ShotgunItem), "ItemActivate")]
    public class ShotgunItem_ItemActivate_Patch
    {
        public static bool Prefix(ShotgunItem __instance)
        {
            if (Settings.Instance.settingsData.b_InfiniteShotgunAmmo)
            {
                __instance.isReloading = false;
                __instance.shellsLoaded++;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(StartOfRound), "UpdatePlayerVoiceEffects")]
    public class StartOfRound_UpdatePlayerVoiceEffects_Patch
    {
        public static void Postfix(StartOfRound __instance)
        {
            if (Settings.Instance.settingsData.b_HearEveryone
                && !StartOfRound.Instance.shipIsLeaving /* Without this you'll be stuck at "Wait for ship to land" - cba to find out way this happens */)
            {
                for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
                {
                    PlayerControllerB playerControllerB = __instance.allPlayerScripts[i];
                    AudioSource currentVoiceChatAudioSource = playerControllerB.currentVoiceChatAudioSource;

                    currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().enabled = false;
                    currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>().enabled = false;
                    currentVoiceChatAudioSource.panStereo = 0f;
                    SoundManager.Instance.playerVoicePitchTargets[(int)((IntPtr)playerControllerB.playerClientId)] = 1f;
                    SoundManager.Instance.SetPlayerPitch(1f, unchecked((int)playerControllerB.playerClientId));

                    currentVoiceChatAudioSource.spatialBlend = 0f;
                    playerControllerB.currentVoiceChatIngameSettings.set2D = true;
                    playerControllerB.voicePlayerState.Volume = 1f;
                }
            }
        }
    }


    [HarmonyPatch(typeof(HUDManager), "HoldInteractionFill")]
    public class HUDManager_HoldInteractionFill_Patch
    {
        public static bool Prefix(HUDManager __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_InstantInteractions)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SteamLobbyManager), "RefreshServerListButton")] // Removes the refresh cooldown
    public class SteamLobbyManager_RefreshServerListButton_Patch 
    {
        public static bool Prefix(SteamLobbyManager __instance)
        {
            PAUtils.SetValue(__instance, "refreshServerListTimer", 1f, PAUtils.protectedFlags); 
            return true;
        }
    }

    [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter")] // Forces lobbies with blacklisted names to appear
    public class SteamLobbyManager_loadLobbyListAndFilter_Patch
    {
        public static bool Prefix(SteamLobbyManager __instance)
        {
            __instance.censorOffensiveLobbyNames = false;
            return true;
        }
    }

    /* Graphical */

    [HarmonyPatch(typeof(Fog), "IsFogEnabled")]
    public class Fog_IsFogEnabled_Patch
    {
        public static bool Prefix(Fog __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableFog)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Fog), "IsVolumetricFogEnabled")]
    public class Fog_IsVolumetricFogEnabled_Patch
    {
        public static bool Prefix(Fog __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableFog)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Fog), "IsPBRFogEnabled")]
    public class Fog_IsPBRFogEnabled_Patch
    {
        public static bool Prefix(Fog __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableFog)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Bloom), "IsActive")]
    public class Bloom_IsActive_Patch
    {
        public static bool Prefix(Bloom __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableBloom)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(DepthOfField), "IsActive")]
    public class DepthOfField_IsActive_Patch
    {
        public static bool Prefix(DepthOfField __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableDepthOfField)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Vignette), "IsActive")]
    public class Vignette_IsActive_Patch
    {
        public static bool Prefix(Vignette __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableVignette)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(FilmGrain), "IsActive")]
    public class FilmGrain_IsActive_Patch
    {
        public static bool Prefix(FilmGrain __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableFilmGrain)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }


    [HarmonyPatch(typeof(Exposure), "IsActive")]
    public class Exposure_IsActive_Patch
    {
        public static bool Prefix(Exposure __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableFilmGrain)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
