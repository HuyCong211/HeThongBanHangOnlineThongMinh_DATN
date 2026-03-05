using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DoAn_Ver2.Models;

namespace DoAn_Ver2.Models.ViewModel
{
    public class ProductListViewModel
    {
        // Dữ liệu sản phẩm
        public IEnumerable<SanPham> Products { get; set; }

        // Dữ liệu bộ lọc (Master Data) để vẽ sidebar
        public List<MauSac> Colors { get; set; }
        public List<KichThuoc> Sizes { get; set; }
        public List<string> Styles { get; set; } // List kiểu dáng lấy từ cột KieuDang (Distinct)

        // Trạng thái hiện tại (để giữ tích chọn khi chuyển trang)
        public int? CurrentCateID { get; set; }
        public string SortBy { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }

        // Các giá trị đang lọc
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public List<int> SelectedColors { get; set; }
        public List<int> SelectedSizes { get; set; }
        public string SelectedStyle { get; set; }

        public ProductListViewModel()
        {
            SelectedColors = new List<int>();
            SelectedSizes = new List<int>();
        }
    }
}