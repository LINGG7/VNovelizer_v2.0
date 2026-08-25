using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gallery ending data container.
/// </summary>
[CreateAssetMenu(fileName = "EndingDataContainer", menuName = "VNovelizer/Ending Data Container")]
public class EndingDataContainer : ScriptableObject
{
    [Tooltip("Ending list.")]
    public List<VNEnding> endingList = new List<VNEnding>();

    public VNEnding GetEndingByID(string endingID)
    {
        if (endingList == null) return null;

        foreach (VNEnding ending in endingList)
        {
            if (ending != null && ending.EndingID == endingID)
            {
                return ending;
            }
        }
        return null;
    }

    public void AddEnding(VNEnding ending)
    {
        if (endingList == null)
        {
            endingList = new List<VNEnding>();
        }
        endingList.Add(ending);
    }

    public void RemoveEnding(VNEnding ending)
    {
        if (endingList != null)
        {
            endingList.Remove(ending);
        }
    }
}
