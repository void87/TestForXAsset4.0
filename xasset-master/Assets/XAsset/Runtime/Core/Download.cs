//
// Download.cs
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
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace libx {
    // 抽象的下载
    // Download: DownloadHandlerScript: DownloadHandler
    public class Download : DownloadHandlerScript, IDisposable, ICloneable {
        #region ICloneable implementation

        public object Clone() {
            return new Download() {
                id = id,
                hash = hash,
                url = url,
                len = len,
                savePath = savePath,
                completed = completed,
                name = name
            };
        }

        #endregion

        // 下载文件的索引
        public int id { get; set; }

        // 下载时会出现的错误
        public string error { get; private set; }

        // 下载文件的长度
        public long len { get; set; }

        // 下载文件的crc
        public string hash { get; set; }

        // 下载文件的 url
        public string url { get; set; }

        // 当前的下载进度
        public long position { get; private set; }

        // 下载文件的名称 e.g. assets/xasset/demo/scenes/game.unity.unity3d
        public string name { get; set; }

        // e.g. C:\\Users\\void8\\AppData\\LocalLow\\xasset\\xasset\\DLC\\assets\\xasset\\demo\\scenes/eb6aa3c5
        // 临时路径， 文件名为这个 VFile 的  crc
        public string tempPath {
            get {
                var dir = Path.GetDirectoryName(savePath);
                return string.Format("{0}/{1}", dir, hash);
            }
        }

        // e.g. C:/Users/void8/AppData/LocalLow/xasset/xasset/DLC/assets/xasset/demo/scenes/game.unity.unity3d
        public string savePath;

        // 下载完成时的回调, 也就是 Downloader.OnFinished
        public Action<Download> completed { get; set; }

        private UnityWebRequest _request;
        // 文件 Stream
        private FileStream _stream;
        // 是否在运行中
        private bool _running;
        // 是否下载完成
        private bool _finished = false;

        // 获取下载的进度
        protected override float GetProgress() {
            return position * 1f / len;
        }

        protected override byte[] GetData() {
            return null;
        }

        protected override void ReceiveContentLength(int contentLength) {

        }

        // 下载内容时的回调
        protected override bool ReceiveData(byte[] buffer, int dataLength) {
            if (!string.IsNullOrEmpty(_request.error)) {
                error = _request.error;
                Complete();
                return true;
            }

            // 将下载的 byte[] 写入到 FileSteam
            _stream.Write(buffer, 0, dataLength);
            // 重新设置 position 
            position += dataLength;

            return _running;
        }

        // 下载完成时的回调
        protected override void CompleteContent() {
            Complete();
        }

        public override string ToString() {
            return string.Format("{0}, size:{1}, hash:{2}", url, len, hash);
        }

        // 开始下载
        public void Start() {
            if (_running) {
                return;
            }

            error = null;
            finished = false;
            _running = true;

            _stream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.Write);
            // 为下载 position = 0
            // 没下载完 position 为当前已经下载的长度
            // 下载完了 position = len
            position = _stream.Length;

            if (position < len) {
                // 定位到 Begin
                _stream.Seek(position, SeekOrigin.Begin);
                
                _request = UnityWebRequest.Get(url);
                // 从 position 为止 开始下载
                _request.SetRequestHeader("Range", "bytes=" + position + "-");
                // 设置 DownloadHandler 为 自身
                _request.downloadHandler = this;
                // 请求要下载的文件
                _request.SendWebRequest();
                Debug.Log("Start Download：" + url);
            } else {
                Complete();
            }
        }

        // 下载内容的处理不在这里,这里主要处理相关错误信息
        public void Update() {
            if (_running) {
                if (_request.isDone && _request.downloadedBytes < (ulong)len) {
                    error = "unknown error: downloadedBytes < len";
                }
                if (!string.IsNullOrEmpty(_request.error)) {
                    error = _request.error;
                }
            }
        }

        public new void Dispose() {
            // 释放  FileStream
            if (_stream != null) {
                _stream.Close();
                _stream.Dispose();
                _stream = null;
            }
            
            // 释放 UnityWebRequest
            if (_request != null) {
                _request.Abort();
                _request.Dispose();
                _request = null;
            }
            base.Dispose();

            _running = false;
            finished = true;
        }

        // 下载完成, 强制停止
        public void Complete(bool stop = false) {
            // 下载完成后先释放
            Dispose();
            if (stop) {
                return;
            }
            // 然后再 检查 错误
            CheckError();
        }

        // 检查错误
        private void CheckError() {
            if (File.Exists(tempPath)) {
                // 前面的步骤没有错误信息
                if (string.IsNullOrEmpty(error)) {
                    // 读取临时文件
                    using (var fs = File.OpenRead(tempPath)) {
                        // 长度不一致
                        if (fs.Length != len) {
                            error = "下载文件长度异常:" + fs.Length;
                        }

                        // crc 校验错误
                        if (Versions.verifyBy == VerifyBy.Hash) {
                            const StringComparison compare = StringComparison.OrdinalIgnoreCase;
                            if (!hash.Equals(Utility.GetCRC32Hash(fs), compare)) {
                                error = "下载文件哈希异常:" + hash;
                            }
                        }
                    }
                }

                //  前面的步骤没有错误
                if (string.IsNullOrEmpty(error)) {
                    // 将临时文件 改为 正式文件
                    File.Copy(tempPath, savePath, true);
                    // 删除临时文件
                    File.Delete(tempPath);
                    Debug.Log("Complete Download：" + url);
                    if (completed == null)
                        return;
                    completed.Invoke(this);
                    completed = null;
                // 前面的步骤有错误, 直接删除 临时文件
                } else {
                    File.Delete(tempPath);
                }
            // 文件不存在
            } else {
                error = "文件不存在";
            }
        }

        // 重新下载
        public void Retry() {
            Dispose();
            Start();
        }

        // 是否下载完成
        public bool finished {
            get { return _finished; }
            private set { _finished = value; }
        }
    }

}