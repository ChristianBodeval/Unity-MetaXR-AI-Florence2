using PresentFutures.XRAI.Spatial;
using System.Collections.Generic;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    [Header("Assign the parent object here")]

    GameObject parentObject;
    public AddKeywordExample uploader;

    public List<string> uniqueNames = new List<string>();

    public void Start()
    {
        foreach (OVRSpatialAnchor item in SpatialAnchorFinder.Instance.trackedAnchors)
        {
            string spatialPoint = item.GetComponent<OVRSpatialAnchor>().GetComponent<SpatialLabel>().name;

            uniqueNames.Add(name);
        }
    }

    public void AddNamesAndVoiceKeywords()
    {
        if (parentObject != null)
        {
            

            //CollectChildNames(parentObject);

            foreach (string item in uniqueNames)
            {
                Debug.Log("Calling coroutine for: " + item);
                StartCoroutine(uploader.AddKeyword(item));


            }
        }
        else
        {
            Debug.LogWarning("No parent object assigned!");
        }
    }

    private void Update()
    {
        // Call method again when pressing K
        if (Input.GetKeyDown(KeyCode.K))
        {
            AddNamesAndVoiceKeywords();
        }
    }

    public void CollectChildNames(GameObject parent)
    {
        uniqueNames.Clear(); // avoid duplicates between runs

        /*
        foreach (Transform child in parent.transform)
        {
            string name = child.gameObject.name;

            // Remove "BBox_" if it exists at start
            if (name.StartsWith("BBox_"))
                name = name.Substring(5);

            // Remove "the " if it exists at start (case-insensitive)
            if (name.ToLower().StartsWith("the "))
                name = name.Substring(4);

            uniqueNames.Add(name);
            GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position = child.position;
        }*/

        

        // Log the results
        Debug.Log("Unique child names:");
        foreach (string n in uniqueNames)
        {
            Debug.Log(n);
        }
    }
}
