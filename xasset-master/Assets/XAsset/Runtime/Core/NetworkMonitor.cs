using UnityEngine;

namespace libx {

    public interface INetworkMonitorListener {
        // ���绷���ı�ص�
        void OnReachablityChanged(NetworkReachability reachability);
    }

    // ��������
    public class NetworkMonitor : MonoBehaviour {
        private NetworkReachability _reachability;
        public INetworkMonitorListener listener { get; set; }
        // �������״̬��ʱ����
        [SerializeField] private float sampleTime = 0.5f;
        // �������غ󾭹���ʱ��
        private float _time;
        // NetworkMonitor �Ƿ��Ѿ�����
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
                        // ���绷���ı�, �����ص�
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