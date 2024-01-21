using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//public static class HoarderBugAIExtensions
//{
//    public static List<GrabbableObject> items = new List<GrabbableObject>();
//
//    public static void StealAllItems(this HoarderBugAI bug, MonoBehaviour monoBehaviour)
//    {
//        Debug.Log("StealAllItems Called");
//        PlayerControllerB localPlayer = GameObjectManager.Instance.localPlayer;
//        bug.ChangeEnemyOwnerServerRpc(localPlayer.actualClientId);
//
//        Debug.Log("Starting Coroutine");
//        monoBehaviour.StartCoroutine(StealItems(bug));
//    }
//    
//    private static IEnumerator StealItems(HoarderBugAI bug)
//    {
//        List<NetworkObject> items = HoarderBugAIExtensions.items.FindAll(i => !i.isHeld && !i.isPocketed && !i.isInShipRoom && i.isInFactory).ConvertAll(i => i.NetworkObject);
//
//        foreach (var obj in items)
//        {
//            Debug.Log(items);
//            Debug.Log("doing bug things");
//            yield return new WaitForSeconds(0.2f);
//            bug.GrabItemServerRpc(obj);
//            Debug.Log("item grabbed");
//            bug.DropItemServerRpc(obj, bug.nestPosition, true);
//            Debug.Log("dropping item");
//        }
//    }
//}
//