using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class VNTest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        print(Application.persistentDataPath);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
#endif
