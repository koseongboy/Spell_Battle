using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Models.PlayerModels;
using Unity.Netcode;

namespace DefaultNamespace 
{
    public class PlayerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private PlayerModel _model;

        // UI Toolkit 바인딩용 프로퍼티들
        private int _currentHp;
        private int _maxHp;
        private int _currentMana;
        private int _maxMana;
        private int _finalMana;
        private int _expectedManaCost;

        public int CurrentHp { get => _currentHp; set { if (_currentHp != value) { _currentHp = value; OnPropertyChanged(); } } }
        public int MaxHp { get => _maxHp; set { if (_maxHp != value) { _maxHp = value; OnPropertyChanged(); } } }
        public int CurrentMana { get => _currentMana; set { if (_currentMana != value) { _currentMana = value; OnPropertyChanged(); } } }
        public int MaxMana { get => _maxMana; set { if (_maxMana != value) { _maxMana = value; OnPropertyChanged(); } } }
        public int FinalMana { get => _finalMana; set { if (_finalMana != value) { _finalMana = value; OnPropertyChanged(); } } }
        public int ExpectedManaCost { get => _expectedManaCost; set { if (_expectedManaCost != value) { _expectedManaCost = value; OnPropertyChanged(); } } }

        // 컬렉션 변경 알림용 이벤트 (UI에서 리스트 갱신을 구독할 수 있도록 제공)
        public event Action OnHandChanged;
        public event Action OnStatusChanged;

        public PlayerModel Model => _model;

        public void Initialize(PlayerModel model)
        {
            _model = model;

            // 초기값 설정
            UpdateHpProperties();
            UpdateManaProperties();

            // Model(NetworkVariable) 구독 설정
            _model.CurrentHealth.OnValueChanged += OnHpChanged;
            _model.MaxHealth.OnValueChanged += OnMaxHpChanged;
            _model.CurrentMana.OnValueChanged += OnCurrentManaChanged;
            _model.MaxMana.OnValueChanged += OnMaxManaChanged;
            _model.OnExpectedManaChanged += OnExpectedManaCostChanged;

            _model.ActiveStatuses.OnListChanged += (e) => OnStatusChanged?.Invoke();
            if (_model.Hand != null)
            {
                _model.Hand.localHand.CollectionChanged += (s, e) => OnHandChanged?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_model == null) return;
            _model.CurrentHealth.OnValueChanged -= OnHpChanged;
            _model.MaxHealth.OnValueChanged -= OnMaxHpChanged;
            _model.CurrentMana.OnValueChanged -= OnCurrentManaChanged;
            _model.MaxMana.OnValueChanged -= OnMaxManaChanged;
            _model.OnExpectedManaChanged -= OnExpectedManaCostChanged;
        }

        private void OnHpChanged(int old, int newVal) => UpdateHpProperties();
        private void OnMaxHpChanged(int old, int newVal) => UpdateHpProperties();
        private void UpdateHpProperties()
        {
            CurrentHp = _model.CurrentHealth.Value;
            MaxHp = _model.MaxHealth.Value;
        }

        private void OnCurrentManaChanged(int old, int newVal) => UpdateManaProperties();
        private void OnMaxManaChanged(int old, int newVal) => UpdateManaProperties();
        private void OnExpectedManaCostChanged(int newVal) => UpdateManaProperties();
        private void UpdateManaProperties()
        {
            CurrentMana = _model.CurrentMana.Value;
            MaxMana = _model.MaxMana.Value;
            FinalMana = _model.FinalMana.Value;
            ExpectedManaCost = _model.ExpectedManaCost;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}