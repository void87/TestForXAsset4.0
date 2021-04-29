//
// BuildScript.cs
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

namespace libx
{
	public static class BuildScript
	{
		public static string outputPath  = "DLC/" + GetPlatformName(); 

		public static void ClearAssetBundles ()
		{
			var allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames ();
			for (var i = 0; i < allAssetBundleNames.Length; i++) {
				var text = allAssetBundleNames [i];
				if (EditorUtility.DisplayCancelableProgressBar (
					                string.Format ("Clear AssetBundles {0}/{1}", i, allAssetBundleNames.Length), text,
					                i * 1f / allAssetBundleNames.Length))
					break;

				AssetDatabase.RemoveAssetBundleName (text, true);
			} 
			EditorUtility.ClearProgressBar ();
		}

        // 修改 {BuildRules} 也就是Rules.asset
		internal static void ApplyBuildRules ()
		{
            var rules = GetBuildRules();
            rules.Apply();
		}

        // 获取 Rules.asset
        // {BuildRules} 对应 Rules.asset
        internal static BuildRules GetBuildRules() {
            return GetAsset<BuildRules>("Assets/Rules.asset");
        }

        public static void CopyAssetBundlesTo(string path) {
            var files = new[] {
                Versions.ResName,
                Versions.VerName,
            };
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            foreach (var item in files) {
                var src = outputPath + "/" + item;
                var dest = Application.streamingAssetsPath + "/" + item;
                if (File.Exists(src)) {
                    File.Copy(src, dest, true);
                }
            }
        }

		public static string GetPlatformName ()
		{
			return GetPlatformForAssetBundles (EditorUserBuildSettings.activeBuildTarget);
		}

		private static string GetPlatformForAssetBundles (BuildTarget target)
		{
			// ReSharper disable once SwitchStatementMissingSomeCases
			switch (target) {
			case BuildTarget.Android:
				return "Android";
			case BuildTarget.iOS:
				return "iOS";
			case BuildTarget.WebGL:
				return "WebGL";
			case BuildTarget.StandaloneWindows:
			case BuildTarget.StandaloneWindows64:
				return "Windows";
#if UNITY_2017_3_OR_NEWER
			case BuildTarget.StandaloneOSX:
				return "OSX";
#else
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                case BuildTarget.StandaloneOSXUniversal:
                    return "OSX";
#endif
			default:
				return null;
			}
		}

		private static string[] GetLevelsFromBuildSettings ()
		{
			List<string> scenes = new List<string> ();
			foreach (var item in GetBuildRules().scenesInBuild) {
				var path = AssetDatabase.GetAssetPath (item);
				if (!string.IsNullOrEmpty (path)) {
					scenes.Add (path);
				}
			}

			return scenes.ToArray ();
		}

		private static string GetAssetBundleManifestFilePath ()
		{
			var relativeAssetBundlesOutputPathForPlatform = Path.Combine ("Asset", GetPlatformName ());
			return Path.Combine (relativeAssetBundlesOutputPathForPlatform, GetPlatformName ()) + ".manifest";
		}

		public static void BuildStandalonePlayer ()
		{
			var outputPath =
				Path.Combine (Environment.CurrentDirectory,
					"Build/" + GetPlatformName ()
                        .ToLower ()); //EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");
			if (outputPath.Length == 0)
				return;

			var levels = GetLevelsFromBuildSettings ();
			if (levels.Length == 0) {
				Debug.Log ("Nothing to build.");
				return;
			}

			var targetName = GetBuildTargetName (EditorUserBuildSettings.activeBuildTarget);
			if (targetName == null)
				return;
#if UNITY_5_4 || UNITY_5_3 || UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			BuildOptions option = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None;
			BuildPipeline.BuildPlayer(levels, path + targetName, EditorUserBuildSettings.activeBuildTarget, option);
#else
			var buildPlayerOptions = new BuildPlayerOptions {
				scenes = levels,
				locationPathName = outputPath + targetName,
				assetBundleManifestPath = GetAssetBundleManifestFilePath (),
				target = EditorUserBuildSettings.activeBuildTarget,
				options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None
			};
			BuildPipeline.BuildPlayer (buildPlayerOptions);
#endif
		}

        // 创建 ab 目录
        public static string CreateAssetBundleDirectory() {
            // Choose the output path according to the build target.
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            return outputPath;
        }

        public static void BuildAssetBundles() {
            // 创建 ab 目录
            var outputPath = CreateAssetBundleDirectory();
            // ChunkBasedCompression
            const BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;
            // 获取生成ab的平台
            var targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            // 获取 Rules.asset
            var buildRules = GetBuildRules();
            // 将 RuleBundle[] 转化为 AssetBundleBuild[]
            var assetBundleBuilds = buildRules.GetBuilds();
            // 第一次打包
            // 调用 官方API 开始打包, 生成 官方的 AssetBundleManifest
            // 也就是 ab包 输出目录下的  {目录名.manifest}
            //
            // 官方自己的 API 会自动处理 需要打包的文件的变化
            // 如果没有变化是不会重新打 bundle 的
            // AssetBundleManifest 这个文件 每次打包都会重新生成
            AssetBundleManifest assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, assetBundleBuilds, options, targetPlatform);

            if (assetBundleManifest == null) {
                return;
            }

