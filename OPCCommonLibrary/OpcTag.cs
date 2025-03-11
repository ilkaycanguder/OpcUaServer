using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCCommonLibrary
{
    public class OpcTag : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private string _tagName;
        private int _tagValue;
        private bool _isUpdated;
        private DateTime _lastUpdate;
        private string _state; // 🔥 UI'da State değişimini takip etmek için
        private int _id;
        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }
        public bool IsUpdated
        {
            get => _isUpdated;
            set
            {
                if (_isUpdated != value)
                {
                    _isUpdated = value;
                    OnPropertyChanged(nameof(IsUpdated));
                }
            }
        }
        public string TagName
        {
            get => _tagName;
            set
            {
                _tagName = value;
                OnPropertyChanged(nameof(TagName));
            }
        }

        public int TagValue
        {
            get => _tagValue;
            set
            {
                if (_tagValue != value)
                {
                    _tagValue = value;
                    IsUpdated = true;  // ✅ Değişiklik olduysa UI güncellenecek
                    OnPropertyChanged(nameof(TagValue));
                    OnPropertyChanged(nameof(IsUpdated));

                    // 2 saniye sonra güncelleme efektini kaldır
                    Task.Delay(2000).ContinueWith(t => IsUpdated = false);
                }
            }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged(nameof(LastUpdate));
            }
        }

        public string State
        {
            get => _state;
            set
            {
                _state = value;
                OnPropertyChanged(nameof(State));
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}
