namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DonHang")]
    public partial class DonHang
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DonHang()
        {
            ChiTietDonHangs = new HashSet<ChiTietDonHang>();
        }

        public int ID { get; set; }

        [StringLength(155)]
        public string MaDonHang { get; set; }

        public int? NguoiDungID { get; set; }

        public DateTime? NgayDat { get; set; }

        [StringLength(100)]
        public string TenNguoiNhan { get; set; }

        [StringLength(10)]
        public string SDT_NguoiNhan { get; set; }

        [StringLength(100)]
        public string EmailNguoiNhan { get; set; }

        [StringLength(255)]
        public string DiaChiGiaoHang { get; set; }

        public decimal? TongTien { get; set; }

        public decimal? PhiVanChuyen { get; set; }

        public decimal? GiamGia { get; set; }

        public decimal? TongThanhToan { get; set; }

        public int? PhuongThucThanhToanID { get; set; }

        public bool? TrangThaiThanhToan { get; set; }

        public int? TrangThaiDonHang { get; set; }

        [StringLength(255)]
        public string LyDoHuy { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }

        public virtual PhuongThucThanhToan PhuongThucThanhToan { get; set; }
    }
}
