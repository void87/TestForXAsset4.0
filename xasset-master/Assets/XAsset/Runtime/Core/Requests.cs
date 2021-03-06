//
// Requests.cs
//
// Author:
//       fjy <jiyuan.feng@live.com>
//
// Copyright (c) 2020 fjy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace libx
{
    public enum LoadState {
        // 初始化
        Init,
        // 加载自身包含的 BundleRequest
        //  BundleRequestAsync.Load(), 
        //  BundleAssetRequestAsync.Load(), 
        //  SceneAssetRequestAsync.Load(), 
        // 
        //  WebBundleRequest.Load()
        LoadAssetBundle,
        // 异步加载时 才会有这个状态
        // SceneAssetRequestAsync.LoadScene()
        //      SceneManager.LoadSceneAsync()
        // BundleRequestAsync.Load()
        //      AssetBundle.LoadFromFileAsync()
        // 
        // WebAssetRequest.Load()
        //      UnityWebRequest.SendWebRequest()
        LoadAsset,
        // 加载 bundle/asset 完毕
        // AssetRequest.Load()  // 不会调用这个
        // ManifestRequest.Load()   // 不是 runtimeMode
        // ManifestRequest.Update() // request 为空的处理
        // BundleAssetRequest.Load()  
        // BundleAssetRequestAsync.OnError()    错误处理
        // BundleAssetRequestAsync.OnUpdate // request 为空的处理
        // BundleAssetRequestAsync.LoadImmediate()  
        // 
        Loaded,
        // 已卸载
        Unload
    }

    // 所有 Request 的基类
    public class AssetRequest : Reference, IEnumerator {

        // 初始加载状态
        private LoadState _loadState = LoadState.Init;
        // 依赖哪些 UnityEngine.Object
        private List<Object> _requires;
        // asset 类型
        public Type assetType;

        // 请求完毕后的 回调
        public Action<AssetRequest> completed;

        // 资源名字， 根据子类 可以是 asset 名， 也可以是 bundle 名
        // 
        // BundleRequest & BundleRequestAsync    
        //     e.g. C:/Users/void8/AppData/LocalLow/xasset/xasset/DLC/assets/xasset/demo/ui/1loadingpage.unity3d
        // AssetRequest & AssetRequestAsync   
        //      e.g. Assets/XAsset/Demo/UI/1LoadingPage/Title2_bg.png
        // SceneRequest & SceneRequestAsync
        //      e.g. Assets/XAsset/Demo/Scenes/Game.unity
        // ManifestRequest
        //      e.g. Assets/Manifest.asset
        // ManifestRequest 包含的 BundleRequest
        //      e.g. C:/Users/void8/AppData/LocalLow/xasset/xasset/DLC/manifest.unity3d
        public string name;

        public AssetRequest() {
            asset = null;
            // AssetRequest 初始化时会设置 LoadState.Init
            loadState = LoadState.Init;
        }

        // Asset || Bundle 的加载状态
        public LoadState loadState {
            get { return _loadState; }
            protected set {
                _loadState = value;
                // 设置 加载完时， 自动调用 Complete 回调
                if (value == LoadState.Loaded) {
                    Complete();
                }
            }
        }

        // 完成时 回调
        private void Complete() {
            if (completed != null) {
                completed(this);
                completed = null;
            }
        }

        // isDone 状态由 LoadState 判断
        public virtual bool isDone {
            get {
                return loadState == LoadState.Loaded || loadState == LoadState.Unload;
            }
        }

        // AssetRequest  加载进度 默认 1
        public virtual float progress {
            get { return 1; }
        }

        // 请求时出现的错误
        public virtual string error { get; protected set; }

        // 请求时下载的 text
        public string text { get; protected set; }

        // 请求时下载的 byte[]
        public byte[] bytes { get; protected set; }

        // 这个 AssetRequest 包含的 asset (UnityEngine.Object)
        public Object asset { get; internal set; }

        // 检查依赖是否为空
        private bool checkRequires {
            get {
                return _requires != null;
            }
        }

        // 更新 AssetRequest._requires
        private void UpdateRequires() {
            for (var i = 0; i < _requires.Count; i++) {
                var item = _requires[i];
                
                if (item != null)
                    continue;
                // 被引用-1
                Release();

                _requires.RemoveAt(i);
                i--;
            }

            if (_requires.Count == 0)
                _requires = null;
        }

        // 加载 子类都有自己的处理
        internal virtual void Load() {
            // 
            if (!Assets.runtimeMode && Assets.loadDelegate != null) {
                asset = Assets.loadDelegate(name, assetType);
            }
            if (asset == null) {
                error = "error! file not exist:" + name;
            }
            loadState = LoadState.Loaded;
        }

        // AssetRequest.Unload
        internal virtual void Unload() {
            if (asset == null)
                return;

            if (!Assets.runtimeMode) {
                if (!(asset is GameObject)) {
                    Resources.UnloadAsset(asset);
                }
            }

            asset = null;

            loadState = LoadState.Unload;
        }

        // true 表示正在更新
        // false 表示不在更新
        // AssetRequest.Update()
        internal virtual bool Update() {
            if (checkRequires) {
                // 更新 AssetRequest._requires
                UpdateRequires();
            }

            if (!isDone) {
                return true;
            }

            if (completed == null) {
                return false;
            }

            try {
                completed.Invoke(this);
            } catch (Exception ex) {
                Debug.LogException(ex);
            }

            completed = null;
            return false;
        }

        internal virtual void LoadImmediate() {

        }

        #region IEnumerator implementation

        public bool MoveNext() {
            return !isDone;
        }

        public void Reset() {
        }

        public object Current {
            get { return null; }
        }

        #endregion
    }

    //  Manifest 专用 Request
    public class ManifestRequest : AssetRequest {
        // Manifest.asset
        private string assetName;
        // 包含的 BundleRequest
        private BundleRequest bundleRequest;

        public int version { get; private set; }

        public override float progress {
            get {
                if (isDone) return 1;

                if (loadState == LoadState.Init) return 0;

                if (bundleRequest == null) return 1;

                return bundleRequest.progress;
            }
        }

        
        // ManifestRequest.Load()
        internal override void Load() {
            // 只获取 Assets/Manifest 的 文件名 Manifest.asset
            assetName = Path.GetFileName(name);

            if (Assets.runtimeMode) {
                // 特殊处理,因为 Manifest 比较特殊
                // Manifest.asset 转为 manifest.unity3d
                var assetBundleName = assetName.Replace(".asset", ".unity3d").ToLower();
                // 处理 ManifestRequest 包含的 BundleRequest
                bundleRequest = Assets.LoadBundleAsync(assetBundleName);

                // ManifestRequest.loadState 设置为 LoadState.LoadAssetBundle
                loadState = LoadState.LoadAssetBundle;
            } else {
                // 原有代码，注释掉了, 不能在 !runtimeMode 下 加载资源
                // loadState = LoadState.Loaded; 

                // 加入以下代码，可以在 !runtimeMode 下加载资源 黄鑫
                assetType = typeof(ScriptableObject);
                base.Load();
                Assets.OnLoadManifest(asset as Manifest);
            }
        }

        // ManifestRequest.Update
        internal override bool Update() {
            //Debug.Log("ManifestRequest.Update().loadState: " + loadState + "at: " + Time.frameCount);


            if (!base.Update()) {
                return false;
            }

            if (loadState == LoadState.Init) {
                return true;
            }

            if (bundleRequest == null) {
                loadState = LoadState.Loaded;
                error = "request == null";
                return false;
            }

            // 包含的 BundleRequest 完成后，才将自身的 状态 设置为 完成
            if (bundleRequest.isDone) {
                if (bundleRequest.assetBundle == null) {
                    error = "assetBundle == null";
                } else {
                    // 从 AssetBundle 中读取 Manifest（同步）
                    var manifest = bundleRequest.assetBundle.LoadAsset<Manifest>(assetName);

                    if (manifest == null)
                        error = "manifest == null";
                    else
                        // 处理 Manifest
                        Assets.OnLoadManifest(manifest);
                }

                // ManifestRequest.loadState = LoadState.Loaded
                loadState = LoadState.Loaded;


                //Debug.Log("ManifestRequest.Update().loadState: " + loadState + "at: " + Time.frameCount);

                return false;
            }

            return true;
        }

        // ManifestRequest.Unload
        internal override void Unload() {
            // 释放 包含的 BundleRequest
            if (bundleRequest != null) {
                bundleRequest.Release();
                bundleRequest = null;
            }

            // ManifestRequest.loadState 设置 LoadState.Unload
            loadState = LoadState.Unload;
        }
    }

    // 请求 AssetBundle 里的 Asset (同步)
    public class BundleAssetRequest : AssetRequest {
        // bundle 名 e.g. assets/test/prefab1.unity3d
        protected readonly string assetBundleName;
        // 包含的  BundleRequest
        protected BundleRequest bundleRequest;
        // 依赖的 Bundle
        protected List<BundleRequest> childrenBundleRequestList = new List<BundleRequest>();

        public BundleAssetRequest(string bundle) {
            assetBundleName = bundle;
        }

        // BundleAssetRequest.Load() 加载 AssetBudnle
        internal override void Load() {
            
            bundleRequest = Assets.LoadBundle(assetBundleName);
            // 获取 bundle 的依赖
            var names = Assets.GetAllDependencies(assetBundleName);

            foreach (var item in names) {
                childrenBundleRequestList.Add(Assets.LoadBundle(item));
            }

            // 获取文件名 不要路径 e.g. Title2_bg.png
            var assetName = Path.GetFileName(name);
            // AssetBundle
            var ab = bundleRequest.assetBundle;

            if (ab != null)
                // 加载 AssetBundle 里的 asset, 
                asset = ab.LoadAsset(assetName, assetType);

            if (asset == null)
                error = "asset == null";

            loadState = LoadState.Loaded;
        }

        // BundleAssetRequest.Unload
        internal override void Unload() {

            if (bundleRequest != null) {
                // 包含的 BundleRequest 的引用减一
                bundleRequest.Release();

                bundleRequest = null;
            }

            if (childrenBundleRequestList.Count > 0) {
                // 包含的 依赖 BundleRequest 的 引用减一
                foreach (var item in childrenBundleRequestList) {
                    item.Release();
                }
                childrenBundleRequestList.Clear();
            }

            asset = null;
        }
    }

    // 请求 AssetBundle 里的 Asset (异步)
    public class BundleAssetRequestAsync : BundleAssetRequest {
        // AssetBundleRequest
        private AssetBundleRequest _request;

        public BundleAssetRequestAsync(string bundle)
            : base(bundle) {
        }

        public override float progress {
            get {
                if (isDone) return 1;

                if (loadState == LoadState.Init) return 0;

                if (_request != null) return _request.progress * 0.7f + 0.3f;

                if (bundleRequest == null) return 1;

                var value = bundleRequest.progress;
                var max = childrenBundleRequestList.Count;
                if (max <= 0)
                    return value * 0.3f;

                for (var i = 0; i < max; i++) {
                    var item = childrenBundleRequestList[i];
                    value += item.progress;
                }

                return value / (max + 1) * 0.3f;
            }
        }

        private bool OnError(BundleRequest bundleRequest) {
            error = bundleRequest.error;
            if (!string.IsNullOrEmpty(error)) {
                loadState = LoadState.Loaded;
                return true;
            }

            return false;
        }

        // BundleAssetRequestAsync.Update()
        internal override bool Update() {
            if (!base.Update())
                return false;

            if (loadState == LoadState.Init)
                return true;

            if (_request == null) {
                if (!bundleRequest.isDone)
                    return true;

                if (OnError(bundleRequest))
                    return false;

                for (var i = 0; i < childrenBundleRequestList.Count; i++) {
                    var item = childrenBundleRequestList[i];
                    if (!item.isDone)
                        return true;

                    if (OnError(item))
                        return false;
                }

                var assetName = Path.GetFileName(name);
                _request = bundleRequest.assetBundle.LoadAssetAsync(assetName, assetType);
                if (_request == null) {
                    error = "request == null";
                    loadState = LoadState.Loaded;
                    return false;
                }

                return true;
            }

            if (_request.isDone) {
                asset = _request.asset;
                loadState = LoadState.Loaded;
                if (asset == null) error = "asset == null";
                return false;
            }

            return true;
        }

        // BundleAssetRequestAsync.Load()
        internal override void Load() {
            // 通过 bundle名，创建 BundleRequest
            bundleRequest = Assets.LoadBundleAsync(assetBundleName);
            // 通过 bundle名，获取 依赖的 bundle集合
            var bundles = Assets.GetAllDependencies(assetBundleName);

            // 通过每个依赖的 bundle 名， 创建 BundleRequest
            foreach (var item in bundles) {
                childrenBundleRequestList.Add(Assets.LoadBundleAsync(item));
            }

            // BundleAssetRequestAsync.loadState 设置为 LoadState.LoadAssetBundle
            loadState = LoadState.LoadAssetBundle;
        }

        // BundleAssetRequestAsync.Unload
        internal override void Unload() {
            _request = null;
            loadState = LoadState.Unload;

            base.Unload();
        }

        // BundleAssetRequestAsync.LoadImmediate()
        internal override void LoadImmediate() {
            bundleRequest.LoadImmediate();

            foreach (var item in childrenBundleRequestList) {
                item.LoadImmediate();
            }

            if (bundleRequest.assetBundle != null) {
                var assetName = Path.GetFileName(name);
                asset = bundleRequest.assetBundle.LoadAsset(assetName, assetType);
            }

            loadState = LoadState.Loaded;
            if (asset == null) error = "asset == null";
        }
    }

    // 专门处理场景的 SceneAssetRequest
    public class SceneAssetRequest : AssetRequest {
        // 场景名称 e.g. Game
        protected readonly string sceneName;
        // 场景所在的 ab包名 e.g. assets/xasset/demo/scenes.unity3d
        public string assetBundleName;
        // 包含的 BundleRequest
        protected BundleRequest bundleRequest;
        // 依赖的 BundleRequest
        protected List<BundleRequest> children = new List<BundleRequest>();

        public SceneAssetRequest(string path, bool addictive) {
            name = path;
            // 获取 场景所在的 bundle 名
            Assets.GetAssetBundleName(path, out assetBundleName);
            // 获取场景名 e.g. Game
            sceneName = Path.GetFileNameWithoutExtension(name);
            loadSceneMode = addictive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        }

        // 场景加载模式
        public LoadSceneMode loadSceneMode { get; protected set; }

        public override float progress {
            get { return 1; }
        }

        // SceneAssetRequest.Load()
        internal override void Load() {
            if (!string.IsNullOrEmpty(assetBundleName)) {
                bundleRequest = Assets.LoadBundle(assetBundleName);
                if (bundleRequest != null) {
                    var bundles = Assets.GetAllDependencies(assetBundleName);

                    foreach (var item in bundles) {
                        children.Add(Assets.LoadBundle(item));
                    }

                    SceneManager.LoadScene(sceneName, loadSceneMode);
                }
            } else {
                SceneManager.LoadScene(sceneName, loadSceneMode);
            }

            loadState = LoadState.Loaded;
        }

        // SceneAssetRequest 卸载
        internal override void Unload() {
            if (bundleRequest != null)
                bundleRequest.Release();

            if (children.Count > 0) {
                foreach (var item in children) item.Release();
                children.Clear();
            }

            if (loadSceneMode == LoadSceneMode.Additive)
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                    SceneManager.UnloadSceneAsync(sceneName);

            bundleRequest = null;
            loadState = LoadState.Unload;
        }
    }

    // 专门处理场景的 [SceneAsset] Request Async
    public class SceneAssetRequestAsync : SceneAssetRequest {
        // 由 SceneManager.LoadSceneAsync 创建
        private AsyncOperation _asyncOperation;

        public SceneAssetRequestAsync(string path, bool addictive)
            : base(path, addictive) {
        }

        public override float progress {
            get {
                if (isDone) return 1;

                if (loadState == LoadState.Init) return 0;

                if (_asyncOperation != null) return _asyncOperation.progress * 0.7f + 0.3f;

                if (bundleRequest == null) return 1;

                var value = bundleRequest.progress;
                var max = children.Count;
                if (max <= 0)
                    return value * 0.3f;

                for (var i = 0; i < max; i++) {
                    var item = children[i];
                    value += item.progress;
                }

                return value / (max + 1) * 0.3f;
            }
        }

        // SceneAssetRequestAsync.OnError()
        private bool OnError(BundleRequest bundleRequest) {
            error = bundleRequest.error;
            if (!string.IsNullOrEmpty(error)) {
                loadState = LoadState.Loaded;
                return true;
            }

            return false;
        }

        // SceneAssetRequestAsync.Update()
        internal override bool Update() {
            if (!base.Update())
                return false;

            if (loadState == LoadState.Init)
                return true;

            // ab 包加载完后,SceneManager.LoadSceneAsync 才会创建 _asyncOperation
            if (_asyncOperation == null) {
                if (bundleRequest == null) {
                    error = "bundle == null";

                    loadState = LoadState.Loaded;
                    return false;
                }

                if (!bundleRequest.isDone)
                    return true;

                if (OnError(bundleRequest))
                    return false;

                for (var i = 0; i < children.Count; i++) {
                    var item = children[i];
                    if (!item.isDone)
                        return true;
                    if (OnError(item))
                        return false;
                }

                // SceneAssetRequestAsync.LoadScene()
                LoadScene();

                return true;
            }

            // AsyncOperation.isDone
            if (_asyncOperation.isDone) {
                loadState = LoadState.Loaded;
                return false;
            }

            return true;
        }

        // SceneAssetRequestAsync.LoadScene()
        private void LoadScene() {
            try {
                _asyncOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
                
                loadState = LoadState.LoadAsset;
            } catch (Exception e) {
                Debug.LogException(e);
                error = e.Message;

                loadState = LoadState.Loaded;
            }
        }

        // SceneAssetRequestAsync.Load()
        internal override void Load() {
            if (!string.IsNullOrEmpty(assetBundleName)) {
                // 加载 相关的 BundleRequest
                bundleRequest = Assets.LoadBundleAsync(assetBundleName);
                // 获取 bundle 的依赖 bundle
                var bundles = Assets.GetAllDependencies(assetBundleName);

                foreach (var item in bundles) {
                    children.Add(Assets.LoadBundleAsync(item));
                }

                // SceneAssetRequestAsync.loadState 设置为 LoadState.LoadAssetBundle
                loadState = LoadState.LoadAssetBundle;
            } else {
                LoadScene();
            }
        }

        internal override void Unload() {
            base.Unload();
            _asyncOperation = null;
        }
    }

    

    // 请求 AssetBundle (同步)
    public class BundleRequest : AssetRequest {
        // bundle 名称 e.g. assets/xasset/demo/ui/1loadingpage.unity3d
        public string assetBundleName { get; set; }

        // 包含的 AssetBundle
        public AssetBundle assetBundle {
            get { return asset as AssetBundle; }
            internal set { asset = value; }
        }

        // BundleRequest.Load (同步)  真正加载的地方, 调用 API 从 ab 包 加载
        internal override void Load() {
            asset = AssetBundle.LoadFromFile(name);

            if (assetBundle == null)
                error = name + " LoadFromFile failed.";

            // BundleAsset.loadState 设置 为 LoadState.Loaded
            loadState = LoadState.Loaded;
        }

        // BundleRequest.UnLoad
        internal override void Unload() {
            if (assetBundle == null)
                return;
            // AssetBundle.Unload (官方API)
            assetBundle.Unload(true);

            assetBundle = null;

            loadState = LoadState.Unload;
        }
    }

    // 请求 AssetBundle (异步)
    public class BundleRequestAsync : BundleRequest {
        // 包含的 AssetBundleCreateRequest
        private AssetBundleCreateRequest _assetBundleCreateRequest;

        public override float progress {
            get {
                if (isDone)
                    return 1;
                if (loadState == LoadState.Init)
                    return 0;
                if (_assetBundleCreateRequest == null)
                    return 1;
                return _assetBundleCreateRequest.progress;
            }
        }

        // BundleRequestAsync 有 Update处理
        internal override bool Update() {
            if (!base.Update()) {
                // 返回 false 表示不需要更新
                return false;
            }
            
            if (loadState == LoadState.LoadAsset) {
                // 也可以用 协程 查看 isDone
                if (_assetBundleCreateRequest.isDone) {

                    assetBundle = _assetBundleCreateRequest.assetBundle;
                    if (assetBundle == null) {
                        error = string.Format("unable to load assetBundle:{0}", name);
                    }

                    // 根据 AssetBundleCreateRequest.isDone, 设置 LoadState.Loaded
                    loadState = LoadState.Loaded;
                    // 返回 false 表示不需要更新
                    return false;
                }
            }
            // 返回 true 表示需要更新
            return true;
        }

        // BundleRequestAsync.Load
        internal override void Load() {
            
            if (_assetBundleCreateRequest == null) {
                _assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(name);

                if (_assetBundleCreateRequest == null) {
                    error = name + " LoadFromFile failed.";
                    return;
                }

                
                loadState = LoadState.LoadAsset;
            }
        }

        // BundleRequestAsync.Unload
        internal override void Unload() {
            _assetBundleCreateRequest = null;
            loadState = LoadState.Unload;

            base.Unload();
        }

        internal override void LoadImmediate() {
            Load();
            assetBundle = _assetBundleCreateRequest.assetBundle;
            if (assetBundle != null) {
                Debug.LogWarning("LoadImmediate:" + assetBundle.name);
            }
            loadState = LoadState.Loaded;
        }
    }

    // [WebAsset] Request
    public class WebAssetRequest : AssetRequest {
        private UnityWebRequest _www;

        public override float progress {
            get {
                if (isDone) return 1;
                if (loadState == LoadState.Init) return 0;

                if (_www == null) return 1;

                return _www.downloadProgress;
            }
        }

        public override string error {
            get { return _www.error; }
        }


        internal override bool Update() {
            if (!base.Update()) return false;

            if (loadState == LoadState.LoadAsset) {
                if (_www == null) {
                    error = "www == null";
                    return false;
                }

                if (!string.IsNullOrEmpty(_www.error)) {
                    error = _www.error;
                    loadState = LoadState.Loaded;
                    return false;
                }

                if (_www.isDone) {
                    GetAsset();
                    loadState = LoadState.Loaded;
                    return false;
                }

                return true;
            }

            return true;
        }

        private void GetAsset() {
            if (assetType == typeof(Texture2D))
                asset = DownloadHandlerTexture.GetContent(_www);
            else if (assetType == typeof(AudioClip))
                asset = DownloadHandlerAudioClip.GetContent(_www);
            else if (assetType == typeof(TextAsset))
                text = _www.downloadHandler.text;
            else
                bytes = _www.downloadHandler.data;
        }

        internal override void Load() {
            if (assetType == typeof(AudioClip)) {
                _www = UnityWebRequestMultimedia.GetAudioClip(name, AudioType.WAV);
            } else if (assetType == typeof(Texture2D)) {
                _www = UnityWebRequestTexture.GetTexture(name);
            } else {
                _www = new UnityWebRequest(name);
                _www.downloadHandler = new DownloadHandlerBuffer();
            }

            _www.SendWebRequest();
            loadState = LoadState.LoadAsset;
        }

        // WebAssetRequest.Unload
        internal override void Unload() {
            if (asset != null) {
                Object.Destroy(asset);
                asset = null;
            }

            if (_www != null)
                _www.Dispose();

            bytes = null;
            text = null;
            loadState = LoadState.Unload;
        }
    }

    //  [WebBundle] Request
    public class WebBundleRequest : BundleRequest {
        private UnityWebRequest _request;
        // false 没有地方修改这个变量
        public bool cache;
        public Hash128 hash;

        public override float progress {
            get {
                if (isDone) return 1;
                if (loadState == LoadState.Init) return 0;

                if (_request == null) return 1;

                return _request.downloadProgress;
            }
        }

        internal override void Load() {
            _request = cache
                ? UnityWebRequestAssetBundle.GetAssetBundle(name, hash)
                : UnityWebRequestAssetBundle.GetAssetBundle(name);
            _request.SendWebRequest();

            // WebBundleRequest.loadState 设置为 LoadState.LoadAssetBundle
            loadState = LoadState.LoadAssetBundle;
        }

        // WebBundleRequest.Unload
        internal override void Unload() {
            if (_request != null) {
                _request.Dispose();
                _request = null;
            }

            loadState = LoadState.Unload;

            base.Unload();
        }
    }
}