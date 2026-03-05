using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn_Ver2.Models.ViewModel
{
    // Class đại diện cho 1 dòng sản phẩm trong giỏ
    public class CartItemViewModel
    {
        public int SanPhamID { get; set; }
        public int SKU_ID { get; set; } // ID của Biến thể (Quan trọng nhất để xác định Màu/Size)

        public string TenSanPham { get; set; }
        public string TenPhanLoai { get; set; } // VD: Màu Đen / Size L
        public string HinhAnh { get; set; } // Ưu tiên ảnh của biến thể, nếu không có thì lấy ảnh chính

        public decimal DonGia { get; set; } // Giá bán hiện tại
        public decimal? GiaGoc { get; set; } // Giá gốc (để hiện gạch ngang nếu có giảm giá)

        public int SoLuong { get; set; } // Số lượng khách muốn mua
        public int TonKho { get; set; } // Số lượng tồn kho thực tế (để validate không cho mua quá)

        // Tự động tính thành tiền
        public decimal ThanhTien => SoLuong * DonGia;
    }

    // Class đại diện cho toàn bộ Giỏ hàng (dùng để truyền sang View)
    public class ShoppingCartViewModel
    {
        public List<CartItemViewModel> Items { get; set; }
        public decimal TongTienHang => Items.Sum(x => x.ThanhTien);
        public int TongSoLuong => Items.Sum(x => x.SoLuong);

        public ShoppingCartViewModel()
        {
            Items = new List<CartItemViewModel>();
        }
    }
}