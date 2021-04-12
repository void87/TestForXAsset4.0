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
		public const string Dataname = "res";
		public const string Filename = "ver";
		public static  readonly  VerifyBy verifyBy = VerifyBy.Hash;
		private static readonly VDisk _disk = new VDisk ();
		private static readonly Dictionary<string, VFile> _updateData = new Dictionary<string, VFile> ();
		private static readonly Dictionary<string, VFile> _baseData = new Dictionary<string, VFile> ();

		public static AssetBundle LoadAssetBundleFromFile (string url)
		{
			if (!File.Exists (url)) {
				if (_disk != null) {
					var name = Path.GetFileName (url);
					var file = _disk.GetFile (name, string.Empty);
					if (file != null) {
						return AssetBundle.LoadFromFile (_disk.name, 0, (ulong)file.offset);
					}
				}	
			}   
			return AssetBundle.LoadFromFile (url);
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

		public static void BuildVersions (string outputPath, string[] bundles, int version)
		{
            // e.g. DLC/Windows/ver
            var path = outputPath + "/" + Filename;
			if (File.Exists (path)) {
				File.Delete (path);
			}
            // e.g. DLC/Windows/res
            var dataPath = outputPath + "/" + Dataname;
			if (File.Exists (dataPath)) {
				File.Delete (dataPath);
			}  

			var disk = new VDisk (); 

            // �� bundle ����Ϣ �� {VFile} ����ʽ ��ӵ� {VDisk}
			foreach (var file in bundles) {
                // e.g. DLC/Windows/assets/xasset/demo/scenes.unity3d
                using (var fs = File.OpenRead (outputPath + "/" + file)) {
					disk.AddFile (file, fs.Length, Utility.GetCRC32Hash (fs));
				}
			}

            // DLC/Windows/res
            disk.name = dataPath;
			disk.Save ();

            // ����汾��Ϣ
            // e.g.
            // 
            // 66
            // 3
            // {res, 128976, 765321}
            // {assets/xasset/demo/scenes.unity3d, 126793, 75ac4c10}
            // {manifest.unity3d, 2094, dc79a9c9}
            using (var stream = File.OpenWrite (path)) {
				var writer = new BinaryWriter (stream);
                // д��汾��
				writer.Write (version);
                // д���ļ�����, +1 ��ʾ��Ҫд�� res ����Ϣ
				writer.Write (disk.files.Count + 1);
                // �� res ����Ϣ Ҳд�뵽 version ��
				using (var fs = File.OpenRead (dataPath)) {
					var file = new VFile { name = Dataname, len = fs.Length, hash = Utility.GetCRC32Hash (fs) };
					file.Serialize (writer);
				} 
                // д�� assetbundle ���ļ���Ϣ
				foreach (var file in disk.files) {
					file.Serialize (writer);
				}
			}
		}

        // ���� ver �ļ��еİ汾��Ϣ
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

		public static List<VFile> LoadVersions (string filename, bool update = false)
		{
            var rootDir = Path.GetDirectoryName(filename);
			var data = update ? _updateData : _baseData;
			data.Clear ();
			using (var stream = File.OpenRead (filename)) {
				var reader = new BinaryReader (stream);
				var list = new List<VFile> ();
                // ��ȡ�汾��Ϣ
				var ver = reader.ReadInt32 ();
				Debug.Log ("LoadVersions:" + ver);
                // ��ȡbundle����
				var count = reader.ReadInt32 ();
				for (var i = 0; i < count; i++) {
					var version = new VFile ();
					version.Deserialize (reader);
					list.Add (version);
					data [version.name] = version;
                    var dir = string.Format("{0}/{1}", rootDir, Path.GetDirectoryName(version.name));

                    // �������� bundle ·�� �����ļ���
                    if (! Directory.Exists(dir))
                    {
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

		public static bool LoadDisk (string filename)
		{
			return _disk.Load (filename);
		}

		public static bool IsNew (string path, long len, string hash)
		{
			VFile file;

			var key = Path.GetFileName (path);

            // res �Ƚ�
			if (_baseData.TryGetValue (key, out file)) {
				if (key.Equals (Dataname) ||
				    file.len == len && file.hash.Equals (hash, StringComparison.OrdinalIgnoreCase)) {
					return false;
				}
			}

            // ��ȡͬ�����ļ����бȽ�
			if (_disk.Exists ()) {
				var vdf = _disk.GetFile (path, hash);
				if (vdf != null && vdf.len == len && vdf.hash.Equals (hash, StringComparison.OrdinalIgnoreCase)) {
					return false;
				}
			}

            // ���ز����ھ��ļ�
			if (!File.Exists (path)) {
				return true;
			}

			using (var stream = File.OpenRead (path)) {
				if (stream.Length != len) {
					return true;
				} 
				if (verifyBy != VerifyBy.Hash)
					return false;
				return !Utility.GetCRC32Hash (stream).Equals (hash, StringComparison.OrdinalIgnoreCase);
			}
		} 
	}
}