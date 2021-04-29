//
// Updater.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace libx {

    // Updater, UpdateScreen
    public interface IUpdater {
        void OnStart(); // 开始下载
        void OnMessage(string msg); // 显示下载信息 
        void OnProgress(float progress);    // 显示下载进度
        void OnVersion(string ver); // 显示版本
        void OnClear(); // 清除下载内容
    }

    [RequireComponent(typeof(Downloader))]
    [RequireComponent(typeof(NetworkMonitor))]
    public class Updater : MonoBehaviour, IUpdater, INetworkMonitorListener {
        enum Step {
            Wait,   // 初始状态
            Copy,   // 是否 从 S 目录拷贝到 P 目录
            Coping, // 从 P 目录 拷贝到 S目录
            Versions, // 下载 ver, 获取需要下载的文件信息
            Prepared, // 将 VFile 转换为 Download
            Download,   // 进入下载阶段
        }

        private Step _step;

        [SerializeField] private string baseURL = "http://127.0.0.1:7888/DLC/";
        // 启动场景
        [SerializeField] private string gameScene = "Game.unity";
        // 是否开启 VirtualFileSystem
        [SerializeField] private bool enableVFS = true;
        [SerializeField] private bool development;

        // UpdateScreen
        public IUpdater listener { get; set; }
        // 下载器
        private Downloader _downloader;

        private NetworkMonitor _monitor;
        private string _platform;
        // ab包文件读取目录 e.g. C:/Users/void8/AppData/LocalLow/xasset/xasset/DLC/
        private string _savePath;
        private List<VFile> _versions = new List<VFile>();

        // UpdaterScreen->OnMessage
        public void OnMessage(string msg) {
            if (listener != null) {
                listener.OnMessage(msg);
            }
        }

        // UpdaterScreen->OnProgress
        public void OnProgress(float progress) {
            if (listener != null) {
                listener.OnProgress(progress);
            }
        }

        // UpdaterScreen->OnVersion
        public void OnVersion(string ver) {
            if (listener != null) {
                listener.OnVersion(ver);
            }
        }

        private void Start() {
            // 设置 Downloader 回调
            _downloader = gameObject.GetComponent<Downloader>();
            _downloader.onUpdate = OnUpdate;
            _downloader.onFinished = OnComplete;

            // 设置 NetworkMonitor.listener = Updater
            _monitor = gameObject.GetComponent<NetworkMonitor>();
            _monitor.listener = this;

            // ab包保存目录
            _savePath = string.Format("{0}/DLC/", Application.persistentDataPath);

            _platform = GetPlatformForAssetBundles(Application.platform);

            _step = Step.Wait;

            // updatePath = p 目录
            Assets.updatePath = _savePath;
        }

        private void OnApplicationFocus(bool hasFocus) {
            if (_reachabilityChanged || _step == Step.Wait) {
                return;
            }

            if (hasFocus) {
                MessageBox.CloseAll();
                if (_step == Step.Download) {
                    _downloader.Restart();
                } else {
                    StartUpdate();
                }
            } else {
                if (_step == Step.Download) {
                    _downloader.Stop();
                }
            }
        }

        private bool _reachabilityChanged;

        // NetworkMonitor->OnReachablityChanged
        public void OnReachablityChanged(NetworkReachability reachability) {
            if (_step == Step.Wait) {
                return;
            }

            _reachabilityChanged = true;

            if (_step == Step.Download) {
                _downloader.Stop();
            }

            if (reachability == NetworkReachability.NotReachable) {
                MessageBox.Show("提示！", "找不到网络，请确保手机已经联网", "确定", "退出").onComplete += delegate (MessageBox.EventId id) {
                    if (id == MessageBox.EventId.Ok) {
                        if (_step == Step.Download) {
                            _downloader.Restart();
                        } else {
                            StartUpdate();
                        }
                        _reachabilityChanged = false;
                    } else {
                        Quit();
                    }
                };
            } else {
                if (_step == Step.Download) {
                    _downloader.Restart();
                } else {
                    StartUpdate();
                }
                _reachabilityChanged = false;
                MessageBox.CloseAll();
            }
        }

        private void OnUpdate(long progress, long size, float speed) {
            OnMessage(string.Format("下载中...{0}/{1}, 速度：{2}",
                Downloader.GetDisplaySize(progress),
                Downloader.GetDisplaySize(size),
                Downloader.GetDisplaySpeed(speed)));

            OnProgress(progress * 1f / size);
        }

        public void Clear() {
            MessageBox.Show("提示", "清除数据后所有数据需要重新下载，请确认！", "清除").onComplete += id => {
                if (id != MessageBox.EventId.Ok)
                    return;
                OnClear();
            };
        }

        // UpdateScreen 中的 清理按钮 调用这个方法
        public void OnClear() {
            OnMessage("数据清除完毕");
            OnProgress(0);
            // 清理 List<VFile>
            _versions.Clear();
            // Downloader 清理
            _downloader.Clear();
            // Step 重置到 Step.Wait
            _step = Step.Wait;
            // 
            _reachabilityChanged = false;

            // Assets 清理
            Assets.Clear();

            // UpdateScreen.OnClear
            if (listener != null) {
                listener.OnClear();
            }

            // 删除 P 目录下的  保存路径
            if (Directory.Exists(_savePath)) {
                Directory.Delete(_savePath, true);
            }
        }

        public void OnStart() {
            if (listener != null) {
                listener.OnStart();
            }
        }

        private IEnumerator _checking;

        // 默认由 UpdaterScreen 的 START按钮 点击触发
        // 开始走 Step 流程， 走到 最后就是下载
        public void StartUpdate() {
#if UNITY_EDITOR
            if (development) {
                Assets.runtimeMode = false;
                StartCoroutine(LoadGameScene());
                return;
            }
#endif
            // UpdaterScreen.OnStart
            OnStart();

            if (_checking != null) {
                StopCoroutine(_checking);
            }

            _checking = Checking();

            StartCoroutine(_checking);
        }

        // 将 VFile 的信息 转化为 Download 添加到 Downloader 中
        private void AddDownload(VFile item) {
            _downloader.AddDownload(
                GetDownloadURL(item.name), 
                item.name, 
                _savePath + item.name, 
                item.hash, 
                item.len);
        }

        // 准备下载
        private void PrepareDownloads() {
            // 开启VFS
            if (enableVFS) {
                // 读取 P 目录下的  res 文件
                var path = string.Format("{0}{1}", _savePath, Versions.ResName);
                //  P 目录下不存在 res 文件
                if (!File.Exists(path)) {
                    // 开启 VFS, 只下载 文件[0] 也就是 res, 直接返回
                    AddDownload(_versions[0]);
                    return;
                }

                // P 目录下 存在 res 文件, 直接加载
                Versions.LoadDisk(path);
            }

            // 不开启 VFS, 从 文件[1] 开始下载, 
            for (var i = 1; i < _versions.Count; i++) {
                var item = _versions[i];
                // 根据 文件名，长度， CRC 判断 需不需要下载
                if (Versions.IsNew(string.Format("{0}{1}", _savePath, item.name), item.len, item.hash)) {
                    // 将新文件添加到  Downloader._downloads
                    AddDownload(item);
                }
            }
        }

        // 是否开启VFS
        private IEnumerator RequestVFS() {
            var mb = MessageBox.Show("提示", "是否开启VFS？开启有助于提升IO性能和数据安全。", "开启");
            yield return mb;
            enableVFS = mb.isOk;
        }

        // 获取平台目录
        private static string GetPlatformForAssetBundles(RuntimePlatform target) {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (target) {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "Windows";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return "iOS"; // OSX
                default:
                    return null;
            }
        }

        // 获取 下载的 URL
        private string GetDownloadURL(string filename) {
            // e.g. http://192.168.5.121/DLC/Windows/assets/xasset/demo/ui/1loadingpage.unity3d
            return string.Format("{0}{1}/{2}", baseURL, _platform, filename);
        }

        // 不同 Step 阶段, 处理不同内容
        private IEnumerator Checking() {
            // 创建 ab 包的 保存目录, p目录
            if (!Directory.Exists(_savePath)) {
                Directory.CreateDirectory(_savePath);
            }

            // 通过点击 MessageBox 获取是否开启 VFS
            if (_step == Step.Wait) {
                yield return RequestVFS();
                _step = Step.Copy;
            }

            // 是否将 S 目录下的文件 拷贝到 P目录
            // 拷贝进入 Step.Coping
            // 跳过进入 Step.Versions
            if (_step == Step.Copy) {
                yield return RequestCopy();
            }

            // 从 S 目录拷贝到 P 目录阶段
            if (_step == Step.Coping) {
                // 获取 ver.tmp 中的 VFile, 也就是从 S 目录中拷贝出来的 ver, 放到 Version._baseData
                var path = _savePath + Versions.VerName + ".tmp";
                var versions = Versions.LoadVersions(path);
                // 获取 S 目录的路径
                var basePath = GetStreamingAssetsPath() + "/";
                // 从 S 目录 拷贝到 P 目录
                yield return UpdateCopy(versions, basePath);

                _step = Step.Versions;
            }

            // 下载 ver, 获取需要下载的文件信息
            if (_step == Step.Versions) {
                yield return RequestVersions();
            }

            // 下载阶段
            if (_step == Step.Prepared) {
                // UpdateScreen->OnMessage
                OnMessage("正在检查版本信息...");
                // 
                var totalSize = _downloader.size;
                if (totalSize > 0) {
                    var tips = string.Format("发现内容更新，总计需要下载 {0} 内容", Downloader.GetDisplaySize(totalSize));
                    var mb = MessageBox.Show("提示", tips, "下载", "退出");
                    yield return mb;
                    if (mb.isOk) {
                        // 开始下载
                        _downloader.StartDownload();
                        _step = Step.Download;
                    // 退出游戏
                    } else {
                        Quit();
                    }
                // 不需要下载,直接完成
                } else {
                    OnComplete();
                }
            }
        }

        // 下载 ver, 获取需要下载的文件信息
        private IEnumerator RequestVersions() {
            // UpdateScreen->OnMessage
            OnMessage("正在获取版本信息...");
            // 网络有问题
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                var mb = MessageBox.Show("提示", "请检查网络连接状态", "重试", "退出");
                yield return mb;
                // 继续下载
                if (mb.isOk) {
                    StartUpdate();
                // 直接退出 游戏
                } else {
                    Quit();
                }
                yield break;
            }


            // 下载 ver e.g. http://192.168.5.121/DLC/Windows/ver
            var request = UnityWebRequest.Get(GetDownloadURL(Versions.VerName));
            request.downloadHandler = new DownloadHandlerFile(_savePath + Versions.VerName);
            yield return request.SendWebRequest();
            var error = request.error;
            request.Dispose();
            // 下载出错
            if (!string.IsNullOrEmpty(error)) {
                var mb = MessageBox.Show("提示", string.Format("获取服务器版本失败：{0}", error), "重试", "退出");
                yield return mb;
                // 重新下载
                if (mb.isOk) {
                    StartUpdate();
                // 退出游戏
                } else {
                    Quit();
                }
                yield break;
            }


            try {
                // 获取 P 目录中 下载的 ver 文件里 包含的 VFile, 放到 Versions._updateData
                _versions = Versions.LoadVersions(_savePath + Versions.VerName, true);

                if (_versions.Count > 0) {
                    // 获取需要下载的 VFile
                    PrepareDownloads();
                    // 进入准备下载阶段
                    _step = Step.Prepared;
                } else {
                    // 直接完成
                    OnComplete();
                }
            // 意外情况
            } catch (Exception e) {
                Debug.LogException(e);
                MessageBox.Show("提示", "版本文件加载失败", "重试", "退出").onComplete +=
                    delegate (MessageBox.EventId id) {
                        // 重新下载
                        if (id == MessageBox.EventId.Ok) {
                            StartUpdate();
                        // 退出游戏
                        } else {
                            Quit();
                        }
                    };
            }
        }

        // 获取 S目录， 主要是 不同平台 要不同处理
        private static string GetStreamingAssetsPath() {
            if (Application.platform == RuntimePlatform.Android) {
                return Application.streamingAssetsPath;
            }

            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor) {
                return "file:///" + Application.streamingAssetsPath;
            }

            return "file://" + Application.streamingAssetsPath;
        }

        // 是否将资源从 streamingAssetsPath 拷贝到 persistentDataPath
        private IEnumerator RequestCopy() {
            // 读取 P目录中 ver 文件的版本信息
            var v1 = Versions.LoadVersion(_savePath + Versions.VerName);
            // 获取 S目录
            var basePath = GetStreamingAssetsPath() + "/";

            // 下载 S 目录中的 ver 文件，如果有的话
            // e.g. file:///D:/Projects/UnityProjecvts/xasset-master/Assets/StreamingAssets/ver
            var request = UnityWebRequest.Get(basePath + Versions.VerName);

            // 从 s 目录下载 ver 到 p 目录下的 ver.tmp
            var path = _savePath + Versions.VerName + ".tmp";
            request.downloadHandler = new DownloadHandlerFile(path);
            // 开始下载
            yield return request.SendWebRequest();

            if (string.IsNullOrEmpty(request.error)) {
                // 获取p 目录下的 ver.tmp 中的 版本号
                var v2 = Versions.LoadVersion(path);
                // P目录下的 ver.tmp 的版本号 大于 P目录下的 ver 的版本号, 给出两个选择
                // Step.Copying: 将 S 目录下的文件拷贝到 P目录下
                // Step.Versions: 跳过, 进入下载阶段
                if (v2 > v1) {
                    var mb = MessageBox.Show("提示", "是否将资源解压到本地？", "解压", "跳过");
                    yield return mb;
                    _step = mb.isOk ? Step.Coping : Step.Versions;
                // S 目录的版本号 不大于 P目录的版本号
                // 加载 VFile 到 Versions._baseData, 进入 下载阶段
                } else {
                    Versions.LoadVersions(path);
                    _step = Step.Versions;
                }
            // S 目录没有 ver, 直接进入下载阶段
            } else {
                _step = Step.Versions;
            }
            request.Dispose();
        }

        // 根据 VFile 从 S 目录 拷贝到 P 目录
        private IEnumerator UpdateCopy(IList<VFile> versions, string basePath) {
            // 读取第一个 VFile
            var version = versions[0];
            // 如果第一个 VFile 是 res
            if (version.name.Equals(Versions.ResName)) {
                // 从 S 目录中 拷贝 res 文件 到 P目录
                var request = UnityWebRequest.Get(basePath + version.name);
                request.downloadHandler = new DownloadHandlerFile(_savePath + version.name);
                var req = request.SendWebRequest();
                while (!req.isDone) {
                    // UpdateScreen.OnMessage
                    OnMessage("正在复制文件");
                    // UpdateScreen.OnProgress
                    // 单个 res 的下载进度
                    OnProgress(req.progress);
                    yield return null;
                }

                request.Dispose();
            // 第一个文件不是 res
            } else {
                // 下载每个 单独的 VFile
                for (var index = 0; index < versions.Count; index++) {

                    var item = versions[index];
                    var request = UnityWebRequest.Get(basePath + item.name);
                    request.downloadHandler = new DownloadHandlerFile(_savePath + item.name);
                    yield return request.SendWebRequest();
                    request.Dispose();
                    OnMessage(string.Format("正在复制文件：{0}/{1}", index, versions.Count));
                    // 所有 VFile 中的 数量进度
                    OnProgress(index * 1f / versions.Count);
                }
            }
        }

        private void OnComplete() {
            // 如果开启了VFS
            if (enableVFS) {
                var dataPath = _savePath + Versions.ResName;
                var downloads = _downloader.downloads;
                if (downloads.Count > 0 && File.Exists(dataPath)) {
                    OnMessage("更新本地版本信息");
                    var files = new List<VFile>(downloads.Count);
                    foreach (var download in downloads) {
                        files.Add(new VFile {
                            name = download.name,
                            hash = download.hash,
                            len = download.len,
                        });
                    }

                    var file = files[0];
                    if (!file.name.Equals(Versions.ResName)) {
                        Versions.UpdateDisk(dataPath, files);
                    }
                }

                Versions.LoadDisk(dataPath);
            }

            // UpdateScreen 进度100%
            OnProgress(1);
            // UpdateScreen 显示完成信息
            OnMessage("更新完成");

            // 获取 版本号
            var version = Versions.LoadVersion(_savePath + Versions.VerName);
            // 显示版本到 UpdateScreen
            if (version > 0) {
                OnVersion(version.ToString());
            }

            // 加载游戏场景
            StartCoroutine(LoadGameScene());
        }

        // 记载游戏场景
        private IEnumerator LoadGameScene() {
            OnMessage("正在初始化");

            // 加载 Manifest
            var manifest = Assets.Initialize();

            // Debug.Log("manifest.isDone: " + manifest.isDone + "," + Time.frameCount);

            // 等待 manifest.isDone
            yield return manifest;

            // Debug.Log("manifest.isDone: " + manifest.isDone + "," + Time.frameCount);

            if (string.IsNullOrEmpty(manifest.error)) {
                Assets.AddSearchPath("Assets/XAsset/Demo/Scenes");

                manifest.Release();

                OnProgress(0);
                OnMessage("加载游戏场景");

                // 加载场景（异步）
                var sceneAssetRequest = Assets.LoadSceneAsync(gameScene, false);

                // 等待场景加载完成
                while (!sceneAssetRequest.isDone) {
                    OnProgress(sceneAssetRequest.progress);
                    yield return null;
                }


            } else {
                manifest.Release();
                var mb = MessageBox.Show("提示", "初始化异常错误：" + manifest.error + "请联系技术支持");
                yield return mb;
                Quit();
            }
        }

        private void OnDestroy() {
            MessageBox.Dispose();
        }

        private void Quit() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
