//
// Assets.cs
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

#define LOG_ENABLE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace libx {
    
    public sealed class Assets : MonoBehaviour {
        // 自定义 Manifest
        public static readonly string ManifestAsset = "Assets/Manifest.asset";
        // assetbundle 后缀名
        public static readonly string Extension = ".unity3d";

        // 读取ab包
        public static bool runtimeMode = true;
        public static Func<string, Type, Object> loadDelegate = null;
        private const string TAG = "[Assets]";

        [Conditional("LOG_ENABLE")]
        private static void Log(string s) {
            Debug.Log(string.Format("{0}{1}", TAG, s));
        }

        #region API

        /// <summary>
        /// 读取所有资源路径
        /// </summary>
        /// <returns></returns>
        public static string[] GetAllAssetPaths() {
            var assets = new List<string>();
            assets.AddRange(_assetToBundleDict.Keys);
            return assets.ToArray();
        }

        // S 目录
        public static string basePath { get; set; }

        // P 目录
        public static string updatePath { get; set; }

        // 添加 场景的 搜索目录
        public static void AddSearchPath(string path) {
            _searchPaths.Add(path);
        }

        public static ManifestRequest Initialize() {

            // 添加 Assets
            var instance = FindObjectOfType<Assets>();
            if (instance == null) {
                instance = new GameObject("Assets").AddComponent<Assets>();
                DontDestroyOnLoad(instance.gameObject);
            }

            // basePath == S 目录
            if (string.IsNullOrEmpty(basePath)) {
                basePath = Application.streamingAssetsPath + Path.DirectorySeparatorChar;
            }

            // updatePath == P 目录
            if (string.IsNullOrEmpty(updatePath)) {
                updatePath = Application.persistentDataPath + Path.DirectorySeparatorChar;
            }

            Clear();

            Log(string.Format(
                "Initialize with: runtimeMode={0}\nbasePath：{1}\nupdatePath={2}",
                runtimeMode, basePath, updatePath));

            var request = new ManifestRequest {
                name = ManifestAsset
            };
            AddAssetRequest(request);
            return request;
        }

        public static void Clear() {
            _searchPaths.Clear();
            _activeVariants.Clear();
            _assetToBundleDict.Clear();
            _bundleToDependenciesDict.Clear();
        }

        private static SceneAssetRequest _runningScene;

        public static SceneAssetRequest LoadSceneAsync(string path, bool additive) {
            if (string.IsNullOrEmpty(path)) {
                Debug.LogError("invalid path");
                return null;
            }

            path = GetExistPath(path);
            var asset = new SceneAssetRequestAsync(path, additive);
            if (!additive) {
                if (_runningScene != null) {
                    _runningScene.Release(); ;
                    _runningScene = null;
                }
                _runningScene = asset;
            }
            asset.Load();
            asset.Retain();
            _sceneAssetRequestList.Add(asset);
            Log(string.Format("LoadScene:{0}", path));
            return asset;
        }

        public static void UnloadScene(SceneAssetRequest scene) {
            scene.Release();
        }

        public static AssetRequest LoadAssetAsync(string path, Type type) {
            return LoadAsset(path, type, true);
        }

        // 加载 asset
        public static AssetRequest LoadAsset(string path, Type type) {
            return LoadAsset(path, type, false);
        }

        public static void UnloadAsset(AssetRequest asset) {
            asset.Release();
        }

        #endregion

        #region Private

        internal static void OnLoadManifest(Manifest manifest) {
            _activeVariants.AddRange(manifest.activeVariants);

            var assets = manifest.assetRefArray;
            var dirs = manifest.dirs;
            BundleRef[] bundles = manifest.bundleRefArray;

            foreach (var item in bundles) {
                _bundleToDependenciesDict[item.name] = Array.ConvertAll(item.deps, id => bundles[id].name);
            }

            foreach (var item in assets) {
                var path = string.Format("{0}/{1}", dirs[item.dir], item.name);
                if (item.bundle >= 0 && item.bundle < bundles.Length) {
                    _assetToBundleDict[path] = bundles[item.bundle].name;
                } else {
                    Debug.LogError(string.Format("{0} bundle {1} not exist.", path, item.bundle));
                }
            }
        }

        // 所有的 AssetRequest
        private static Dictionary<string, AssetRequest> _allAssetRequestDict = new Dictionary<string, AssetRequest>();

        // 正在加载的 AssetRequest
        private static List<AssetRequest> _loadingAssetRequestList = new List<AssetRequest>();
        // 专门的 SceneRequest
        private static List<SceneAssetRequest> _sceneAssetRequestList = new List<SceneAssetRequest>();

        private static List<AssetRequest> _unusedAssetRequestList = new List<AssetRequest>();

        // Update 驱动 正在加载的 AssetRequest
        private void Update() {
            UpdateAssets();
            UpdateBundles();
        }

        // 更新 _loadingAssetRequestList, _unusedAssetRequestList
        // _sceneAssetRequestList
        private static void UpdateAssets() {
            for (var i = 0; i < _loadingAssetRequestList.Count; ++i) {
                var request = _loadingAssetRequestList[i];
                // 跳过正在更新的 AssetRequest
                if (request.Update())
                    continue;
                // 移除不在更新中的 AssetRequest
                _loadingAssetRequestList.RemoveAt(i);
                --i;
            }

            // 查找 不需要的 AssetRequest
            foreach (var item in _allAssetRequestDict) {
                // 将 isDone && IsUnused 的 AssetRequest 添加到 _unusedAssetRequest
                if (item.Value.isDone && item.Value.IsUnused()) {
                    _unusedAssetRequestList.Add(item.Value);
                }
            }

            // 清理不需要的 AssetRequest
            if (_unusedAssetRequestList.Count > 0) {
                for (var i = 0; i < _unusedAssetRequestList.Count; ++i) {
                    var request = _unusedAssetRequestList[i];
                    Log(string.Format("UnloadAsset:{0}", request.name));
                    // 从 _allAssetRequest 中 移除这个 AssetRequest
                    _allAssetRequestDict.Remove(request.name);
                    //  卸载 这个 AssetRequest
                    request.Unload();
                }
                // 每帧清空 当前帧收集的 没用的 AssetRequest
                _unusedAssetRequestList.Clear();
            }

            // 处理 SceneAssetRequest
            for (var i = 0; i < _sceneAssetRequestList.Count; ++i) {
                var request = _sceneAssetRequestList[i];
                // 跳过 正在更新的 SceneAssetRequest || 没有使用的 SceneAssetRequest
                if (request.Update() || !request.IsUnused())
                    continue;
                // 从 集合中移除
                _sceneAssetRequestList.RemoveAt(i);
                Log(string.Format("UnloadScene:{0}", request.name));
                // SceneAssetRequest 卸载
                request.Unload();
                --i;
            }
        }

        // 添加到 _assetRequestDict, _loadingAssetRequestList
        // 然后 Load
        private static void AddAssetRequest(AssetRequest request) {
            _allAssetRequestDict.Add(request.name, request);

            _loadingAssetRequestList.Add(request);

            request.Load();
        }

        // 加载 Asset
        private static AssetRequest LoadAsset(string path, Type type, bool async) {
            if (string.IsNullOrEmpty(path)) {
                Debug.LogError("invalid path");
                return null;
            }

            // 判断这个路径是否存在记录,有就返回
            path = GetExistPath(path);

            AssetRequest request;
            if (_allAssetRequestDict.TryGetValue(path, out request)) {
                request.Retain();
                _loadingAssetRequestList.Add(request);
                return request;
            }

            string assetBundleName;
            if (GetAssetBundleName(path, out assetBundleName)) {
                request = async
                    ? new BundleAssetRequestAsync(assetBundleName)
                    : new BundleAssetRequest(assetBundleName);
            } else {
                if (path.StartsWith("http://", StringComparison.Ordinal) ||
                    path.StartsWith("https://", StringComparison.Ordinal) ||
                    path.StartsWith("file://", StringComparison.Ordinal) ||
                    path.StartsWith("ftp://", StringComparison.Ordinal) ||
                    path.StartsWith("jar:file://", StringComparison.Ordinal))
                    request = new WebAssetRequest();
                else
                    request = new AssetRequest();
            }

            request.name = path;
            request.assetType = type;
            AddAssetRequest(request);
            request.Retain();
            Log(string.Format("LoadAsset:{0}", path));
            return request;
        }

        #endregion

        #region Paths

        // 附加的搜索路径
        private static List<string> _searchPaths = new List<string>();


        // 从现有记录里查找 是否有记录 这个 asset名, 如果有返回 asset名，再加上特殊处理(如果有的话)
        private static string GetExistPath(string path) {
#if UNITY_EDITOR
            if (!runtimeMode) {
                if (File.Exists(path))
                    return path;

                foreach (var item in _searchPaths) {
                    var existPath = string.Format("{0}/{1}", item, path);
                    if (File.Exists(existPath))
                        return existPath;
                }

                Debug.LogError("找不到资源路径" + path);
                return path;
            }
#endif
            // 先查找所有的 asset bundle 映射表
            if (_assetToBundleDict.ContainsKey(path))
                return path;

            // 特殊搜索路径
            foreach (var item in _searchPaths) {
                // 通过特殊路径组合
                var existPath = string.Format("{0}/{1}", item, path);

                // 查找所有的 asset bundle 映射表
                if (_assetToBundleDict.ContainsKey(existPath))
                    return existPath;
            }

            Debug.LogError("资源没有收集打包" + path);
            return path;
        }

        #endregion

        #region Bundles

        private static readonly int MAX_BUNDLES_PERFRAME = 0;

        // [bunlde路径名， BundleRequest]
        private static Dictionary<string, BundleRequest> _bundleRequestDict = new Dictionary<string, BundleRequest>();

        // 正在加载的 BundleRequest
        private static List<BundleRequest> _loadingBundleRequestList = new List<BundleRequest>();

        // 需要移除的 BundleRequest
        private static List<BundleRequest> _unusedBundleRequestList = new List<BundleRequest>();

        private static List<BundleRequest> _toloadBundleList = new List<BundleRequest>();

        private static List<string> _activeVariants = new List<string>();

        // [asset名, bundle名] 
        // e.g. [Assets/XAsset/Demo/UI/1LoadingPage/Progress1.png, assets/xasset/demo/ui/1loadingpage.unity3d]
        private static Dictionary<string, string> _assetToBundleDict = new Dictionary<string, string>();
        // [bundle名, 依赖名]
        private static Dictionary<string, string[]> _bundleToDependenciesDict = new Dictionary<string, string[]>();

        internal static bool GetAssetBundleName(string path, out string assetBundleName) {
            return _assetToBundleDict.TryGetValue(path, out assetBundleName);
        }

        // 通过 在 记录好的 依赖字典里 查找 依赖
        internal static string[] GetAllDependencies(string bundle) {
            string[] deps;
            if (_bundleToDependenciesDict.TryGetValue(bundle, out deps)) {
                return deps;
            }

            return new string[0];
        }

        // 通过bundle名 加载 bundle
        internal static BundleRequest LoadBundle(string assetBundleName) {
            return LoadBundle(assetBundleName, false);
        }

        // 通过 ab 包名字 读取ab包 async
        internal static BundleRequest LoadBundleAsync(string assetBundleName) {
            return LoadBundle(assetBundleName, true);
        }

        internal static void UnloadBundle(BundleRequest bundle) {
            bundle.Release();
        }

        // 通过 ab 包名 读取 ab包, 同步/异步
        internal static BundleRequest LoadBundle(string assetBundleName, bool asyncMode) {
            if (string.IsNullOrEmpty(assetBundleName)) {
                Debug.LogError("assetBundleName == null");
                return null;
            }

            assetBundleName = RemapVariantName(assetBundleName);

            // 路径 +  ab名
            var url = GetDataPath(assetBundleName) + assetBundleName;

            BundleRequest bundleRequest;

            // 已经有这个 BundleRequest
            if (_bundleRequestDict.TryGetValue(url, out bundleRequest)) {
                // bundle 被引用+1
                bundleRequest.Retain();
                // 加入 loadingBundleRequest
                _loadingBundleRequestList.Add(bundleRequest);
                return bundleRequest;
            }

            // 从网络请求    WebBundleRequest
            if (url.StartsWith("http://", StringComparison.Ordinal) ||
                url.StartsWith("https://", StringComparison.Ordinal) ||
                url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("ftp://", StringComparison.Ordinal)) {

                // WebBundleRequest
                bundleRequest = new WebBundleRequest();
            // 从非网络请求   BundleRequestAsync: BundleRequest
            } else {
                // BundleRequestAsync(): BundleRequest()
                bundleRequest = asyncMode ? new BundleRequestAsync() : new BundleRequest();
            }

            bundleRequest.name = url;

            _bundleRequestDict.Add(url, bundleRequest);

            if (MAX_BUNDLES_PERFRAME > 0 && (bundleRequest is BundleRequestAsync || bundleRequest is WebBundleRequest)) {
                _toloadBundleList.Add(bundleRequest);
            } else {
                // 真正加载ab的地方
                bundleRequest.Load();
                // 加入 _loadingBundleRequestList
                _loadingBundleRequestList.Add(bundleRequest);
                Log("LoadBundle: " + url);
            }

            // BundleRequest 的被引用 +1
            bundleRequest.Retain();

            // 返回 BundleRequest
            return bundleRequest;
        }

        // 获取 bunlde 的路径
        private static string GetDataPath(string bundleName) {
            // P 目录为空, 返回 S 目录
            if (string.IsNullOrEmpty(updatePath))
                return basePath;

            // P 目录存在这个  bundle, 返回 P目录
            if (File.Exists(updatePath + bundleName))
                return updatePath;

            // 返回 P 目录
            return basePath;
        }

        private static void UpdateBundles() {

            var max = MAX_BUNDLES_PERFRAME;
            if (_toloadBundleList.Count > 0 && max > 0 && _loadingBundleRequestList.Count < max) {
                for (var i = 0; i < Math.Min(max - _loadingBundleRequestList.Count, _toloadBundleList.Count); ++i) {
                    var item = _toloadBundleList[i];
                    if (item.loadState == LoadState.Init) {
                        item.Load();
                        _loadingBundleRequestList.Add(item);
                        _toloadBundleList.RemoveAt(i);
                        --i;
                    }
                }
            }

            // 处理 _loadingBundleRequestList
            for (var i = 0; i < _loadingBundleRequestList.Count; i++) {
                var item = _loadingBundleRequestList[i];
                // 跳过正在更新中的 BundleRequest
                if (item.Update())
                    continue;
                // 移除不在更新的 BundleRequest
                _loadingBundleRequestList.RemoveAt(i);
                --i;
            }


            foreach (var item in _bundleRequestDict) {
                if (item.Value.isDone && item.Value.IsUnused()) {
                    _unusedBundleRequestList.Add(item.Value);
                }
            }

            if (_unusedBundleRequestList.Count <= 0) return;
            {
                for (var i = 0; i < _unusedBundleRequestList.Count; i++) {
                    var item = _unusedBundleRequestList[i];
                    if (item.isDone) {
                        item.Unload();
                        _bundleRequestDict.Remove(item.name);
                        Log("UnloadBundle: " + item.name);
                    }
                }
                _unusedBundleRequestList.Clear();
            }
        }

        // 暂时没有 Variant
        private static string RemapVariantName(string assetBundleName) {
            var bundlesWithVariant = _activeVariants;
            // Get base bundle path
            var baseName = assetBundleName.Split('.')[0];

            var bestFit = int.MaxValue;
            var bestFitIndex = -1;

            // Loop all the assetBundles with variant to find the best fit variant assetBundle.
            for (var i = 0; i < bundlesWithVariant.Count; i++) {
                var curSplit = bundlesWithVariant[i].Split('.');
                var curBaseName = curSplit[0];
                var curVariant = curSplit[1];

                if (curBaseName != baseName)
                    continue;

                var found = bundlesWithVariant.IndexOf(curVariant);

                // If there is no active variant found. We still want to use the first
                if (found == -1)
                    found = int.MaxValue - 1;

                if (found >= bestFit)
                    continue;
                bestFit = found;
                bestFitIndex = i;
            }

            if (bestFit == int.MaxValue - 1)
                Debug.LogWarning(
                    "Ambiguous asset bundle variant chosen because there was no matching active variant: " +
                    bundlesWithVariant[bestFitIndex]);

            return bestFitIndex != -1 ? bundlesWithVariant[bestFitIndex] : assetBundleName;
        }

        #endregion
    }
}