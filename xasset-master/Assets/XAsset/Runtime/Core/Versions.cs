//
// Versions.cs
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
using UnityEngine;

namespace libx
{
	public enum VerifyBy
	{
		Size,
		Hash,
	}

	public static class Versions
	{
		public const string ResName = "res";
		public const string VerName = "ver";
		public static  readonly  VerifyBy verifyBy = VerifyBy.Hash;
		private static readonly VDisk _disk = new VDisk ();
        // 更新的 Vfile
		private static readonly Dictionary<string, VFile> _updateData = new Dictionary<string, VFile> ();
        // 当前的 VFile
		private static readonly Dictionary<string, VFile> _baseData = new Dictionary<string, VFile> ();

        public static AssetBundle LoadAssetBundleFromFile(string url) {
            if (!File.Exists(url)) {
                if (_disk != null) {
                    var name = Path.GetFileName(url);
                    var file = _disk.GetFile(name, string.Empty);
                    if (file != null) {
                        return AssetBundle.LoadFromFile(_disk.name, 0, (ulong)file.offset);
                    }
                }
            }
            return AssetBundle.LoadFromFile(url);
        }

		public static AssetBundleCreateRequest LoadAssetBundleFromFileAsync (string url)
		{
			if (!File.Exists (url)) {
				if (_disk != null) {
					var name = Path.GetFileName (url);
					var file = _disk.GetFile (name, string.Empty);
					if (file != null) {
						return AssetBundle.LoadFromFileAsync (_disk.name, 0, (ulong)file.offset);
					}
				}	
			} 
			return AssetBundle.LoadFromFileAsync (url);
		}

        // 生成 res, ver 文件
        public static void BuildVersions(string outputPath, string[] bundles, int version) {
            // e.g. DLC/Windows/ver
            var verPath = outputPath + "/" + VerName;
            if (File.Exists(verPath)) {
                File.Delete(verPath);
            }
            // e.g. DLC/Windows/res
            var resPath = outputPath + "/" + ResName;
            if (File.Exists(resPath)) {
                File.Delete(resPath);
            }

            VDisk disk = new VDisk();

            // 将 bundle 的信息 以 {VFile} 的形式 添加到 {VDisk}
            foreach (var fileName in bundles) {
                // e.g. DLC/Windows/assets/xasset/demo/scenes.unity3d
                using (FileStream fs = File.OpenRead(outputPath + "/" + fileName)) {
                    // 文件名， 文件长度, CRC
                    disk.AddFile(fileName, fs.Length, Utility.GetCRC32Hash(fs));
                }
            }

            // DLC/Windows/res
            disk.name = resPath;
            // 保存 res
            disk.Save();

            // 保存 ver   e.g. DLC/Windows/ver
            // 
            // ver的格式
            // 
            // version
            //
            // VFile 的数量
            //
            // VFile.name, VFile.len, VFile.crc (单独的 res)
            // VFile.name, VFile.len, VFile.crc
            // VFile.name, VFile.len, VFile.crc
            //
            // 读取 ver, 覆盖以前的 ver
            using (var stream = File.OpenWrite(verPath)) {
                var writer = new BinaryWriter(stream);
                // 写入版本号
                writer.Write(version);
                // 写入 VFile 数量, +1 表示还要写入 res 的信息
                writer.Write(disk.files.Count + 1);

                // 先 将 res 也作为 VFile 写入到 version 中, res 文件是第一个 VFile
                using (var fs = File.OpenRead(resPath)) {
                    var file = new VFile {
                        name = ResName,
                        len = fs.Length,
                        hash = Utility.GetCRC32Hash(fs)
                    };
                    file.Serialize(writer);
                }

                // 写入 VFile 的信息 到 version
                foreach (var file in disk.files) {
                    file.Serialize(writer);
                }
            }
        }