            // 获取 {Manifest} 也就是 Asssets/Manifest.asset
            Manifest manifest = GetManifest();
            // 目录
            var dirs = new List<string>();
            // 
            List<AssetRef> assetRefList = new List<AssetRef>();
            // 通过 官方的 AssetBundleManifest 获取 所有的 bundle 名
            // e.g. [assets/test/3stageselect/test1.unity3d, assets/test/3stageselect/test2.unity3d]
            string[] bundleNameArray = assetBundleManifest.GetAllAssetBundles();

            // 用一个 Map 记录 bundle名字,id
            Dictionary<string, int> bundleName2IdMap = new Dictionary<string, int>();
            for (var index = 0; index < bundleNameArray.Length; index++) {
                var bundle = bundleNameArray[index];
                // e.g. [assets/test/3stageselect/test1.unity3d, 0]
                bundleName2IdMap[bundle] = index;
            }

            var bundleRefs = new List<BundleRef>();
            for (var index = 0; index < bundleNameArray.Length; index++) {
                var bundle = bundleNameArray[index];
                // 通过 官方的 AssetBundleManifest 获取 单个 bundle 的 依赖
                string[] deps = assetBundleManifest.GetAllDependencies(bundle);

                var path = string.Format("{0}/{1}", outputPath, bundle);
                if (File.Exists(path)) {
                    // 读取指定路径的 bundle
                    using (var stream = File.OpenRead(path)) {
                        bundleRefs.Add(new BundleRef {
                            bundleName = bundle,  // bundle 名
                            bundleIndex = index, // bundleNameArray 中的索引
                            // 获取 所有 依赖 的  索引
                            deps = Array.ConvertAll(deps, input => bundleName2IdMap[input]),
                            // 通过 FileStream 获取 文件的 大小
                            len = stream.Length,
                            // 通过 官方的 AssetBundleManifest 获取 bundle 的 hash
                            hash = assetBundleManifest.GetAssetBundleHash(bundle).ToString(),
                        });
                    }
                } else {
                    Debug.LogError(path + " file not exsit.");
                }
            }

            for (var i = 0; i < buildRules.ruleAssets.Length; i++) {
                var item = buildRules.ruleAssets[i];
                var path = item.path;
                // 获取asset 所在的目录
                string dir = Path.GetDirectoryName(path).Replace("\\", "/");
                var index = dirs.FindIndex(o => o.Equals(dir));
                if (index == -1) {
                    index = dirs.Count;
                    // 添加到 目录 集合
                    dirs.Add(dir);
                }

                AssetRef assetRef = new AssetRef {
                    bundleIndex = bundleName2IdMap[item.bundle], // bundle 索引
                    dirIndex = index,    // 目录索引
                    name = Path.GetFileName(path)   // asset名
                };
                assetRefList.Add(assetRef);
            }

            manifest.dirs = dirs.ToArray();
            manifest.assetRefArray = assetRefList.ToArray();
            manifest.bundleRefArray = bundleRefs.ToArray();

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 自定义 Assets/Manifest.asset, bunlde 名为 manifest.unity3d
            var manifestBundleName = "manifest.unity3d";
            assetBundleBuilds = new[] {
                new AssetBundleBuild {
                    assetNames = new[] {
                        AssetDatabase.GetAssetPath (manifest)
                    },
                    assetBundleName = manifestBundleName
                }
            };

            // 第二次打包, 将 Assets/Manifest.asset 打包为 manifest.unity3d
            // 如果 Assets/Manifest.asset 没有变化,就不会重新打包这个 bundle
            BuildPipeline.BuildAssetBundles(outputPath, assetBundleBuilds, options, targetPlatform);

            // 将 manifest.untiy3d 添加到 bundleNameArray
            ArrayUtility.Add(ref bundleNameArray, manifestBundleName);

            // 先创建 res, 再创建 ver, 每次打包都会重新创建这个文件
            Versions.BuildVersions(outputPath, bundleNameArray, GetBuildRules().AddVersion());
        }

		private static string GetBuildTargetName (BuildTarget target)
		{
			var time = DateTime.Now.ToString ("yyyyMMdd-HHmmss");
			var name = PlayerSettings.productName + "-v" + PlayerSettings.bundleVersion + ".";
			switch (target) {
			case BuildTarget.Android:
				return string.Format ("/{0}{1}-{2}.apk", name, GetBuildRules().version, time);

			case BuildTarget.StandaloneWindows:
			case BuildTarget.StandaloneWindows64:
				return string.Format ("/{0}{1}-{2}.exe", name, GetBuildRules().version, time);

#if UNITY_2017_3_OR_NEWER
			case BuildTarget.StandaloneOSX:
				return "/" + name + ".app";

#else
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                case BuildTarget.StandaloneOSXUniversal:
                    return "/" + path + ".app";

#endif

			case BuildTarget.WebGL:
			case BuildTarget.iOS:
				return "";
			// Add more build targets for your own.
			default:
				Debug.Log ("Target not implemented.");
				return null;
			}
		}

        private static T GetAsset<T>(string path) where T : ScriptableObject {
            // 没有就创建
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }

        // 获取 {Manifest} 也就是 Manifest.asset
		public static Manifest GetManifest ()
		{
			return GetAsset<Manifest> (Assets.ManifestAsset);
		}
	}
}