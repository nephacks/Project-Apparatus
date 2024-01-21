using Hax;
using System.Collections;
using UnityEngine;
// shoutout to openai
public class YourScript : MonoBehaviour
{
    // Adjust the waiting time in seconds as needed
    private float waitTimeInSeconds = 1.0f; // for example, 1 second

    private IEnumerator CloseAndOpenDoorCoroutine()
    {
        Helper.CloseShipDoor(true);
        yield return new WaitForSeconds(waitTimeInSeconds); // Wait for specified time
        Helper.CloseShipDoor(false);
    }
}