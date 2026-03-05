using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using DoAn_Ver2.Models;

namespace DoAn_Ver2.Models.ViewModel
{
    // 1. Cập nhật thông tin cơ bản
    public class CustomerInfoViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        public string SDT { get; set; }

        public string Avt { get; set; }
    }

    // 2. Đổi mật khẩu
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Nhập mật khẩu hiện tại")]
        public string MatKhauCu { get; set; }

        [Required(ErrorMessage = "Nhập mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải trên 6 ký tự")]
        public string MatKhauMoi { get; set; }

        [System.ComponentModel.DataAnnotations.Compare("MatKhauMoi", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string XacNhanMatKhau { get; set; }
    }

    // 3. Sổ địa chỉ (Dùng cho thêm mới)
    public class UserAddressViewModel
    {
        public int ID { get; set; } // Dùng cho sửa/xóa

        [Required(ErrorMessage = "Nhập tên người nhận")]
        public string TenNguoiNhan { get; set; }

        [Required(ErrorMessage = "Nhập số điện thoại")]
        public string SDT { get; set; }

        [Required(ErrorMessage = "Chọn Tỉnh/Thành")]
        public string TinhThanh { get; set; }

        [Required(ErrorMessage = "Chọn Phường/Xã")]
        public string PhuongXa { get; set; }

        [Required(ErrorMessage = "Nhập địa chỉ chi tiết")]
        public string DiaChiChiTiet { get; set; }

        public bool MacDinh { get; set; }
    }
}