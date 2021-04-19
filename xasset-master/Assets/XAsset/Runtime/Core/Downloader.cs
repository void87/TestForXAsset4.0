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
    // ������
    public class Downloader : MonoBehaviour {
        // byte ת��Ϊ mb
        private const float BYTES_2_MB = 1f / (1024 * 1024);

        public int maxDownloads = 3;

        private readonly List<Download> _downloads = new List<Download>();
        // ÿ֡Ҫ���ص� List<Download>, Update ʱ���� _processing ��
        private readonly List<Download> _tostart = new List<Download>();
        // ÿ֡�������ص� List<Download>, Update ʱ���� Download.Update
        private readonly List<Download> _progressing = new List<Download>();
        // Updater.OnUpdate
        public Action<long, long, float> onUpdate;
        // Updater.OnComplete
        public Action onFinished;

        private int _finishedIndex;
        // ��ǰ���������е����������ļ��е� ����
        private int _downloadIndex;
        // ���ؿ�ʼ��ʱ��, ����䶯��applicationfocus ������ e.g. 10��
        private float _startTime;
        // ��һ�β���ʱ�� ����Ϸ����ʱ������ʱ��
        private float _lastTime;

        // ��һ�β���ʱ�ļ������ؽ���
        private long _lastSize;

        // �ܹ���Ҫ���ص��ļ��Ĵ�С
        public long size { get; private set; }

        // ��ǰ�����ؽ���
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

        // �Ƿ�ʼ ����
        private bool _started;

        // �����ص���Ϣ������ UI �ϵ�ʱ����
        [SerializeField] private float sampleTime = 0.5f;

        // ��ʼ����
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
            // _downloadIndex ��  _finishedIndex ��ʼ
            _downloadIndex = _finishedIndex;

            
            var max = Math.Min(_downloads.Count, maxDownloads);
            for (var i = _finishedIndex; i < max; i++) {
                var item = _downloads[i];
                // ��� Download �� _tostart
                _tostart.Add(item);
                // ��ǰ����Download ������
                _downloadIndex++;
            }
        }

        // ֹͣ����
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
            // �ܴ�С����
            size = 0;
            // �����ؽ�������
            position = 0;

            // ������������
            _downloadIndex = 0;
            // �����������
            _finishedIndex = 0;
            // ��һ�β���ʱ�� ����Ϸ����ʱ������ʱ�� ����
            _lastTime = 0f;
            // ��һ�β���ʱ�ļ������ؽ��� ����
            _lastSize = 0L;
            // ���ؿ�ʼ��ʱ�� ����
            _startTime = 0;
            // ��ʼ��� ����
            _started = false;

            // ǿ��ֹͣ ��ǰ�������ص� Download
            foreach (var item in _progressing) {
                item.Complete(true);
            }
            _progressing.Clear();

            // ���е� Download ����
            _downloads.Clear();
            // _tostart ����
            _tostart.Clear();
        }

        // �� VFile ����Ϣ ת��Ϊ Download ��ӵ� Downloader ��
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

            // ��ȡ ��ʱ�ļ� ��Ϣ
            var info = new FileInfo(download.tempPath);
            // �ļ�����, ��ȡ �����ص��ļ��Ĵ�С�� �ļ���С�� ��ֵ ���뵽 �ܴ�С
            if (info.Exists) {
                size += len - info.Length;
            // �ļ�������, �������ļ���С���뵽 �ܴ�С
            } else {
                size += len;
            }
        }

        // �������ʱ�Ļص�
        private void OnFinished(Download download) {

            // �����ļ�û������
            if (_downloadIndex < _downloads.Count) {
                // ���� _downloadIndex,  ��ӵ� _tostart
                _tostart.Add(_downloads[_downloadIndex]);
                // ��ǰ���ص���������
                _downloadIndex++;
            }
            // ����ɵ���������
            _finishedIndex++;

            Debug.Log(string.Format("OnFinished:{0}, {1}", _finishedIndex, _downloads.Count));

            // �������� û������ ֱ�� return
            if (_finishedIndex != downloads.Count)
                return;

            //  ���� ��������,  Updater.OnComplete
            if (onFinished != null) {
                onFinished.Invoke();
            }

            // ���� _started
            _started = false;
        }

        // ��ȡ �����ٶ�
        public static string GetDisplaySpeed(float downloadSpeed) {
            // mb/ÿ��
            if (downloadSpeed >= 1024 * 1024) {
                return string.Format("{0:f2}MB/s", downloadSpeed * BYTES_2_MB);
            }
            // kb/ÿ��
            if (downloadSpeed >= 1024) {
                return string.Format("{0:f2}KB/s", downloadSpeed / 1024);
            }
            // byte/ÿ��
            return string.Format("{0:f2}B/s", downloadSpeed);
        }

        // ��ʾ byte, kb, mb
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
                // �� _tostart ��� Download ת�Ƶ� _progressing ��
                for (var i = 0; i < Math.Min(maxDownloads, _tostart.Count); i++) {
                    var item = _tostart[i];
                    // Download ��ʼ����
                    item.Start();
                    // �� _tostart �Ƴ���� Download
                    _tostart.RemoveAt(i);
                    // �� Download ��ӵ� _progressing
                    _progressing.Add(item);
                    // i--,  i����䣬 ��Ϊ _tostart ��һֱ�Ƴ� Download
                    i--;
                }
            }

            // _processing �б��е� Download �����������ص� Download
            for (var index = 0; index < _progressing.Count; index++) {
                var download = _progressing[index];

                // �������ݵĴ���������,������Ҫ������ش�����Ϣ
                download.Update();

                if (!download.finished)
                    continue;
                // �� _progressing �Ƴ���� Download
                _progressing.RemoveAt(index);
                // i--,  i����䣬 ��Ϊ _progressing ��һֱ�Ƴ� Download
                index--;
            }

            // ��ȡ��ǰ�ܵ��Ѿ����ص��ļ��Ĵ�С
            position = GetDownloadSize();

            // �ӿ�ʼ���ؾ�����ʱ��, ����䶯��applicationfocus ������ e.g. 10��
            var elapsed = Time.realtimeSinceStartup - _startTime;

            if (elapsed - _lastTime < sampleTime)
                return;


            // ���� sample ֮��ļ��ʱ��. �������� sampleTime e.g. 1��
            var deltaTime = elapsed - _lastTime;

            // �����ٶ�
            speed = (position - _lastSize) / deltaTime;

            if (onUpdate != null) {
                // Updater.onUpdate->UpdateScreen.OnMessage, UpdateScreen.OnProgress
                onUpdate(position, size, speed);
            }
            // ���¼�¼  ��һ�β���ʱ�� ����Ϸ����ʱ������ʱ��
            _lastTime = elapsed;
            // ���¼�¼ ��һ�β���ʱ�ļ������ؽ���
            _lastSize = position;
        }
    }
}