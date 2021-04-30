//
// EditorRuntimeInitializeOnLoad.cs
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

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace libx
{
    public class EditorRuntimeInitializeOnLoad
    {
        // Editor 在游戏启动时
        [RuntimeInitializeOnLoadMethod]
        private static void OnInitialize() {
            Debug.Log("RuntimeInitalizeOnLoadMethod");

            // e.g. DLC/Windows\\
            // runtimeMode 下 会替换成 S 目录或者 P 目录
            Assets.basePath = BuildScript.outputPath + Path.DirectorySeparatorChar;
            // 设置 Assets.loadDelegate 用于 !Assets.runtimeMode
            Assets.loadDelegate = AssetDatabase.LoadAssetAtPath;

            // 读取 BuildRules
            var rules = BuildScript.GetBuildRules();

            List<string> assetNameList = new List<string>();
            // 遍历 BuildRules.scenesInBuild
            foreach (SceneAsset sceneAsset in rules.scenesInBuild) {
                // 获取 场景的路径
                // e.g. Assets/XAsset/Demo/Scenes/Game.unity
                var path = AssetDatabase.GetAssetPath(sceneAsset);
                if (string.IsNullOrEmpty(path)) {
                    continue;
                }
                // 添加到 assetNameList
                assetNameList.Add(path);
            }

            // 遍历 BuildRule, 搜索 .unity
            foreach (BuildRule buildRule in rules.rules) {
                if (buildRule.searchPattern.Contains("*.unity")) {
                    // 添加到 assetNameList
                    assetNameList.AddRange(buildRule.GetAssets());
                }
            }


            // 设置 Scenes In Build
            var scenes = new EditorBuildSettingsScene[assetNameList.Count];
            for (var index = 0; index < assetNameList.Count; index++) {
                var asset = assetNameList[index];
                scenes[index] = new EditorBuildSettingsScene(asset, true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        // 编辑器启动, 不是游戏启动
        [InitializeOnLoadMethod]
        private static void OnEditorInitialize()
        {
            EditorUtility.ClearProgressBar();
            //BuildScript.GetManifest();
            //BuildScript.GetBuildRules();
        }
    }
}