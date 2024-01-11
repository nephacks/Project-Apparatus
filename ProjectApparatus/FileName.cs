using System;
using System.Collections.Generic;
using System.Linq;
using Hax;
using Steamworks;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameObjectManager;
using System.Windows.Forms;
using Unity.Netcode;
using System.IO;
using UnityEngine.ProBuilder.Shapes;
using static UnityEngine.GraphicsBuffer;
using Steamworks.Data;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace ProjectApparatus
{
   // public static class HoarderBugAIExtensions
   // {
   //     public static void StealAllItems(this HoarderBugAI bug)
   //     {
   //         bug.ChangeEnemyOwnerServerRpc(LethalMenu.localPlayer.actualClientId);
   //
   //         LethalMenu.Instance.StartCoroutine(StealItems(bug));
   //
   //
   //     }
   //
   //     private static IEnumerator StealItems(HoarderBugAI bug)
   //     {
   //         List<NetworkObject> items = LethalMenu.items.FindAll(i => !i.isHeld && !i.isPocketed && !i.isInShipRoom && i.isInFactory).ConvertAll(i => i.NetworkObject);
   //
   //         foreach (var obj in items)
   //         {
   //             yield return new WaitForSeconds(0.2f);
   //             bug.GrabItemServerRpc(obj);
   //             bug.DropItemServerRpc(obj, bug.nestPosition, true);
   //         }
   //     }
   // }

    public static class SandSpiderAIExtensions
    {
        public static int SpawnWeb(this SandSpiderAI spider, Vector3 position)
        {
            SandSpiderAI spiderr = UnityEngine.Object.FindObjectOfType(typeof(SandSpiderAI)) as SandSpiderAI;
            spiderr.ChangeEnemyOwnerServerRpc(Instance.localPlayer.actualClientId);

            Ray ray = new Ray(position, Vector3.Scale(UnityEngine.Random.onUnitSphere, new Vector3(1f, UnityEngine.Random.Range(0.6f, 1f), 1f)));

            if (Physics.Raycast(ray, out RaycastHit rayHit, 7f, StartOfRound.Instance.collidersAndRoomMask) && (double)rayHit.distance >= 1.5)
            {
                Vector3 point = rayHit.point;
                if (Physics.Raycast(position, Vector3.down, out rayHit, 10f, StartOfRound.Instance.collidersAndRoomMask))
                {
                    spiderr.SpawnWebTrapServerRpc(rayHit.point, point);


                    return spiderr.webTraps.Count - 1;
                }
            }

            return -1;
        }

        public static void BreakAllWebs(this SandSpiderAI spiderr)
        {
            spiderr.webTraps.ForEach(web => spiderr.BreakWebServerRpc(web.trapID, -1));
        }
    }

}
