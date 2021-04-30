using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using libx;
using UnityEngine;
using UnityEngine.UI;

public class Game : MonoBehaviour {
    public Dropdown dropdown;
    public Image temp;
    //  asset 的名称
    private string[] _assets;
    private int _optionIndex;

    List<GameObject> _gos = new List<GameObject>();
    List<AssetRequest> _requests = new List<AssetRequest>();

    public void OnLoad() {
        StartCoroutine(LoadAsset());
    }

    // Game.LoadSprite
    AssetRequest LoadSprite(string path) {
        var request = Assets.LoadAsset(path, typeof(Sprite));
        _requests.Add(request);
        return request;
    }

    // 加载全部
    public void OnLoadAll() {
        StartCoroutine(LoadAll(_assets.Length));
    }

    IEnumerator LoadAll(int size) {
        var count = 0;
        List<AssetRequest> list = new List<AssetRequest>();
        for (int i = _optionIndex; i < _assets.Length; i++) {
            var asset = _assets[i];
            var ext = Path.GetExtension(asset);
            if (count >= size) {
                _optionIndex = i;
                break;
            }
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)) {
                var request = LoadSprite(asset);
                request.completed += OnCompleted;
                list.Add(request);
                count++;
            }
        }


        yield return new WaitUntil(() => list.TrueForAll(o => {
            return o.isDone;
        }));
    }

    // 加载完成回调
    private void OnCompleted(AssetRequest request) {
        if (!string.IsNullOrEmpty(request.error)) {
            request.Release();
            return;
        }
        var go = Instantiate(temp.gameObject, temp.transform.parent);
        go.SetActive(true);
        go.name = request.asset.name;
        var image = go.GetComponent<Image>();
        image.sprite = request.asset as Sprite;
        _gos.Add(go);
    }

    private IEnumerator LoadAsset() {
        if (_assets == null || _assets.Length == 0) {
            yield break;
        }
        var path = _assets[_optionIndex];
        // 获取后缀名
        var ext = Path.GetExtension(path);

        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)) {
            // Game.LoadSprite
            var request = LoadSprite(path);
            yield return request;

            if (!string.IsNullOrEmpty(request.error)) {
                // 有错误 被引用减1
                request.Release();
                yield break;
            }

            var go = Instantiate(temp.gameObject, temp.transform.parent);
            go.SetActive(true);
            go.name = request.asset.name;
            var image = go.GetComponent<Image>();
            image.sprite = request.asset as Sprite;
            _gos.Add(go);
        } else {
            Debug.Log(path + "," + ext);

            var request = Assets.LoadAsset(path, typeof(GameObject));

            Debug.Log(request == null);

            //Debug.Assert(request == null, $"{request} is null");

            _requests.Add(request);
            yield return request;

            var go = Instantiate(request.asset as GameObject);


            request.Unload();

        }
    }

    // 卸载
    public void OnUnload() {
        _optionIndex = 0;
        StartCoroutine(UnloadAssets());
    }

    private IEnumerator UnloadAssets() {
        foreach (var image in _gos) {
            DestroyImmediate(image);
        }
        _gos.Clear();

        // 减少引用
        foreach (var request in _requests) {
            request.Release();
        }

        _requests.Clear();
        yield return null;
    }

    // Use this for initialization
    void Start() {
        dropdown.ClearOptions();
        _assets = Assets.GetAllAssetPaths();
        foreach (var item in _assets) {
            dropdown.options.Add(new Dropdown.OptionData(item));
        }

        dropdown.onValueChanged.AddListener(OnDropdown);
    }

    // 下拉框事件
    private void OnDropdown(int index) {
        _optionIndex = index;
    }
}