        // 加载 ver 文件中的版本信息
		public static int LoadVersion (string filename)
		{
			if (!File.Exists (filename))
				return -1;
			try
			{
				using (var stream = File.OpenRead (filename)) {
					var reader = new BinaryReader (stream);
					return reader.ReadInt32 ();
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				return -1;
			} 
		}

        // 通过 ver 读取 List<VFile> 并返回, 另外会将 VFile 读取到  _updateData || _baseData
        // 三种情况, 按执行顺序排列
        // S 目录版本号 大于 P 目录, 且执行 从 S 目录 拷贝到 P 目录的功能, 从P 目录 加载 VFile 到 Version._baseData
        // S 目录版本号 不大于 P目录,  从 P 目录加载 VFile 加载到 Version._baseData
        // 
		public static List<VFile> LoadVersions (string filename, bool update = false)
		{
            // 获取 ver 文件的目录
            var rootDir = Path.GetDirectoryName(filename);
            // 将 VFile 存到 _updateData || baseData
			var data = update ? _updateData : _baseData;
            // 清空旧数据,如果有的话
			data.Clear ();

			using (var stream = File.OpenRead (filename)) {
				var reader = new BinaryReader (stream);
				var list = new List<VFile> ();
                // 读取版本信息
				var ver = reader.ReadInt32 ();
				Debug.Log ("LoadVersions:" + ver);
                // 读取 VFile 数量
				var count = reader.ReadInt32 ();

				for (var i = 0; i < count; i++) {
                    // 读取 ver 里的 二进制信息, 反序列化为 VFIle
                    var version = new VFile ();
					version.Deserialize (reader);
					list.Add (version);

                    // 将 VFile 添加到 _updateData || baseData
                    data[version.name] = version;

                    // 获取 ver 文件所属的文件夹
                    var dir = string.Format("{0}/{1}", rootDir, Path.GetDirectoryName(version.name));

                    // 创建 ver 文件所属的文件夹
                    if (!Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
				} 
				return list;
			}
		}

		public static void UpdateDisk(string savePath, List<VFile> newFiles)
		{
			var saveFiles = new List<VFile> ();
			var files = _disk.files;
			foreach (var file in files) {
				if (_updateData.ContainsKey (file.name)) {
					saveFiles.Add (file);
				}
			}  
			_disk.Update(savePath, newFiles, saveFiles);
		}

        // 加载 res 文件
        public static bool LoadDisk(string filename) {
            return _disk.Load(filename);
        }

        // 根据 文件名，长度， CRC 判断 需不需要下载
        // path e.g. C:/Users/void87/AppData/LocalLow/xasset/xasset/DLC/assets/test/prefab2.unity3d
        public static bool IsNew (string path, long len, string hash)
		{
			VFile file;

            // 获取文件名, e.g. prefab2.unity3d
            var key = Path.GetFileName (path);

            // 在 Versions._baseData 里获取 VFile
            if (_baseData.TryGetValue(key, out file)) {
                // 文件名 为 res, 跳过
                // 文件名 相同 且 len 和 CRC 都相等, 也跳过
                if (key.Equals(ResName) || file.len == len && file.hash.Equals(hash, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            // disck 中有 VFile
            if (_disk.Exists()) {
                // 通过 path 获取 VFile
                var vdf = _disk.GetFile(path, hash);
                if (vdf != null && vdf.len == len && vdf.hash.Equals(hash, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            // 本地不存在旧文件
            if (!File.Exists(path)) {
                return true;
            }

            // 读取 path 对应的
			using (var stream = File.OpenRead (path)) {
                //  长度不一样, 是新文件
				if (stream.Length != len) {
					return true;
				} 

                // 没有启用 CRC 直接返回 false, 表示不是新文件
				if (verifyBy != VerifyBy.Hash)
					return false;

                // 判断 CRC 是否一样
				return !Utility.GetCRC32Hash (stream).Equals (hash, StringComparison.OrdinalIgnoreCase);
			}
		} 
	}
}