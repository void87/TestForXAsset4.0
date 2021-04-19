//
// Downloader.cs
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

namespace libx {
    // 下载器
    public class Downloader : MonoBehaviour {
        // byte 转换为 mb
        private const float BYTES_2_MB = 1f / (1024 * 1024);

        public int maxDownloads = 3;

        private readonly List<Download> _downloads = new List<Download>();
        // 每帧要下载的 List<Download>, Update 时传到 _processing 中
        private readonly List<Download> _tostart = new List<Download>();
        // 每帧正在下载的 List<Download>, Update 时调用 Download.Update
        private readonly List<Download> _progressing = new List<Download>();
        // Updater.OnUpdate
        public Action<long, long, float> onUpdate;
        // Updater.OnComplete
        public Action onFinished;

        private int _finishedIndex;
        // 当前正在下载中的所有下载文件中的 索引
        private int _downloadIndex;
        // 下载开始的时间, 网络变动或applicationfocus 会重置 e.g. 10秒
        private float _startTime;
        // 上一次采样时， 从游戏启动时经过的时间
        private float _lastTime;

        // 上一次采样时文件的下载进度
        private long _lastSize;

        // 总共需要下载的文件的大小
        public long size { get; private set; }

        // 当前的下载进度
        public long position { get; private set; }

        public float speed { get; private set; }

        public List<Download> downloads { get { return _downloads; } }

        private long GetDownloadSize() {
            var len = 0L;
            var downloadSize = 0L;
            foreach (var download in _downloads) {
                downloadSize += download.position;
                len += download.len;
            }
            return downloadSize - (len - size);
        }

        // 是否开始 下载
        private bool _started;

        // 将下载的信息反馈到 UI 上的时间间隔
        [SerializeField] private float sampleTime = 0.5f;

        // 开始下载
        public void StartDownload() {
            _tostart.Clear();
            _finishedIndex = 0;
            _lastSize = 0L;
            Restart();
        }

        public void Restart() {
            _startTime = Time.realtimeSinceStartup;

            _lastTime = 0;
            _started = true;
            // _downloadIndex 从  _finishedIndex 开始
            _downloadIndex = _finishedIndex;

            
            var max = Math.Min(_downloads.Count, maxDownloads);
            for (var i = _finishedIndex; i < max; i++) {
                var item = _downloads[i];
                // 添加 Download 到 _tostart
                _tostart.Add(item);
                // 当前下载Download 的索引
                _downloadIndex++;
            }
        }

        // 停止下载
        public void Stop() {
            _tostart.Clear();

            foreach (var download in _progressing) {
                download.Complete(true);
                _downloads[download.id] = download.Clone() as Download;

            }
            _progressing.Clear();
            _started = false;
        }

        public void Clear() {
            // 总大小清零
            size = 0;
            // 总下载进度清零
            position = 0;

            // 下载索引清零
            _downloadIndex = 0;
            // 完成索引清零
            _finishedIndex = 0;
            // 上一次采样时， 从游戏启动时经过的时间 清零
            _lastTime = 0f;
            // 上一次采样时文件的下载进度 清零
            _lastSize = 0L;
            // 下载开始的时间 清零
            _startTime = 0;
            // 开始标记 重置
            _started = false;

            // 强制停止 当前正在下载的 Download
            foreach (var item in _progressing) {
                item.Complete(true);
            }
            _progressing.Clear();

            // 所有的 Download 清零
            _downloads.Clear();
            // _tostart 清零
            _tostart.Clear();
        }

        // 将 VFile 的信息 转化为 Download 添加到 Downloader 中
        public void AddDownload(string url, string filename, string savePath, string hash, long len) {
            var download = new Download {
                id = _downloads.Count,
                url = url,
                name = filename,
                hash = hash,
                len = len,
                savePath = savePath,
                completed = OnFinished
            };
            _downloads.Add(download);

            // 获取 临时文件 信息
            var info = new FileInfo(download.tempPath);
            // 文件存在, 获取 已下载的文件的大小和 文件大小的 差值 加入到 总大小
            if (info.Exists) {
                size += len - info.Length;
            // 文件不存在, 将单个文件大小加入到 总大小
            } else {
                size += len;
            }
        }

