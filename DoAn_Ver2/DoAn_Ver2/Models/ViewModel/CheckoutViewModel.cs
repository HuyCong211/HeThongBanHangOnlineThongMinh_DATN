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
        // --- VOUCHER: TÁCH THÀNH 2 LOẠI ---
        // Mã freeship (chỉ áp dụng khi đơn > 500k)
        public string MaFreeShip { get; set; }

        // Mã giảm giá thông thường (PERCENT hoặc AMOUNT)
        public string MaVoucherGiam { get; set; }

        // Giữ field MaVoucher để tương thích với form cũ (sẽ không dùng trong logic mới)
        // Nếu muốn xóa hẳn thì remove dòng này và cập nhật form POST
        public string MaVoucher => MaVoucherGiam; // alias để không break code cũ

        // Số tiền được giảm (từ voucher giảm giá)
        public decimal SoTienGiamVoucher { get; set; } = 0;

        // Phí ship thực tế (sau khi áp freeship = 0)
        public decimal PhiShipThucTe => !string.IsNullOrEmpty(MaFreeShip) ? 0 : PhiShip;

        // Tổng số tiền giảm (freeship + voucher giảm giá)
        public decimal TongSoTienGiam => SoTienGiamVoucher + (PhiShip - PhiShipThucTe);

        // Tổng thanh toán cuối cùng
        public decimal TongThanhToan => (TongTienHang + PhiShipThucTe) - SoTienGiamVoucher;

        // --- DANH SÁCH VOUCHER PHÂN LOẠI ---
        // Voucher free ship (LoaiGiam == "FREE_SHIP") — hiển thị khi đơn > 500k
        public List<MaGiamGia> DsVoucherFreeShip { get; set; }

        // Voucher giảm giá thông thường (PERCENT / AMOUNT)
        public List<MaGiamGia> DsVoucherGiamGia { get; set; }

        // 5. Dành cho Member (Sổ địa chỉ)
        public List<DiaChi> SoDiaChi { get; set; }

        // Ngưỡng free ship
        public const decimal NGUONG_FREE_SHIP = 500000;

        // Computed helpers cho View
        public bool DuDieuKienFreeShip => TongTienHang >= NGUONG_FREE_SHIP;

        public CheckoutViewModel()
        {
            CartItems = new List<CartItemViewModel>();
            SoDiaChi = new List<DiaChi>();
            DsVoucherFreeShip = new List<MaGiamGia>();
            DsVoucherGiamGia = new List<MaGiamGia>();
        }
    }
}