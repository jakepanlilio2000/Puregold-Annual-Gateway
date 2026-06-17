using LocatorAutoPrint.ViewModels;

namespace LocatorAutoPrint.Models
{
    public class PrelocModel : ViewModelBase
    {
        private string _slotNo;
        private string _name;

        public string SlotNo { get => _slotNo; set { _slotNo = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    }
}