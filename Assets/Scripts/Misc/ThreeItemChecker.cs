using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ThreeItemChecker : MonoBehaviour
{
    [Header("Items to Check")]
    public GameObject item1;
    public GameObject item2;
    public GameObject item3;

    [Header("Events")]
    public UnityEvent OnSuccess;

    private bool hasTriggeredSuccess = false;

    void Update()
    {
        if (!hasTriggeredSuccess && AreAllItemsActive())
        {
            hasTriggeredSuccess = true;
            OnSuccess?.Invoke();
        }
    }

    private bool AreAllItemsActive()
    {
        return item1 != null && item1.activeInHierarchy &&
               item2 != null && item2.activeInHierarchy &&
               item3 != null && item3.activeInHierarchy;
    }

    public void Reset()
    {
        hasTriggeredSuccess = false;
    }
}