        // 下载完成时的回调
        private void OnFinished(Download download) {

            // 还有文件没下载完
            if (_downloadIndex < _downloads.Count) {
                // 根据 _downloadIndex,  添加到 _tostart
                _tostart.Add(_downloads[_downloadIndex]);
                // 当前下载的索引自增
                _downloadIndex++;
            }
            // 已完成的索引自增
            _finishedIndex++;

            Debug.Log(string.Format("OnFinished:{0}, {1}", _finishedIndex, _downloads.Count));

            // 总体内容 没下载完 直接 return
            if (_finishedIndex != downloads.Count)
                return;

            //  总体 下载完了,  Updater.OnComplete
            if (onFinished != null) {
                onFinished.Invoke();
            }

            // 重置 _started
            _started = false;
        }

        // 获取 下载速度
        public static string GetDisplaySpeed(float downloadSpeed) {
            // mb/每秒
            if (downloadSpeed >= 1024 * 1024) {
                return string.Format("{0:f2}MB/s", downloadSpeed * BYTES_2_MB);
            }
            // kb/每秒
            if (downloadSpeed >= 1024) {
                return string.Format("{0:f2}KB/s", downloadSpeed / 1024);
            }
            // byte/每秒
            return string.Format("{0:f2}B/s", downloadSpeed);
        }

        // 显示 byte, kb, mb
        public static string GetDisplaySize(long downloadSize) {
            if (downloadSize >= 1024 * 1024) {
                return string.Format("{0:f2}MB", downloadSize * BYTES_2_MB);
            }
            if (downloadSize >= 1024) {
                return string.Format("{0:f2}KB", downloadSize / 1024);
            }
            return string.Format("{0:f2}B", downloadSize);
        }


        private void Update() {
            if (!_started)
                return;

            if (_tostart.Count > 0) {
                // 将 _tostart 里的 Download 转移到 _progressing 里
                for (var i = 0; i < Math.Min(maxDownloads, _tostart.Count); i++) {
                    var item = _tostart[i];
                    // Download 开始下载
                    item.Start();
                    // 从 _tostart 移除这个 Download
                    _tostart.RemoveAt(i);
                    // 将 Download 添加到 _progressing
                    _progressing.Add(item);
                    // i--,  i不会变， 因为 _tostart 会一直移除 Download
                    i--;
                }
            }

            // _processing 列表中的 Download 才是真正下载的 Download
            for (var index = 0; index < _progressing.Count; index++) {
                var download = _progressing[index];

                // 下载内容的处理不在这里,这里主要处理相关错误信息
                download.Update();

                if (!download.finished)
                    continue;
                // 从 _progressing 移除这个 Download
                _progressing.RemoveAt(index);
                // i--,  i不会变， 因为 _progressing 会一直移除 Download
                index--;
            }

            // 获取当前总的已经下载的文件的大小
            position = GetDownloadSize();

            // 从开始下载经过的时间, 网络变动或applicationfocus 会重置 e.g. 10秒
            var elapsed = Time.realtimeSinceStartup - _startTime;

            if (elapsed - _lastTime < sampleTime)
                return;


            // 两次 sample 之间的间隔时间. 基本等于 sampleTime e.g. 1秒
            var deltaTime = elapsed - _lastTime;

            // 下载速度
            speed = (position - _lastSize) / deltaTime;

            if (onUpdate != null) {
                // Updater.onUpdate->UpdateScreen.OnMessage, UpdateScreen.OnProgress
                onUpdate(position, size, speed);
            }
            // 重新记录  上一次采样时， 从游戏启动时经过的时间
            _lastTime = elapsed;
            // 重新记录 上一次采样时文件的下载进度
            _lastSize = position;
        }
    }
}