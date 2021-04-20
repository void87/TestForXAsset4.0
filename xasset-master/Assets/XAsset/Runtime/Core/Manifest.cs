//
// Manifest.cs
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
using UnityEngine;

namespace libx {
    [Serializable]
    public class AssetRef {
        // asset名称 e.g. bg_Stage1_02.png
        public string name;
        // asset所属的bundle的索引
        public int bundle;
        // asset所属的文件夹的索引
        public int dir;
    }

    [Serializable]
    public class BundleRef {
        // bundle名称 e.g. assets/test/3stageselect/test1.unity3d
        public string name;
        // bundle索引 e.g. 0
        public int id;
        // bundle依赖 e.g. int[0] 
        // 通过 AssetBundleManifest.GetAllDependencies 获得
        public int[] deps;
        // bundle大小 e.g. 8878   
        // 通过 File.OpenRead 获得
        public long len;
        // bundle哈希 e.g. ffd861a78a7c80a119ac75907b7699b5   
        // 通过 AssetBundleManifest.GetAssetBundleHash 获得
        public string hash;
    }

    public class Manifest : ScriptableObject {
        
        public string[] activeVariants = new string[0];
        // 目录列表
        // e.g.
        // [Assets/Test/3StageSelect/Test1/Test11]
        public string[] dirs = new string[0];
        public AssetRef[] assetRefArray = new AssetRef[0];
        public BundleRef[] bundleRefArray = new BundleRef[0];
    }
}