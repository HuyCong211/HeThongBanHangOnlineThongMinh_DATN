using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn_Ver2.Models.ViewModel
{
    public class OrderDetailDisplayVM
    {
        public int SanPhamID { get; set; }
        public string TenSanPham { get; set; }
        public string TenPhanLoai { get; set; } // Ví dụ: Màu Đen / Size L
        public string HinhAnh { get; set; }     // Ảnh đại diện của màu đó
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
        public bool CanReview { get; set; }
    }
}