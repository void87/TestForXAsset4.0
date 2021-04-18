using UnityEngine;

namespace libx {

    public interface INetworkMonitorListener {
        // 网络环境改变回调
        void OnReachablityChanged(NetworkReachability reachability);
    }

    // 网络监控器
    public class NetworkMonitor : MonoBehaviour {
        private NetworkReachability _reachability;
        public INetworkMonitorListener listener { get; set; }
        // 检查网络状态的时间间隔
        [SerializeField] private float sampleTime = 0.5f;
        // 场景加载后经过的时间
        private float _time;
        // NetworkMonitor 是否已经启动
        private bool _started;

        private void Start() {
            _reachability = Application.internetReachability;
            Restart();
        }

        public void Restart() {
            _time = Time.timeSinceLevelLoad;
            _started = true;
        }

        public void Stop() {
            _started = false;
        }

        private void Update() {
            if (_started && Time.timeSinceLevelLoad - _time >= sampleTime) {
                var state = Application.internetReachability;
                if (_reachability != state) {
                    if (listener != null) {
                        // 网络环境改变, 触发回调
                        // Updater->OnReachablityChanged
                        listener.OnReachablityChanged(state);
                    }
                    _reachability = state;
                }
                _time = Time.timeSinceLevelLoad;
            }
        }
    }
}