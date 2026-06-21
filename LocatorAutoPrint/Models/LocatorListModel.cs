using LocatorAutoPrint.ViewModels;

namespace LocatorAutoPrint.Models
{
    public class LocatorListModel : ViewModelBase
    {
        private string _slotNo;
        private int _recordCount;
        private bool _inUse;
        private bool _closed;

        public string SlotNo { get => _slotNo; set { _slotNo = value; OnPropertyChanged(); } }
        public int RecordCount { get => _recordCount; set { _recordCount = value; OnPropertyChanged(); } }
        public bool InUse { get => _inUse; set { _inUse = value; OnPropertyChanged(); } }
        public bool Closed { get => _closed; set { _closed = value; OnPropertyChanged(); } }

        public string Location { get; set; }
        public string Status { get; set; }
    }
}