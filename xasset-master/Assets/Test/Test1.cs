using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MyFile {
    public string name;
    public int length;
    public string hash;

    public override string ToString() {
        return "{name: " + name + ", length: " + length + ", hash: " + hash + "}";
    }
}

public class Test1 : MonoBehaviour
{
    void Start()
    {
        //var ab = AssetBundle.LoadFromFile("assets/xasset/demo/ui/1loadingpage.unity3d");

        //Debug.Log(ab);

    }
}
