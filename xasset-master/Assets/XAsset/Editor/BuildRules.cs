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
        Explicit,
        Path,
        Directory,
        TopDirectory
    }

    // asset和它所属的bundle,有多少个asset就有多少个 {RuleAsset} 
    [Serializable]
    public class RuleAsset
    {
        // bundle e.g. "assets/test/3stageselect/test1.unity3d"
        public string bundle;
        // asset e.g. "Assets/Test/3StageSelect/Test1/bg_Stage1_01.png"
        public string path;
    }

    // bundle和它包含的assets
    [Serializable]
    public class RuleBundle
    {
        // asset e.g. "assets/test/3stageselect/test1.unity3d"
        public string name;
        // bundle e.g. ["Assets/Test/3StageSelect/Test1.bg_Stage1_01.png", "Assets/Test/3StageSelect/Test1/Test11/bg_Stage1_02.png"]
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

        // 获取 searchPath 下符合 searchPattern 要求的文件
        public string[] GetAssets()
        {
            var patterns = searchPattern.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);

            // 判断文件夹是否存在
            // 错误, searchPath 是单独的文件 e.g. Assets/Test/3StageSelect/Battlefield 2.png,
            // 正确, searchPath 是文件夹 e.g. Assets/Test/3StageSelect
            if (!Directory.Exists(searchPath))
            {
                Debug.LogWarning("Rule searchPath not exist:" + searchPath);
                return new string[0];
            }

            var getFiles = new List<string>();
            // 通过不同的后缀名查找文件
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (Directory.Exists(file)) {
                        continue;
                    }

                    var ext = Path.GetExtension(file).ToLower();
                    // 跳过非法文件
                    if ((ext == ".fbx" || ext == ".anim") && !pattern.Contains(ext)) continue;

                    // 跳过非法文件
                    if (!BuildRules.ValidateAsset(file)) continue;
                    var asset = file.Replace("\\", "/");
                    getFiles.Add(asset);
                }
            }

            return getFiles.ToArray();
        }
    }

    public class BuildRules : ScriptableObject
    {
        // [asset, bundle]
        // e.g. [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, assets/test/3stageselect/test1.unity3d]
        private readonly Dictionary<string, string> _asset2Bundles = new Dictionary<string, string>();
        private readonly Dictionary<string, string[]> _conflicted = new Dictionary<string, string[]>();
        // [asset]
        // 没有设置过 {BuildRule} 的 asset
        // e.g. Assets/XAsset/Demo/UI/3StageSelect/Battlefield 2.png
        private readonly List<string> _duplicated = new List<string>();
        // [asset, HashSet<bundle>]
        // asset 所属的 bundles
        // e.g.
        // [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, [assets/test/3stageselect/test1.unity3d]]
        private readonly Dictionary<string, HashSet<string>> _tracker = new Dictionary<string, HashSet<string>>();
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

        public void Apply()
        {
            Clear();
            CollectAssets();
            AnalysisAssets();
            OptimizeAssets();
            Save();
        }

        // 将 RuleBundle[] 转化为 AssetBundleBuild[]
        public AssetBundleBuild[] GetBuilds()
        {
            var builds = new List<AssetBundleBuild>();
            foreach (var bundle in ruleBundles)
            {
                builds.Add(new AssetBundleBuild
                {
                    assetNames = bundle.assets,
                    assetBundleName = bundle.name
                });
            }

            return builds.ToArray();
        }

        #endregion

        #region Private

        internal static bool ValidateAsset(string asset)
        {
            if (!asset.StartsWith("Assets/")) return false;

            var ext = Path.GetExtension(asset).ToLower();
            return ext != ".dll" && ext != ".cs" && ext != ".meta" && ext != ".js" && ext != ".boo";
        }

        private static bool IsScene(string asset)
        {
            return asset.EndsWith(".unity");
        }

        private static string RuledAssetBundleName(string name)
        {
            if (nameByHash)
            {
                return Utility.GetMD5Hash(name) + Assets.Extension; 
            } 
            return name.Replace("\\", "/").ToLower() + Assets.Extension;
        }

        // 记录 asset 所属的 bundles(可能有多个)
        private void Track(string asset, string bundle)
        {
            HashSet<string> bundles;
            if (!_tracker.TryGetValue(asset, out bundles))
            {
                bundles = new HashSet<string>();
                _tracker.Add(asset, bundles);
            }

            bundles.Add(bundle);
            if (bundles.Count > 1)
            {
                string bundleName;
                _asset2Bundles.TryGetValue(asset, out bundleName);
                // 如果 asset 不在任何 bundle 里, 添加到 duplicated内
                if (string.IsNullOrEmpty(bundleName))
                {
                    Debug.Log("Duplicated.Asset: " + asset);

                    _duplicated.Add(asset);
                }
            }
        }

        // 将 _asset2Bundles 转化为 Dictionary<string, List<string>>
        // [bundle, asset] e.g.
        // [assets/test/3stageselect/test1.unity3d, [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, Assets/Test/3StageSelect/Test1/Test11/bg_Stage1_02.png]]
        private Dictionary<string, List<string>> GetBundles()
        {
            var bundles = new Dictionary<string, List<string>>();
            foreach (var item in _asset2Bundles)
            {
                var bundle = item.Value;
                List<string> list;
                if (!bundles.TryGetValue(bundle, out list))
                {
                    list = new List<string>();
                    bundles[bundle] = list;
                }

                if (!list.Contains(item.Key)) list.Add(item.Key);
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

        private void Save()
        {
            var getBundles = GetBundles();
            ruleBundles = new RuleBundle[getBundles.Count];
            var i = 0;
            foreach (var item in getBundles)
            {
                ruleBundles[i] = new RuleBundle
                {
                    name = item.Key,
                    assets = item.Value.ToArray()
                };
                i++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void OptimizeAssets()
        {
            int i = 0, max = _conflicted.Count;
            foreach (var item in _conflicted)
            {
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冲突{0}/{1}", i, max), item.Key,
                    i / (float) max)) break;
                var list = item.Value;
                foreach (var asset in list)
                    if (!IsScene(asset))
                        _duplicated.Add(asset);
                i++;
            }

            for (i = 0, max = _duplicated.Count; i < max; i++)
            {
                var item = _duplicated[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冗余{0}/{1}", i, max), item,
                    i / (float) max)) break;
                OptimizeAsset(item);
            }
        }

        private void AnalysisAssets()
        {
            var getBundles = GetBundles();
            int i = 0, max = getBundles.Count;
            foreach (var item in getBundles)
            {
                var bundle = item.Key;
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("分析依赖{0}/{1}", i, max), bundle,
                    i / (float) max)) break;

                // 获取ab包里的文件(可能有多个)
                var assetPaths = getBundles[bundle];

                // 场景文件判断
                if (assetPaths.Exists(IsScene) && !assetPaths.TrueForAll(IsScene))
                    _conflicted.Add(bundle, assetPaths.ToArray());

                // 获取依赖
                var dependencies = AssetDatabase.GetDependencies(assetPaths.ToArray(), true);
                if (dependencies.Length > 0)
                    foreach (var asset in dependencies)
                        if (ValidateAsset(asset))
                            Track(asset, bundle);
                i++;
            }
        }

        // 通过 BuildRule 获得需要打包的文件路径和该文件所属的assetbundle的名称
        // 将其转化为 RuleAsset
        private void CollectAssets()
        {
            for (int i = 0, max = rules.Length; i < max; i++)
            {
                var rule = rules[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("收集资源{0}/{1}", i, max), rule.searchPath,
                    i / (float) max))
                    break;
                ApplyRule(rule);
            }

            var list = new List<RuleAsset>();
            foreach (var item in _asset2Bundles)
                list.Add(new RuleAsset
                {
                    path = item.Key,
                    bundle = item.Value
                });
            list.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            ruleAssets = list.ToArray();
        }

        private void OptimizeAsset(string asset)
        {
            if (asset.EndsWith(".shader"))
                _asset2Bundles[asset] = RuledAssetBundleName("shaders");
            else
                _asset2Bundles[asset] = RuledAssetBundleName(asset);
        }

        // 通过 BuildRule 查找文件信息，将这些文件信息存放到 _asset2Bundles
        private void ApplyRule(BuildRule rule)
        {
            var assets = rule.GetAssets();
            switch (rule.nameBy)
            {
                // ab包按自定义名字分割, 多个文件合为一个ab包
                case NameBy.Explicit:
                {
                    // nameByHash = false
                    // e.g.
                    // [Assets/Test/3StageSelect/Battlefield 2.png, test.unity3d]
                    // [Assets/Test/3StageSelect/bg_Name1.png, test.unity3d]
                    // [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, test.unity3d]
                    foreach (var asset in assets) _asset2Bundles[asset] = RuledAssetBundleName(rule.assetBundleName);

                    break;
                }
                // ab包按路径分割, 也就是一个文件一个ab(因为一个文件一个路径)
                case NameBy.Path:
                {
                    // nameByHash = true e.g. [Assets/Test/3StageSelect/Battlefield 2.png, 2a22374f1202ddb786fc83bd496d7e56.unity3d]
                    // nameByHash = false e.g. [Assets/Test/3StageSelect/Battlefield 2.png, assets/test/3stageselect/battlefield 2.png.unity3d]
                    foreach (var asset in assets) _asset2Bundles[asset] = RuledAssetBundleName(asset);

                    break;
                }
                // ab包按文件夹分割, 有嵌套文件夹则单独区分
                case NameBy.Directory:
                {
                    // nameByHash = false
                    // e.g.
                    // [Assets/Test/3StageSelect/Battlefield 2.png, assets/test/3stageselect.unity3d]
                    // [Assets/Test/3StageSelect/bg_Name1.png, assets/test/3stageselect.unity3d]
                    // [Assets/Test/3StageSelect/Test1/bg_Stage1_01.png, assets/test/3stageselect/test1.unity3d]
                    foreach (var asset in assets) _asset2Bundles[asset] = RuledAssetBundleName(Path.GetDirectoryName(asset));

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