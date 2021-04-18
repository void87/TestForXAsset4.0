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
        // ���µ� Vfile
		private static readonly Dictionary<string, VFile> _updateData = new Dictionary<string, VFile> ();
        // ��ǰ�� VFile
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

        // ���� res, ver �ļ�
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

            // �� bundle ����Ϣ �� {VFile} ����ʽ ��ӵ� {VDisk}
            foreach (var fileName in bundles) {
                // e.g. DLC/Windows/assets/xasset/demo/scenes.unity3d
                using (FileStream fs = File.OpenRead(outputPath + "/" + fileName)) {
                    // �ļ����� �ļ�����, CRC
                    disk.AddFile(fileName, fs.Length, Utility.GetCRC32Hash(fs));
                }
            }

            // DLC/Windows/res
            disk.name = resPath;
            // ���� res
            disk.Save();

            // ���� ver   e.g. DLC/Windows/ver
            // 
            // ver�ĸ�ʽ
            // 
            // version
            //
            // VFile ������
            //
            // VFile.name, VFile.len, VFile.crc (������ res)
            // VFile.name, VFile.len, VFile.crc
            // VFile.name, VFile.len, VFile.crc
            //
            // ��ȡ ver, ������ǰ�� ver
            using (var stream = File.OpenWrite(verPath)) {
                var writer = new BinaryWriter(stream);
                // д��汾��
                writer.Write(version);
                // д�� VFile ����, +1 ��ʾ��Ҫд�� res ����Ϣ
                writer.Write(disk.files.Count + 1);

                // �� �� res Ҳ��Ϊ VFile д�뵽 version ��, res �ļ��ǵ�һ�� VFile
                using (var fs = File.OpenRead(resPath)) {
                    var file = new VFile {
                        name = ResName,
                        len = fs.Length,
                        hash = Utility.GetCRC32Hash(fs)
                    };
                    file.Serialize(writer);
                }

                // д�� VFile ����Ϣ �� version
                foreach (var file in disk.files) {
                    file.Serialize(writer);
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

        // ͨ�� ver ��ȡ List<VFile> ������, ����Ὣ VFile ��ȡ��  _updateData || _baseData
        // �������, ��ִ��˳������
        // S Ŀ¼�汾�� ���� P Ŀ¼, ��ִ�� �� S Ŀ¼ ������ P Ŀ¼�Ĺ���, ��P Ŀ¼ ���� VFile �� Version._baseData
        // S Ŀ¼�汾�� ������ PĿ¼,  �� P Ŀ¼���� VFile ���ص� Version._baseData
        // 
		public static List<VFile> LoadVersions (string filename, bool update = false)
		{
            // ��ȡ ver �ļ���Ŀ¼
            var rootDir = Path.GetDirectoryName(filename);
            // �� VFile �浽 _updateData || baseData
			var data = update ? _updateData : _baseData;
            // ��վ�����,����еĻ�
			data.Clear ();

			using (var stream = File.OpenRead (filename)) {
				var reader = new BinaryReader (stream);
				var list = new List<VFile> ();
                // ��ȡ�汾��Ϣ
				var ver = reader.ReadInt32 ();
				Debug.Log ("LoadVersions:" + ver);
                // ��ȡ VFile ����
				var count = reader.ReadInt32 ();

				for (var i = 0; i < count; i++) {
                    // ��ȡ ver ��� ��������Ϣ, �����л�Ϊ VFIle
                    var version = new VFile ();
					version.Deserialize (reader);
					list.Add (version);

                    // �� VFile ��ӵ� _updateData || baseData
                    data[version.name] = version;

                    // ��ȡ ver �ļ��������ļ���
                    var dir = string.Format("{0}/{1}", rootDir, Path.GetDirectoryName(version.name));

                    // ���� ver �ļ��������ļ���
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

        // ���� res �ļ�
        public static bool LoadDisk(string filename) {
            return _disk.Load(filename);
        }

        // ���� �ļ��������ȣ� CRC �ж� �費��Ҫ����
        // path e.g. C:/Users/void87/AppData/LocalLow/xasset/xasset/DLC/assets/test/prefab2.unity3d
        public static bool IsNew (string path, long len, string hash)
		{
			VFile file;

            // ��ȡ�ļ���, e.g. prefab2.unity3d
            var key = Path.GetFileName (path);

            // �� Versions._baseData ���ȡ VFile
            if (_baseData.TryGetValue(key, out file)) {
                // �ļ��� Ϊ res, ����
                // �ļ��� ��ͬ �� len �� CRC �����, Ҳ����
                if (key.Equals(ResName) || file.len == len && file.hash.Equals(hash, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            // disck ���� VFile
            if (_disk.Exists()) {
                // ͨ�� path ��ȡ VFile
                var vdf = _disk.GetFile(path, hash);
                if (vdf != null && vdf.len == len && vdf.hash.Equals(hash, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            // ���ز����ھ��ļ�
            if (!File.Exists(path)) {
                return true;
            }

            // ��ȡ path ��Ӧ��
			using (var stream = File.OpenRead (path)) {
                //  ���Ȳ�һ��, �����ļ�
				if (stream.Length != len) {
					return true;
				} 

                // û������ CRC ֱ�ӷ��� false, ��ʾ�������ļ�
				if (verifyBy != VerifyBy.Hash)
					return false;

                // �ж� CRC �Ƿ�һ��
				return !Utility.GetCRC32Hash (stream).Equals (hash, StringComparison.OrdinalIgnoreCase);
			}
		} 
	}
}