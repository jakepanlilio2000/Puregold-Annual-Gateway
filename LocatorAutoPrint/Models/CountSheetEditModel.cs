using LocatorAutoPrint.ViewModels;

namespace LocatorAutoPrint.Models
{
    public class CountSheetEditModel : ViewModelBase
    {
        private string _slotNo;
        private int _recNo;
        private string _upc;
        private string _sku;
        private string _descr;
        private double _editedQty;
        private double _originalQty; 

        public string SlotNo { get => _slotNo; set { _slotNo = value; OnPropertyChanged(); } }
        public int RecNo { get => _recNo; set { _recNo = value; OnPropertyChanged(); } }
        public string UPC { get => _upc; set { _upc = value; OnPropertyChanged(); } }
        public string SKU { get => _sku; set { _sku = value; OnPropertyChanged(); } }
        public string Descr { get => _descr; set { _descr = value; OnPropertyChanged(); } }
        public double EditedQty { get => _editedQty; set { _editedQty = value; OnPropertyChanged(); } }
        public double OriginalQty { get => _originalQty; set { _originalQty = value; OnPropertyChanged(); } }
    }

    public class ItemLookupResult
    {
        public string UPC { get; set; }
        public string SKU { get; set; }
        public string Description { get; set; }
    }
}