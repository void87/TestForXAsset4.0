//
// VDisk.cs
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
using System.Net;
using UnityEngine;

namespace libx
{
	public class VFile
	{
        // e.g. 75ac4c10
        public string hash { get; set; }
        
		public long id { get; set; }

        // 文件大小 e.g. 126793
        public long len { get; set; }

        // 文件名也就是 bundle 名 e.g. assets/xasset/demo/scenes.unity3d
        public string name { get; set; }

		public long offset { get; set; }

		public VFile ()
		{
			offset = -1;
		}

		public void Serialize (BinaryWriter writer)
		{
			writer.Write (name);    // 写入 名字 e.g. assets/test/prefab1.unity3d
            writer.Write (len); // 写入 长度 
			writer.Write (hash);    // 写入 crc
		}

		public void Deserialize (BinaryReader reader)
		{
			name = reader.ReadString ();
			len = reader.ReadInt64 ();
			hash = reader.ReadString ();
        }
	}

    // 虚拟硬盘
	public class VDisk
	{
		private readonly byte[] _buffers = new byte[1024 * 4];
        // [bundle,{VFile}] e.g. [assets/xasset/demo/scenes.unity3d, {name: assets/xasset/demo/scenes.unity3d, len: 126794, hash: c1f7fbc8 }]
        private readonly Dictionary<string, VFile> _data = new Dictionary<string, VFile> ();
		private readonly List<VFile> _files = new List<VFile>();
		public  List<VFile> files { get { return _files; }}
		public string name { get; set; } 
		private long _pos;
		private long _len;

		public VDisk ()
		{
		}

		public bool Exists ()
		{
			return files.Count > 0;
		}

        // 添加 _data, files
        private void AddFile(VFile file) {
            _data[file.name] = file;
            files.Add(file);
        }

		public void AddFile (string path, long len, string hash)
		{ 
			var file = new VFile{ name = path, len = len, hash = hash };
			AddFile (file);
		}

        // 写入所有的 VFile 内容
		private void WriteFile (string path, BinaryWriter writer)
		{
            // 读取对应的bundle
			using (var fs = File.OpenRead (path)) {
				var len = fs.Length;
				WriteStream (len, fs, writer);
			}
		}

        // 写入单个 VFile 内容
		private void WriteStream (long len, Stream stream, BinaryWriter writer)
		{
			var count = 0L;
			while (count < len) {
				var read = (int)Math.Min (len - count, _buffers.Length);
                // 从  bundle 里 读取 内容 到 _buffers
				stream.Read (_buffers, 0, read);
                // 将 _buffers 里的内容 写入到 res
				writer.Write (_buffers, 0, read);
				count += read;
			}
		}

		public bool Load (string path)
		{
			if (!File.Exists (path))
				return false;

			Clear ();

			name = path;
			using (var reader = new BinaryReader (File.OpenRead (path))) {
				var count = reader.ReadInt32 ();
				for (var i = 0; i < count; i++) {
                    // 读取的时候给 VFile 设置 id
					var file = new VFile { id = i };
					file.Deserialize (reader);
					AddFile (file); 
				} 
				_pos = reader.BaseStream.Position;  
			}
			Reindex ();
			return true;
		}

		public void Reindex ()
		{
			_len = 0L;
			for (var i = 0; i < files.Count; i++) {
				var file = files [i];
				file.offset = _pos + _len;
				_len += file.len;
			}
		} 

		public VFile GetFile (string path, string hash)
		{
			var key = Path.GetFileName (path);
			VFile file;
			_data.TryGetValue (key, out file);
			return file;
		}

		public void Update(string dataPath, List<VFile> newFiles, List<VFile> saveFiles)
		{
			var dir = Path.GetDirectoryName(dataPath); 
			using (var stream = File.OpenRead(dataPath))
			{
				foreach (var item in saveFiles)
				{
					var path = string.Format("{0}/{1}", dir, item.name);
					if (File.Exists(path)) { continue; }  
					stream.Seek(item.offset, SeekOrigin.Begin); 
					using (var fs = File.OpenWrite(path))
					{
						var count = 0L;
						var len = item.len;
						while (count < len)
						{
							var read = (int) Math.Min(len - count, _buffers.Length);
							stream.Read(_buffers, 0, read);
							fs.Write(_buffers, 0, read);
							count += read;
						}
					}    
					newFiles.Add(item);
				}
			}

			if (File.Exists(dataPath))
			{
				File.Delete(dataPath);
			}
			
			using (var stream = File.OpenWrite (dataPath)) {
				var writer = new BinaryWriter (stream);
				writer.Write (newFiles.Count);
				foreach (var item in newFiles) {
					item.Serialize (writer);
				}  
				foreach (var item in newFiles) {
					var path = string.Format("{0}/{1}", dir, item.name);
					WriteFile (path, writer);
					File.Delete (path);
					Debug.Log ("Delete:" + path);
				} 
			} 
		}

        // res 格式
        // 
        // VFile.Count
        //
        // VFile.name, VFile.len, VFile.crc
        // VFile.name, VFile.len, VFile.crc
        // 
        // {VFile(binary)}
        // {VFile(binary)}
        public void Save ()
		{
            // 获取 ab包 所在的 目录名
			var dir = Path.GetDirectoryName (name);   
            // 建立一个 res 文件，准备开始写入,  覆盖以前的 res
			using (var stream = File.OpenWrite (name)) {
				var writer = new BinaryWriter (stream);
                // 向 res 写入 VFile 数量
				writer.Write (files.Count);

                // 每个 VFile 依次写入 res, 包括  name, len, crc
				foreach (var item in files) {
					item.Serialize (writer);
				}  

                // 将每个 VFile 对应的bundle 内容 写入到 res
				foreach (var item in files) {
					var path = dir + "/" + item.name;
					WriteFile (path, writer);
				}
			} 
		}

		public void Clear ()
		{
			_data.Clear ();
			files.Clear ();
		}
	}
}