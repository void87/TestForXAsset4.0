//
// BuildRules.cs
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
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace libx
{
    public enum NameBy
    {
        Explicit,   // ab包按自定义名字分割, 多个文件合为一个ab包
        Path,   // ab包按路径分割, 也就是一个文件一个ab(因为一个文件一个路径)
        Directory,  // ab包按文件夹分割, 有嵌套文件夹则单独区分
        TopDirectory    // ab包按文件夹分割, 不能有不在子文件夹里的文件
    }

    // asset和它所属的bundle,有多少个asset就有多少个 {RuleAsset} 
    [Serializable]
    public class RuleAsset
    {
        // ab包名 bundle e.g. "assets/test/3stageselect/test1.unity3d"
        public string bundle;
        // asset名 asset e.g. "Assets/Test/3StageSelect/Test1/bg_Stage1_01.png"
        public string path;
    }

    // bundle和它包含的assets
    [Serializable]
    public class RuleBundle
    {
        // bundle名 e.g. "assets/test/3stageselect/test1.unity3d"
        public string name;
        // asset名 e.g. ["Assets/Test/3StageSelect/Test1.bg_Stage1_01.png", "Assets/Test/3StageSelect/Test1/Test11/bg_Stage1_02.png"]
        public string[] assets;
    }

    [Serializable]
    public class BuildRule
    {
        // e.g. Assets/Test/3StageSelect
        [Tooltip("搜索路径")] public string searchPath;

        // e.g. *.txt, *.bytes
        [Tooltip("搜索通配符，多个之间请用,(逗号)隔开")] public string searchPattern;

        // e.g. 
        // Explicit         assetbundle的名称为自定义
        // Path             assetbundle的名称为按文件路径
        // Directory        assetbundle的名称为文件所属的第一级父文件夹
        // Top Directory    assetbundle的名称为设置文件夹下的第一级子文件夹
        [Tooltip("命名规则")] public NameBy nameBy = NameBy.Path;

        // e.g. assetbundle的自定义名称
        [Tooltip("Explicit的名称")] public string assetBundleName;

        // 获取 searchPath, 必须是文件夹, 下符合 searchPattern 要求的文件
        public string[] GetAssets() {
            // 按 , 分割后缀名
            var patterns = searchPattern.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // 判断文件夹是否存在
            // 错误, searchPath 是单独的文件 e.g. Assets/Test/3StageSelect/Battlefield 2.png,
            // 正确, searchPath 是文件夹 e.g. Assets/Test/3StageSelect
            if (!Directory.Exists(searchPath)) {
                Debug.LogWarning("Rule searchPath not exist:" + searchPath);
                return new string[0];
            }

            var getFiles = new List<string>();
            // 通过不同的后缀名查找文件
            foreach (var pattern in patterns) {
                var files = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
                foreach (var file in files) {
                    // 跳过目录
                    if (Directory.Exists(file)) {
                        continue;
                    }

                    // 获取后缀名
                    var ext = Path.GetExtension(file).ToLower();
                    // 跳过非法文件
                    if ((ext == ".fbx" || ext == ".anim") && !pattern.Contains(ext)) {
                        continue;
                    }

                    // 跳过非法文件
                    if (!BuildRules.ValidateAsset(file)) {
                        continue;
                    }

                    var asset = file.Replace("\\", "/");

                    getFiles.Add(asset);
                }
            }

            return getFiles.ToArray();
        }
    }

    public class BuildRules : ScriptableObject
    {
        // [asset名, bundle名]
        // e.g. [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, assets/test/3stageselect/test1.unity3d]
        private readonly Dictionary<string, string> _asset2Bundles = new Dictionary<string, string>();

        // [budle名, [asset名]], 场景文件和非场景文件放在一个文件夹时
        // 这个ab包和其所包含的asset数组会被放进 _conflicted里, 供 _duplicated 使用
        private readonly Dictionary<string, string[]> _conflicted = new Dictionary<string, string[]>();

        // [asset, HashSet<bundle>]
        // asset 所属的 bundles, 主要是为 计算出_duplicated, 没有别的用处
        // 如果一个asset被多处引用且没有加入到 _asset2Bundles中， 那这个 asset 就会被加入到 _duplicated
        private readonly Dictionary<string, HashSet<string>> _tracker = new Dictionary<string, HashSet<string>>();

        // [asset名]
        // 没有设置过 {BuildRule} 的 asset, 被不同的设置过 {BuildRule} 的asset 引用
        // e.g. Assets/XAsset/Demo/UI/3StageSelect/Battlefield 2.png
        private readonly List<string> _duplicated = new List<string>();


		[Header("Patterns")]
        // .asset 文件搜索模式
		public string searchPatternAsset = "*.asset";
        // .controller 文件搜索模式
		public string searchPatternController = "*.controller";
        // 文件夹 文件搜索模式
		public string searchPatternDir = "*";
        // 材质文件搜索模式
		public string searchPatternMaterial = "*.mat";
        // 图片文件搜索模式
		public string searchPatternPng = "*.png";
        // prefab文件搜索模式
		public string searchPatternPrefab = "*.prefab";
        // 场景文件搜索模式
		public string searchPatternScene = "*.unity";
        // 文本文件搜索模式
		public string searchPatternText = "*.txt,*.bytes,*.json,*.csv,*.xml,*htm,*.html,*.yaml,*.fnt";
        // 名字转换为 Hash
        public static bool nameByHash = false;
        
		[Tooltip("构建的版本号")]
		[Header("Builds")] 
        public int version;
        [Tooltip("BuildPlayer 的时候被打包的场景")] public SceneAsset[] scenesInBuild = new SceneAsset[0]; 
        public BuildRule[] rules = new BuildRule[0]; 
		[Header("Assets")]
		[HideInInspector]public RuleAsset[] ruleAssets = new RuleAsset[0];
        [HideInInspector]public RuleBundle[] ruleBundles = new RuleBundle[0];
        #region API

        public int AddVersion()
        {
            version = version + 1;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return version;
        }

        // 解析 Rules.asset
        public void Apply()
        {
            Clear();
            // 收集 {BuildRule} 下的 asset
            CollectAssets();
            // 获取依赖, 分析出 _conflicted, _tracker, _duplicated
            AnalysisAssets();
            // 优化资源
            OptimizeAssets();
            // 保存 Builrules
            Save();
        }

        // 将 RuleBundle[] 转化为 AssetBundleBuild[]
        public AssetBundleBuild[] GetBuilds() {
            var builds = new List<AssetBundleBuild>();
            foreach (var bundle in ruleBundles) {
                builds.Add(new AssetBundleBuild {
                    assetNames = bundle.assets,
                    assetBundleName = bundle.name
                });
            }

            return builds.ToArray();
        }

        #endregion

        #region Private

        // 排除不符合规则的资源
        internal static bool ValidateAsset(string asset)
        {
            if (!asset.StartsWith("Assets/")) {
                return false;
            }

            var ext = Path.GetExtension(asset).ToLower();
            return ext != ".dll" && ext != ".cs" && ext != ".meta" && ext != ".js" && ext != ".boo";
        }

        // 是否是 场景文件
        private static bool IsScene(string asset)
        {
            return asset.EndsWith(".unity");
        }

        // 加密与否, 组合扩展名
        private static string RuledAssetBundleName(string name)
        {
            if (nameByHash)
            {
                return Utility.GetMD5Hash(name) + Assets.Extension; 
            } 
            return name.Replace("\\", "/").ToLower() + Assets.Extension;
        }

        // 记录 asset 所属的 bundles(可能有多个)
        // 添加 _tracker 记录
        // 添加 _duplicated 记录
        private void Track(string asset, string bundle)
        {
            // 跟踪  asset 对应的 bundle(s)
            HashSet<string> bundles;
            if (!_tracker.TryGetValue(asset, out bundles))
            {
                bundles = new HashSet<string>();
                _tracker.Add(asset, bundles);
            }

            bundles.Add(bundle);

            // 单个 asset 被多个 asset 引用
            if (bundles.Count > 1)
            {
                string bundleName;
                _asset2Bundles.TryGetValue(asset, out bundleName);
                // 如果 asset 不在任何 bundle 里, 且被 设置过 {BuildRule} 的 不同的asset 引用
                // 就会添加到 _duplicated 里
                if (string.IsNullOrEmpty(bundleName))
                {
                    Debug.Log("Duplicated.Asset: " + asset);

                    _duplicated.Add(asset);
                }
            }
        }

        // 将 _asset2Bundles也就是 <asset名, bundle名> 转化为 
        // 临时的 Dictionary<bundle名, List<asset名>> 返回
        // [assets/test/3stageselect/test1.unity3d, [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, Assets/Test/3StageSelect/Test1/Test11/bg_Stage1_02.png]]
        private Dictionary<string, List<string>> GetBundles() {
            var bundles = new Dictionary<string, List<string>>();
            foreach (var item in _asset2Bundles) {
                var bundle = item.Value;
                List<string> list;
                if (!bundles.TryGetValue(bundle, out list)) {
                    list = new List<string>();
                    bundles[bundle] = list;
                }

                if (!list.Contains(item.Key)) {
                    list.Add(item.Key);
                }
            }

            return bundles;
        }

        private void Clear()
        {
            _tracker.Clear();
            _duplicated.Clear();
            _conflicted.Clear();
            _asset2Bundles.Clear();
        }

        private void Save() {
            var getBundles = GetBundles();
            // 每个 bundle 一个 RuleBulde
            ruleBundles = new RuleBundle[getBundles.Count];
            var i = 0;
            foreach (var item in getBundles) {
                ruleBundles[i] = new RuleBundle {
                    name = item.Key,
                    assets = item.Value.ToArray()
                };
                i++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void OptimizeAssets() {
            // _conflicted 中的 非场景文件 加入到 _duplicated中
            int i = 0, max = _conflicted.Count;
            foreach (var item in _conflicted) {
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冲突{0}/{1}", i, max), item.Key,
                    i / (float)max)) break;
                var list = item.Value;
                foreach (var asset in list) {
                    if (!IsScene(asset)) {
                        _duplicated.Add(asset);
                    }
                }
                i++;
            }

            // 处理每个 _duplicated
            for (i = 0, max = _duplicated.Count; i < max; i++) {
                var item = _duplicated[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冗余{0}/{1}", i, max), item,
                    i / (float)max)) {
                    break;
                }
                OptimizeAsset(item);
            }
        }

        // 获取依赖, 分析出 _conflicted, _tracker, _duplicated
        private void AnalysisAssets()
        {
            var getBundles = GetBundles();

            int i = 0, max = getBundles.Count;

            foreach (var item in getBundles)
            {
                var bundle = item.Key;
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("分析依赖{0}/{1}", i, max), bundle,
                    i / (float) max)) break;

                // 获取单个ab包里的asset名(可能有多个)
                var assetPaths = getBundles[bundle];



                // 单个ab包里包含场景文件和非场景文件, 加入到 _conflicted
                if (assetPaths.Exists(IsScene) && !assetPaths.TrueForAll(IsScene)) {
                    _conflicted.Add(bundle, assetPaths.ToArray());
                }

                // 获取 单个 ab包里 所有的 asset的依赖
                // 这些依赖 形如  Assets/Test/Scenes/Battlefield 2.png
                // 同时也会自动处理 prefab 嵌套的问题
                var dependencies = AssetDatabase.GetDependencies(assetPaths.ToArray(), true);


                if (dependencies.Length > 0) {
                    foreach (var asset in dependencies) {
                        if (ValidateAsset(asset)) {
                            Track(asset, bundle);
                        }
                    }
                }

                i++;
            }
        }

        // 通过 BuildRule 获得需要打包的文件路径和该文件所属的assetbundle的名称 将其转化为 RuleAsset
        // BuildRule 里的路径必须是文件夹
        private void CollectAssets()
        {
            for (int i = 0, max = rules.Length; i < max; i++) {
                var rule = rules[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("收集资源{0}/{1}", i, max), rule.searchPath,
                    i / (float)max))
                    break;
                // 获取每个 BuildRule 下的符合规则的文件
                ApplyRule(rule);
            }

            // _asset2Bundles 转换为 临时的 List<RuleAsset> 
            var list = new List<RuleAsset>();
            foreach (var item in _asset2Bundles)
                list.Add(new RuleAsset {
                    path = item.Key,
                    bundle = item.Value
                });
            // 按 asset名 排序
            list.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            ruleAssets = list.ToArray();
        }

        // 优化asset
        private void OptimizeAsset(string asset)
        {
            // 所有的 shader 放到 单独的 shader bundle 中
            if (asset.EndsWith(".shader")) {
                _asset2Bundles[asset] = RuledAssetBundleName("shaders");
            // 没每个asset 单独设置 bundle
            } else {
                _asset2Bundles[asset] = RuledAssetBundleName(asset);
            }
        }

        // 通过 BuildRule 查找文件信息，将这些文件信息存放到 _asset2Bundles
        private void ApplyRule(BuildRule rule)
        {
            var assets = rule.GetAssets();
            switch (rule.nameBy)
            {
                // ab包按自定义名字分割, 多个文件合为一个ab包
                case NameBy.Explicit: {
                        // nameByHash = false
                        // e.g.
                        // [Assets/Test/3StageSelect/Battlefield 2.png, test.unity3d]
                        // [Assets/Test/3StageSelect/bg_Name1.png, test.unity3d]
                        // [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, test.unity3d]
                        foreach (var asset in assets) {
                            _asset2Bundles[asset] = RuledAssetBundleName(rule.assetBundleName);
                        }

                        break;
                    }
                // ab包按路径分割, 也就是一个文件一个ab(因为一个文件一个路径)
                case NameBy.Path: {
                        // nameByHash = true e.g. [Assets/Test/3StageSelect/Battlefield 2.png, 2a22374f1202ddb786fc83bd496d7e56.unity3d]
                        // nameByHash = false e.g. [Assets/Test/3StageSelect/Battlefield 2.png, assets/test/3stageselect/battlefield 2.png.unity3d]
                        foreach (var asset in assets) {
                            _asset2Bundles[asset] = RuledAssetBundleName(asset);
                        }

                        break;
                    }
                // ab包按文件夹分割, 有嵌套文件夹则单独区分
                case NameBy.Directory: {
                        // nameByHash = false
                        // e.g.
                        // [Assets/Test/3StageSelect/Battlefield 2.png, assets/test/3stageselect.unity3d]
                        // [Assets/Test/3StageSelect/bg_Name1.png, assets/test/3stageselect.unity3d]
                        // [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, assets/test/3stageselect/test1.unity3d]
                        foreach (var asset in assets) {
                            _asset2Bundles[asset] = RuledAssetBundleName(Path.GetDirectoryName(asset));
                        }

                        break;
                    }
                // ab包按文件夹分割, 不能有不在子文件夹里的文件
                // e.g. 文件结构
                // 3StageSelect
                //  Test1
                //      Test11(包含在Test1中)
                //  Test2
                //      Test21(包含在Test2中)
                case NameBy.TopDirectory:
                {
                    var startIndex = rule.searchPath.Length;
                    foreach (var asset in assets)
                    {
                        // var dir = Path.GetDirectoryName(asset); 源码bug
                        var dir = Path.GetDirectoryName(asset).Replace("\\", "/");
                        if (!string.IsNullOrEmpty(dir))
                        if (!dir.Equals(rule.searchPath))
                        {
                            var pos = dir.IndexOf("/", startIndex + 1, StringComparison.Ordinal);
                            if (pos != -1) dir = dir.Substring(0, pos);
                        }
                        // nameByHash = false
                        // e.g.
                        // [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, assets/test/3stageselect/test1.unity3d]
                        // [Assets/Test/3StageSelect/Test1/Test11/bg_Stage1_02.png, assets/test/3stageselect/test1.unity3d]
                        // [Assets/Test/3StageSelect/Test2/Battlefield 2.png, assets/test/3stageselect/test2.unity3d]
                        // [Assets/Test/3StageSelect/Test21/Battlefield 3.png, assets/test/3stageselect/test2.unity3d]
                        _asset2Bundles[asset] = RuledAssetBundleName(dir);
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion
    }
}