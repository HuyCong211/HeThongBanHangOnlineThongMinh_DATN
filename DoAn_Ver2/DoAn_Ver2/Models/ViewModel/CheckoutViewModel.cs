using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using DoAn_Ver2.Models;

namespace DoAn_Ver2.Models.ViewModel
{
    public class CheckoutViewModel
    {
        // 1. Thông tin người nhận
        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
        public string TenNguoiNhan { get; set; } // Khớp với DB: TenNguoiNhan

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string SDT { get; set; } // Khớp với DB: SDT_NguoiNhan (sẽ map ở Controller)

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        public string Email { get; set; } // Khớp với DB: EmailNguoiNhan

        // 2. Địa chỉ (3 cấp hành chính)
        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành")]
        public string TinhThanh { get; set; }

        

        [Required(ErrorMessage = "Vui lòng chọn Phường/Xã")]
        public string PhuongXa { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể")]
        public string DiaChiChiTiet { get; set; }

        public string GhiChu { get; set; }

        // 3. Thanh toán
        public int PhuongThucThanhToan { get; set; } = 1; // 1: COD, 2: VNPay

        // 4. Dữ liệu hiển thị
        public List<CartItemViewModel> CartItems { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal PhiShip { get; set; } = 30000;
        // --- [MỚI] THÊM CÁC TRƯỜNG CHO VOUCHER ---
        public string MaVoucher { get; set; } // Mã code khách chọn (gửi lên server)
        public decimal SoTienGiam { get; set; } = 0; // Hiển thị số tiền được giảm

        // Danh sách voucher phù hợp để hiển thị cho khách chọn
        public List<MaGiamGia> DsVoucherKhaDung { get; set; }
        // Tính tổng cuối cùng (Logic hiển thị)
        public decimal TongThanhToan => (TongTienHang + PhiShip) - SoTienGiam;

        // 5. Dành cho Member (Sổ địa chỉ)
        public List<DiaChi> SoDiaChi { get; set; }

        public CheckoutViewModel()
        {
            CartItems = new List<CartItemViewModel>();
            SoDiaChi = new List<DiaChi>();
            DsVoucherKhaDung = new List<MaGiamGia>();
        }
    }
}